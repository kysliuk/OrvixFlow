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
        
        _configMock.Setup(c => c["Jwt:Secret"]).Returns("test-secret-key-for-testing-min-32-chars");
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        _configMock.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_ReturnNewTokens_When_GivenValidRefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);
        var userId = Guid.NewGuid();
        
        var user = new User
        {
            Id = userId,
            Email = "refresh@example.com",
            DisplayName = "Refresh User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);

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
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe("valid-refresh-token", "the refresh token should be rotated");

        var oldTokenInDb = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == "valid-refresh-token");
        oldTokenInDb!.RevokedAt.Should().NotBeNull("the old token should be revoked after use");
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_ReturnError_When_GivenExpiredRefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);
        var userId = Guid.NewGuid();
        
        var user = new User
        {
            Id = userId,
            Email = "expired@example.com",
            DisplayName = "Expired User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);

        var expiredToken = new RefreshToken
        {
            Token = "expired-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            RevokedAt = null
        };
        _db.RefreshTokens.Add(expiredToken);
        await _db.SaveChangesAsync();

        var result = await authService.RefreshSessionAsync("expired-token");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired", "the error message should mention token expiration");
    }

    [Fact]
    public async Task RefreshSessionAsync_Should_ReturnError_When_GivenRevokedRefreshToken()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);
        var userId = Guid.NewGuid();
        
        var user = new User
        {
            Id = userId,
            Email = "revoked@example.com",
            DisplayName = "Revoked User",
            TenantId = _tenantId
        };
        _db.Users.Add(user);

        var revokedToken = new RefreshToken
        {
            Token = "revoked-token",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        _db.RefreshTokens.Add(revokedToken);
        await _db.SaveChangesAsync();

        var result = await authService.RefreshSessionAsync("revoked-token");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("revoked", "the error message should mention revocation");
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
