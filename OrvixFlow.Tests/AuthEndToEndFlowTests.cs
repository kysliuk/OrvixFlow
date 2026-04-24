using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Filters;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Tests;

public class AuthEndToEndFlowTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _tenantId;
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly Mock<IConfiguration> _configMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly CompanyBootstrapService _companyBootstrapService;

    public AuthEndToEndFlowTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, new TestTenantProvider(_tenantId));
        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Primary Company", Plan = "Free", SubscriptionStatus = "Active" });
        _db.PlanTemplates.Add(new PlanTemplate
        {
            Id = PlanCatalog.FreeId,
            Name = "Free",
            Slug = "free",
            IsActive = true,
            MaxSeats = 5
        });
        _db.SaveChanges();

        _configMock.Setup(c => c["Jwt:Secret"]).Returns("test-secret-key-for-testing-min-32-chars");
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        _configMock.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
        _configMock.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:3000");

        _companyBootstrapService = new CompanyBootstrapService(_db, Mock.Of<ILogger<CompanyBootstrapService>>());
    }

    [Fact]
    public async Task Register_Verify_Login_EndToEnd_ShouldSucceed()
    {
        var authService = CreateAuthService();

        var registerResult = await authService.RegisterAsync("flow@example.com", "ValidPassword123!", "Flow User");
        registerResult.IsSuccess.Should().BeTrue();

        var user = await _db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "flow@example.com");
        user.EmailVerified.Should().BeFalse();

        var verificationNotification = await _db.NotificationQueues.IgnoreQueryFilters()
            .SingleAsync(n => n.RecipientEmail == "flow@example.com" && n.Subject == "Verify your OrvixFlow account");
        var verificationToken = ExtractToken(verificationNotification.Body!, "verify");

        var verifyResult = await authService.VerifyEmailAsync(verificationToken);
        verifyResult.IsSuccess.Should().BeTrue();

        var loginResult = await authService.LoginAsync("flow@example.com", "ValidPassword123!");
        loginResult.IsSuccess.Should().BeTrue();
        loginResult.Token.Should().NotBeNullOrWhiteSpace();
        loginResult.RefreshToken.Should().NotBeNullOrWhiteSpace();
        loginResult.Profile.Should().NotBeNull();
        loginResult.Profile!.Plan.Should().Be("Free");
        loginResult.Profile.ActiveCompanyId.Should().Be(user.TenantId);
    }

    [Fact]
    public async Task Invite_Send_Accept_EndToEnd_ShouldProvisionMembershipAndSession()
    {
        var authService = CreateAuthService();
        var inviterId = Guid.NewGuid();

        _db.Users.Add(new User
        {
            Id = inviterId,
            Email = "inviter@example.com",
            DisplayName = "Inviter",
            TenantId = _tenantId,
            EmailVerified = true,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!")
        });
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = inviterId,
            CompanyId = _tenantId,
            CompanyRole = "CompanyOwner",
            Status = "Active"
        });
        await _db.SaveChangesAsync();

        var inviteResult = await authService.InviteUserAsync(new InviteRequest(inviterId, _tenantId, "invite-flow@example.com", "CompanyMember"));
        inviteResult.IsSuccess.Should().BeTrue();
        inviteResult.Token.Should().NotBeNullOrWhiteSpace();

        var inviteNotification = await _db.NotificationQueues.IgnoreQueryFilters()
            .FirstAsync(n => n.RecipientEmail == "invite-flow@example.com" && n.Subject == "Verify your OrvixFlow invitation");
        var inviteToken = ExtractToken(inviteNotification.Body!, "invite");

        var acceptResult = await authService.AcceptInvitationAsync(inviteToken, "Invite Flow User", "ValidPassword123!");
        acceptResult.IsSuccess.Should().BeTrue();
        acceptResult.Token.Should().NotBeNullOrWhiteSpace();
        acceptResult.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var invitedUser = await _db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "invite-flow@example.com");
        invitedUser.EmailVerified.Should().BeTrue();

        var membership = await _db.UserCompanyMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == invitedUser.Id && m.CompanyId == _tenantId);
        membership.CompanyRole.Should().Be("CompanyMember");
        membership.Status.Should().Be("Active");
    }

    /// <summary>
    /// KEY SCENARIO: Company owner invites a brand new user (no prior company) without specifying
    /// a department. After accepting, the user must be auto-assigned to the company's General
    /// department so that AccessResolver can grant them module access.
    ///
    /// Root cause without this fix:
    ///   - AcceptInvitationAsync only creates UserDepartmentMembership when DepartmentId.HasValue
    ///   - CompanyMember with no dept memberships → ScopeContext.AllowedDepartmentIds = []
    ///   - AccessResolver.GetEffectivePermissionsAsync → departmentIds.Count == 0 → Empty()
    ///   - User gets 403 on all module endpoints despite company being entitled
    /// </summary>
    [Fact]
    public async Task Invite_NoDept_Accept_UserGetsGeneralDeptMembership_ForModuleAccess()
    {
        var authService = CreateAuthService();
        var ownerId = Guid.NewGuid();

        // Owner sets up their company (EnsureOwnerBootstrapAsync creates General dept)
        _db.Users.Add(new User
        {
            Id = ownerId,
            Email = "owner@company.com",
            DisplayName = "Owner",
            TenantId = _tenantId,
            EmailVerified = true,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!")
        });
        await _db.SaveChangesAsync();
        await _companyBootstrapService.EnsureOwnerBootstrapAsync(ownerId, _tenantId);

        // Verify bootstrap created the General department
        var generalDept = await _db.Departments.IgnoreQueryFilters()
            .SingleAsync(d => d.CompanyId == _tenantId && d.Code == "general");
        generalDept.Should().NotBeNull("EnsureOwnerBootstrapAsync must create a General department");

        // Owner invites a brand-new user WITHOUT specifying a department
        var inviteResult = await authService.InviteUserAsync(
            new InviteRequest(ownerId, _tenantId, "newuser@example.com", "CompanyMember"));
        inviteResult.IsSuccess.Should().BeTrue("InviteUserAsync must succeed");

        var notification = await _db.NotificationQueues.IgnoreQueryFilters()
            .FirstAsync(n => n.RecipientEmail == "newuser@example.com");
        var inviteToken = ExtractToken(notification.Body!, "invite");

        // Brand-new user accepts (creates account + membership)
        var acceptResult = await authService.AcceptInvitationAsync(inviteToken, "New User", "ValidPassword123!");
        acceptResult.IsSuccess.Should().BeTrue("AcceptInvitationAsync must succeed");

        var newUser = await _db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "newuser@example.com");

        // Company membership must be created
        var companyMembership = await _db.UserCompanyMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == newUser.Id && m.CompanyId == _tenantId);
        companyMembership.CompanyRole.Should().Be("CompanyMember");
        companyMembership.Status.Should().Be("Active");

        // CRITICAL: User must be auto-assigned to General dept so AccessResolver grants them access
        var deptMembership = await _db.UserDepartmentMemberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == newUser.Id && m.CompanyId == _tenantId);

        deptMembership.Should().NotBeNull(
            "AcceptInvitationAsync must auto-assign user to the General department " +
            "when no specific department is given — otherwise AccessResolver blocks all module access (departmentIds.Count == 0 → Empty())");
        deptMembership!.DepartmentId.Should().Be(generalDept.Id,
            "User must be placed in the General department");
        deptMembership.Status.Should().Be("Active");
    }

    /// <summary>
    /// When an invitation specifies an explicit department, the user is assigned there
    /// (not to General). The explicit dept takes precedence.
    /// </summary>
    [Fact]
    public async Task Invite_WithExplicitDept_Accept_UserGetsSpecificDeptMembership()
    {
        var authService = CreateAuthService();
        var ownerId = Guid.NewGuid();

        _db.Users.Add(new User
        {
            Id = ownerId,
            Email = "owner2@company.com",
            DisplayName = "Owner2",
            TenantId = _tenantId,
            EmailVerified = true,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPassword123!")
        });
        await _db.SaveChangesAsync();
        await _companyBootstrapService.EnsureOwnerBootstrapAsync(ownerId, _tenantId);

        // Create a specific department
        var salesDeptId = Guid.NewGuid();
        _db.Departments.Add(new Department
        {
            Id = salesDeptId,
            CompanyId = _tenantId,
            Name = "Sales",
            Code = "sales",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        // Owner invites with explicit Sales dept + DepartmentOperator role
        var inviteResult = await authService.InviteUserAsync(
            new InviteRequest(ownerId, _tenantId, "salesuser@example.com", "CompanyMember",
                DepartmentId: salesDeptId, InvitedDepartmentRole: "DepartmentOperator"));
        inviteResult.IsSuccess.Should().BeTrue();

        var notification = await _db.NotificationQueues.IgnoreQueryFilters()
            .FirstAsync(n => n.RecipientEmail == "salesuser@example.com");
        var inviteToken = ExtractToken(notification.Body!, "invite");

        var acceptResult = await authService.AcceptInvitationAsync(inviteToken, "Sales User", "ValidPassword123!");
        acceptResult.IsSuccess.Should().BeTrue();

        var newUser = await _db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "salesuser@example.com");

        var deptMembership = await _db.UserDepartmentMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == newUser.Id && m.CompanyId == _tenantId);
        deptMembership.DepartmentId.Should().Be(salesDeptId, "User must be in the explicit Sales department");
        deptMembership.DepartmentRole.Should().Be("DepartmentOperator");
        deptMembership.Status.Should().Be("Active");

        var companyMembership = await _db.UserCompanyMemberships.IgnoreQueryFilters()
            .SingleAsync(m => m.UserId == newUser.Id && m.CompanyId == _tenantId);
        companyMembership.CompanyRole.Should().Be("CompanyMember");

        _db.ModuleDefinitions.AddRange(
            new ModuleDefinition
            {
                Key = "inbox-guardian",
                DisplayName = "Inbox Guardian",
                Tier = "Starter",
                Visibility = "UserFacing",
                IsActive = true
            },
            new ModuleDefinition
            {
                Key = "knowledge-base",
                DisplayName = "Knowledge Base",
                Tier = "Starter",
                Visibility = "UserFacing",
                IsActive = true
            });
        await _db.SaveChangesAsync();

        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", newUser.Id.ToString()),
            new Claim("Role", "CompanyMember"),
            new Claim("ActiveCompanyId", _tenantId.ToString()),
            new Claim("TenantId", _tenantId.ToString())
        ], "TestAuth"));

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var scopeContext = new ScopeContext(httpContextAccessor, _db);
        var entitlementResolver = new Mock<IEntitlementResolver>();
        entitlementResolver
            .Setup(x => x.CanUseModuleWithOverridesAsync(_tenantId, It.IsAny<string>()))
            .ReturnsAsync(true);

        var accessResolver = new AccessResolver(_db, scopeContext, entitlementResolver.Object);

        var inboxPermissions = await accessResolver.GetEffectivePermissionsAsync(newUser.Id, _tenantId, "inbox-guardian");
        inboxPermissions.CanView.Should().BeTrue();
        inboxPermissions.CanUse.Should().BeTrue();

        var knowledgePermissions = await accessResolver.GetEffectivePermissionsAsync(newUser.Id, _tenantId, "knowledge-base");
        knowledgePermissions.CanView.Should().BeTrue();
        knowledgePermissions.CanUse.Should().BeTrue();

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(httpContextAccessor)
            .AddScoped<IScopeContext>(_ => new ScopeContext(httpContextAccessor, _db))
            .AddSingleton(entitlementResolver.Object)
            .AddScoped<IAccessResolver>(_ => new AccessResolver(_db, new ScopeContext(httpContextAccessor, _db), entitlementResolver.Object))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal,
            RequestServices = services.CreateScope().ServiceProvider
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var authorizationContext = new AuthorizationFilterContext(actionContext, new System.Collections.Generic.List<IFilterMetadata>());

        var attr = new RequireModuleAttribute("knowledge-base");
        await attr.OnAuthorizationAsync(authorizationContext);
        authorizationContext.Result.Should().BeNull("accepted DepartmentOperator invite should pass RequireModule when the company is entitled");
    }

    private AuthService CreateAuthService() => new(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object, _companyBootstrapService);

    private static string ExtractToken(string body, string route)
    {
        var match = Regex.Match(body, $@"/{route}\?token=([^'\""\s<]+)");
        match.Success.Should().BeTrue($"Expected to find a {route} token link in the queued email body.");
        return match.Groups[1].Value;
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetTenantId() => _tenantId;
    }
}
