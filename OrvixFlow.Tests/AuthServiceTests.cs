using System;
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
using Xunit;

namespace OrvixFlow.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IEmailService> _emailServiceMock;

    public AuthServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _db = new AppDbContext(options, new MockTenantProvider(_tenantId));

        _db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Tenant", Plan = "Free", SubscriptionStatus = "Active" });
        _db.SaveChanges();
        
        _loggerMock = new Mock<ILogger<AuthService>>();
        _configMock = new Mock<IConfiguration>();
        _emailServiceMock = new Mock<IEmailService>();
        
        _configMock.Setup(c => c["Jwt:Secret"]).Returns("test-secret-key-for-testing-min-32-chars");
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        _configMock.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_ReturnNewTokens_When_GivenValidRefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        
        var user = new User
        {
            Id = userId,
            Email = "refresh@example.com",
            DisplayName = "Refresh User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);
        AddActiveMembership(userId, _tenantId, "CompanyOwner");

        var validToken = new RefreshToken
        {
            Token = "valid-refresh-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(validToken);
        await _db.SaveChangesAsync();

        var result = await authService.RefreshSessionAsync("valid-refresh-token");

        result.IsSuccess.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_Fail_When_RefreshTokenDoesNotExist()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);

        var result = await authService.RefreshSessionAsync("non-existent-token");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_Fail_When_RefreshTokenIsExpired()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        
        var user = new User
        {
            Id = userId,
            Email = "expired@example.com",
            DisplayName = "Expired User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);
        AddActiveMembership(userId, _tenantId, "CompanyOwner");

        var expiredToken = new RefreshToken
        {
            Token = "expired-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(expiredToken);
        await _db.SaveChangesAsync();

        var result = await authService.RefreshSessionAsync("expired-token");

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshSessionAsync_WithActiveCompanyId_RetainsCompanySwitch()
    {
        // BUG REPRODUCER: When a user switches companies, the refresh token should
        // preserve the active company context, not reset to the default tenant.
        
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        var switchedCompanyId = Guid.NewGuid();
        
        // Create secondary company
        _db.Tenants.Add(new Tenant { Id = switchedCompanyId, Name = "Second Company", Plan = "Free", SubscriptionStatus = "Active" });
        
        var user = new User
        {
            Id = userId,
            Email = "multicompany@example.com",
            DisplayName = "Multi Company User",
            TenantId = _tenantId // Default tenant
        };
        _db.Users.Add(user);
        AddActiveMembership(userId, _tenantId, "CompanyOwner");

        // Create membership to second company
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = switchedCompanyId,
            CompanyRole = "CompanyAdmin",
            Status = "Active"
        });

        var validToken = new RefreshToken
        {
            Token = "valid-refresh-token-company-switch",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(validToken);
        await _db.SaveChangesAsync();

        // Refresh with the switched company context
        var result = await authService.RefreshSessionAsync("valid-refresh-token-company-switch", switchedCompanyId);

        result.IsSuccess.Should().BeTrue();
        result.Profile.Should().NotBeNull();
        result.Profile!.ActiveCompanyId.Should().Be(switchedCompanyId, "Refresh should preserve the switched company context");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithInvalidActiveCompanyId_FallsBackToDefault()
    {
        // When the provided activeCompanyId is not in user's memberships,
        // should fall back to the default tenant.
        
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        var invalidCompanyId = Guid.NewGuid(); // Company user doesn't belong to
        
        var user = new User
        {
            Id = userId,
            Email = "invalidcompany@example.com",
            DisplayName = "Invalid Company User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);
        AddActiveMembership(userId, _tenantId, "CompanyOwner");

        var validToken = new RefreshToken
        {
            Token = "valid-refresh-token-invalid-company",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(validToken);
        await _db.SaveChangesAsync();

        // Refresh with invalid company context - should fallback
        var result = await authService.RefreshSessionAsync("valid-refresh-token-invalid-company", invalidCompanyId);

        result.IsSuccess.Should().BeTrue();
        result.Profile.Should().NotBeNull();
        result.Profile!.ActiveCompanyId.Should().Be(_tenantId, "Should fall back to default tenant when company is invalid");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithInactiveMembership_FallsBackToDefault()
    {
        // When the user's membership to the company is not Active,
        // should fall back to the default tenant.
        
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        var inactiveCompanyId = Guid.NewGuid();
        
        // Create company with inactive membership
        _db.Tenants.Add(new Tenant { Id = inactiveCompanyId, Name = "Inactive Company", Plan = "Free", SubscriptionStatus = "Active" });
        
        var user = new User
        {
            Id = userId,
            Email = "inactivemembership@example.com",
            DisplayName = "Inactive Membership User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);
        AddActiveMembership(userId, _tenantId, "CompanyOwner");

        // Create INACTIVE membership to second company
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = inactiveCompanyId,
            CompanyRole = "CompanyAdmin",
            Status = "Pending" // Inactive status
        });

        var validToken = new RefreshToken
        {
            Token = "valid-refresh-token-inactive-membership",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(validToken);
        await _db.SaveChangesAsync();

        // Refresh with inactive company context - should fallback
        var result = await authService.RefreshSessionAsync("valid-refresh-token-inactive-membership", inactiveCompanyId);

        result.IsSuccess.Should().BeTrue();
        result.Profile.Should().NotBeNull();
        result.Profile!.ActiveCompanyId.Should().Be(_tenantId, "Should fall back to default tenant when membership is inactive");
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_When_UserHasNoActiveMembership()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var password = "ValidPassword123!";

        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "nomembership@example.com",
            DisplayName = "No Membership User",
            TenantId = _tenantId,
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            EmailVerified = true
        });
        await _db.SaveChangesAsync();

        var result = await authService.LoginAsync("nomembership@example.com", password);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("active company membership");
    }

    [Fact]
    public async Task LogoutAsync_Should_Revoke_RefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var token = new RefreshToken
        {
            Token = "logout-token",
            UserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();

        await authService.LogoutAsync("logout-token");

        var storedToken = await _db.RefreshTokens.FirstAsync(r => r.Token == "logout-token");
        storedToken.RevokedAt.Should().NotBeNull();
    }

    private void AddActiveMembership(Guid userId, Guid companyId, string role)
    {
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = companyId,
            CompanyRole = role,
            Status = "Active"
        });
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
