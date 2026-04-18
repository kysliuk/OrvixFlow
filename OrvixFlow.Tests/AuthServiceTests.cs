using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    [Fact]
    public async Task RefreshSessionAsync_Should_Validate_HashedRefreshToken_AndRotate()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var userId = Guid.NewGuid();
        var rawToken = CreateStructuredRefreshToken("lookup123");

        _db.Users.Add(new User
        {
            Id = userId,
            Email = "hashed-refresh@example.com",
            DisplayName = "Hashed Refresh User",
            TenantId = _tenantId
        });
        AddActiveMembership(userId, _tenantId, "CompanyOwner");
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            LookupKey = "lookup123",
            Token = ComputeTokenHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        var result = await authService.RefreshSessionAsync(rawToken);

        result.IsSuccess.Should().BeTrue();
        result.RefreshToken.Should().Contain(".");

        var revokedToken = await _db.RefreshTokens.FirstAsync(r => r.LookupKey == "lookup123");
        revokedToken.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LogoutAsync_Should_Revoke_HashedRefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        var rawToken = CreateStructuredRefreshToken("logoutlookup");

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = Guid.NewGuid(),
            LookupKey = "logoutlookup",
            Token = ComputeTokenHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        await authService.LogoutAsync(rawToken);

        var storedToken = await _db.RefreshTokens.FirstAsync(r => r.LookupKey == "logoutlookup");
        storedToken.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_Should_QueueVerificationEmail_And_StoreHashedToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        _configMock.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:3000");

        var result = await authService.RegisterAsync("queued@example.com", "ValidPassword123!", "Queued User");

        result.IsSuccess.Should().BeTrue();

        var user = await _db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "queued@example.com");
        user.VerificationToken.Should().NotBeNullOrWhiteSpace();
        user.VerificationTokenExpiresAt.Should().NotBeNull();

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.RecipientEmail.Should().Be("queued@example.com");
        notification.Subject.Should().Be("Verify your OrvixFlow account");
        notification.Body.Should().Contain("/verify?token=");
    }

    [Fact]
    public async Task VerifyEmailAsync_Should_Fail_When_TokenExpired()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        const string rawToken = "expired-verification-token";

        _db.Users.Add(new User
        {
            Email = "verify-expired@example.com",
            DisplayName = "Expired Verify User",
            TenantId = _tenantId,
            OAuthProvider = "local",
            VerificationToken = ComputeTokenHash(rawToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            EmailVerified = false,
        });
        await _db.SaveChangesAsync();

        var result = await authService.VerifyEmailAsync(rawToken);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public async Task InviteUserAsync_Should_QueueInvitationEmail_And_StoreHashedToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        _configMock.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:3000");
        var invitedByUserId = Guid.NewGuid();
        AddActiveMembership(invitedByUserId, _tenantId, "CompanyOwner");
        await _db.SaveChangesAsync();

        var result = await authService.InviteUserAsync(new InviteRequest(
            invitedByUserId,
            _tenantId,
            "invitee@example.com",
            "Operator",
            null));

        result.IsSuccess.Should().BeTrue();
        result.Token.Should().NotBeNullOrWhiteSpace();

        var invitation = await _db.Invitations.IgnoreQueryFilters().SingleAsync();
        invitation.Token.Should().Be(ComputeTokenHash(result.Token!));

        var notification = await _db.NotificationQueues.IgnoreQueryFilters()
            .OrderByDescending(n => n.CreatedAt)
            .FirstAsync();
        notification.RecipientEmail.Should().Be("invitee@example.com");
        notification.Subject.Should().Be("You're invited to join OrvixFlow");
        notification.Body.Should().Contain(result.Token!);
    }

    [Fact]
    public async Task AcceptInvitationAsync_Should_RequirePassword_For_NewLocalUser()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        const string rawToken = "invite-password-required";

        _db.Invitations.Add(new Invitation
        {
            Email = "newinvite@example.com",
            CompanyId = _tenantId,
            AssignedRole = "Operator",
            Token = ComputeTokenHash(rawToken),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            InvitedByUserId = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();

        var result = await authService.AcceptInvitationAsync(rawToken, "New Invitee", null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("password is required");
    }

    [Fact]
    public async Task AcceptInvitationAsync_Should_Verify_NewUser_Email_When_InviteAccepted()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object, _emailServiceMock.Object);
        const string rawToken = "invite-verifies-user";

        _db.Invitations.Add(new Invitation
        {
            Email = "verifiedinvite@example.com",
            CompanyId = _tenantId,
            AssignedRole = "Operator",
            Token = ComputeTokenHash(rawToken),
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            InvitedByUserId = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();

        var result = await authService.AcceptInvitationAsync(rawToken, "Verified Invitee", "ValidPassword123!");

        result.IsSuccess.Should().BeTrue();
        var user = await _db.Users.IgnoreQueryFilters().FirstAsync(u => u.Email == "verifiedinvite@example.com");
        user.EmailVerified.Should().BeTrue();
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

    private static string ComputeTokenHash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string CreateStructuredRefreshToken(string lookupKey)
    {
        return $"{lookupKey}.refresh-secret-value";
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
