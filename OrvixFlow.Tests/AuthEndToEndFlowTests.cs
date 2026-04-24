using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
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
