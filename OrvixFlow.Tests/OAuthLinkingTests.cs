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

/// <summary>
/// Tests for F-02: OAuth email linking security vulnerability
/// </summary>
public class OAuthLinkingTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly AppDbContext _db;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public OAuthLinkingTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _db = new AppDbContext(options, new MockTenantProvider(_tenantId));
        
        _loggerMock = new Mock<ILogger<AuthService>>();
        _configMock = new Mock<IConfiguration>();
        
        _configMock.Setup(c => c["Jwt:Secret"]).Returns("test-secret-key-for-testing-min-32-chars");
        _configMock.Setup(c => c["Jwt:Issuer"]).Returns("test-issuer");
        _configMock.Setup(c => c["Jwt:Audience"]).Returns("test-audience");
    }

    [Fact]
    public async Task ProvisionOAuthUserAsync_Should_Reject_When_Email_Exists_With_Different_Provider()
    {
        // Arrange: A local user exists with email "user@example.com"
        var localUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "Local User",
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
            TenantId = _tenantId
        };
        
        _db.Users.Add(localUser);
        await _db.SaveChangesAsync();

        // Verify user exists
        var userCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "user@example.com");
        userCount.Should().Be(1);

        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);

        // Act: Attacker tries to sign in with Google using the same email
        var result = await authService.ProvisionOAuthUserAsync(
            "user@example.com",  // exact match
            "Attacker",
            "google",
            "google-external-id-123"
        );

        // Assert: F-02 FIX - Should reject
        result.IsSuccess.Should().BeFalse("{Error}", result.Error);
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task ProvisionOAuthUserAsync_Should_Allow_Linking_When_Same_Provider()
    {
        // Arrange: User exists with Google OAuth
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "Google User",
            OAuthProvider = "google",
            ExternalId = "existing-google-id",
            TenantId = _tenantId
        };
        
        _db.Users.Add(existingUser);
        await _db.SaveChangesAsync();

        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);

        // Act: User signs in again with same Google account
        var result = await authService.ProvisionOAuthUserAsync(
            "user@example.com",
            "Google User",
            "google",
            "existing-google-id"
        );

        // Assert: Should succeed - same provider, same external ID
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionOAuthUserAsync_Should_Create_New_User_When_Email_Not_Found()
    {
        var authService = new AuthService(_db, _configMock.Object, _loggerMock.Object);

        // Act: Brand new user signs up with Google
        var result = await authService.ProvisionOAuthUserAsync(
            "newuser@example.com",
            "New User",
            "google",
            "new-google-id"
        );

        // Assert: Should create new user and tenant
        result.IsSuccess.Should().BeTrue();
        result.Profile.Should().NotBeNull();
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
