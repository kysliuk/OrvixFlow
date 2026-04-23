using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for F-08: Invitation role ceiling check
/// Verifies that callers cannot assign roles higher than their own.
/// Only callers who pass IsCompanyAdminOrAbove() (CompanyAdmin, CompanyOwner, SuperAdmin)
/// can send invitations. DepartmentManager and Operator get Forbid at the admin check.
/// </summary>
public class RoleCeilingTests : IDisposable
{
    // Each test gets its own database name for complete isolation
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly AppDbContext _db;
    private readonly MockTenantProvider _tenantProvider;

    public RoleCeilingTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _tenantProvider = new MockTenantProvider();
        _db = new AppDbContext(options, _tenantProvider);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private InviteController CreateController(string role, Guid userId, Guid companyId)
    {
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("Role", role),
            new Claim("ActiveCompanyId", companyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };

        var entitlementResolver = new EntitlementResolver(_db);
        var configMock = new Moq.Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var authService = new AuthService(_db, configMock.Object, Mock.Of<ILogger<AuthService>>(), Mock.Of<IEmailService>());
        var controller = new InviteController(authService, _db, entitlementResolver);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        return controller;
    }

    private async Task<Guid> SetupCompanyWithSubscriptionAndMemberAsync(string memberRole)
    {
        var tenantId = Guid.NewGuid();
        var planTemplateId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var planTemplate = new PlanTemplate
        {
            Id = planTemplateId,
            Name = "Free",
            Slug = "free",
            IsActive = true,
            MaxSeats = 5
        };

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            PlanTemplateId = planTemplateId,
            Status = SubscriptionState.Active
        };
        subscription.PlanTemplate = planTemplate;

        var tenant = new Tenant { Id = tenantId, Name = "Test Company", Plan = "Free" };

        var member = new User
        {
            Id = memberId,
            Email = $"{Guid.NewGuid()}@test.com",
            TenantId = tenantId,
            DisplayName = "Test User",
            Role = string.Empty
        };

        var membership = new UserCompanyMembership
        {
            UserId = memberId,
            CompanyId = tenantId,
            CompanyRole = memberRole,
            Status = "Active"
        };

        _db.Tenants.Add(tenant);
        _db.PlanTemplates.Add(planTemplate);
        _db.CompanySubscriptions.Add(subscription);
        _db.Users.Add(member);
        _db.UserCompanyMemberships.Add(membership);
        await _db.SaveChangesAsync();

        _tenantProvider.SetTenantId(tenantId);

        return memberId;
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-08: Role Ceiling Tests — CompanyAdmin callers
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CompanyAdmin_CannotInviteAsCompanyOwner_RoleCeiling()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyAdmin");
        var controller = CreateController("CompanyAdmin", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "CompanyOwner"));

        // Assert — F-08: CompanyAdmin(11) cannot assign CompanyOwner(10) which is higher privilege
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "CompanyAdmin (rank 11) should NOT be able to invite as CompanyOwner (rank 10)");
    }

    [Fact]
    public async Task CompanyAdmin_CanInviteAsCompanyAdmin()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyAdmin");
        var controller = CreateController("CompanyAdmin", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "CompanyAdmin"));

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            because: "CompanyAdmin can invite as CompanyAdmin (same rank)");
    }

    [Fact]
    public async Task CompanyAdmin_CanInviteAsOperator()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyAdmin");
        var controller = CreateController("CompanyAdmin", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "Operator"));

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            because: "CompanyAdmin (rank 11) can invite as Operator (rank 30, lower privilege)");
    }

    [Fact]
    public async Task CompanyAdmin_CanInviteAsViewer()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyAdmin");
        var controller = CreateController("CompanyAdmin", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "Viewer"));

        // Assert
        result.Should().BeOfType<OkObjectResult>(
            because: "CompanyAdmin (rank 11) can invite as Viewer (rank 31, lower privilege)");
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-08: Role Ceiling Tests — CompanyOwner callers
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CompanyOwner_CannotInviteAsSuperAdmin()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyOwner");
        var controller = CreateController("CompanyOwner", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "SuperAdmin"));

        // Assert — F-08: CompanyOwner cannot assign platform-level roles
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "CompanyOwner cannot invite as SuperAdmin (platform role, higher privilege)");
    }

    [Fact]
    public async Task CompanyOwner_CannotInviteAsCompanyOwner()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyOwner");
        var controller = CreateController("CompanyOwner", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "CompanyOwner"));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "CompanyOwner assignment is restricted to bootstrap or platform-only flows");
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-08: Role Ceiling Tests — SuperAdmin (platform) callers
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SuperAdmin_CanInviteAnyCompanyRole()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyOwner");

        // Elevate to SuperAdmin
        var superAdminUser = await _db.Users.FindAsync(callerId);
        superAdminUser!.Role = "SuperAdmin";
        await _db.SaveChangesAsync();

        var controller = CreateController("SuperAdmin", callerId, _tenantProvider.GetTenantId());

        // Act & Assert — SuperAdmin can invite any company role
        foreach (var targetRole in new[] { "CompanyAdmin", "DepartmentManager", "Operator", "Viewer" })
        {
            var dto = new SendInviteDto($"{Guid.NewGuid()}@test.com", targetRole);
            var result = await controller.SendInvite(dto);
            result.Should().BeOfType<OkObjectResult>(
                because: "SuperAdmin (platform role) bypasses company role ceiling");
        }

        var ownerInvite = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "CompanyOwner"));
        ownerInvite.Should().BeOfType<BadRequestObjectResult>(
            because: "CompanyOwner assignment is intentionally blocked in normal invite flows");
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-08: Authorization — Non-admin callers get Forbid
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Operator_CannotInvite_ForbidAtAdminCheck()
    {
        // Arrange — Operator is not CompanyAdminOrAbove, so gets Forbid
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("Operator");
        var controller = CreateController("Operator", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "Viewer"));

        // Assert — Operator is not an admin, so they can't invite at all
        result.Should().BeOfType<ForbidResult>(
            because: "Operator is not CompanyAdminOrAbove and cannot send invitations");
    }

    [Fact]
    public async Task DepartmentManager_CannotInvite_ForbidAtAdminCheck()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("DepartmentManager");
        var controller = CreateController("DepartmentManager", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "Viewer"));

        // Assert
        result.Should().BeOfType<ForbidResult>(
            because: "DepartmentManager is not CompanyAdminOrAbove and cannot send invitations");
    }

    // ═══════════════════════════════════════════════════════════════════
    // F-08: Invalid role validation
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Invite_WithInvalidRole_ReturnsBadRequest()
    {
        // Arrange
        var callerId = await SetupCompanyWithSubscriptionAndMemberAsync("CompanyOwner");
        var controller = CreateController("CompanyOwner", callerId, _tenantProvider.GetTenantId());

        // Act
        var result = await controller.SendInvite(new SendInviteDto($"{Guid.NewGuid()}@test.com", "NotARealRole"));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>(
            because: "Invalid roles should be rejected");
    }

    private class MockTenantProvider : ITenantProvider
    {
        private Guid _tenantId = Guid.Empty;
        public void SetTenantId(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
