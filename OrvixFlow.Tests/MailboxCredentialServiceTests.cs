using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class MailboxCredentialServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly MailboxCredentialService _service;
    private readonly Guid _tenantId;
    private readonly MailboxCredentialEncryptionService _encryptionService;
    private readonly DbContextOptions<AppDbContext> _dbOptions;

    public MailboxCredentialServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        
        var tenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(_dbOptions, tenantProvider);

        byte[] keyBytes = new byte[32];
        RandomNumberGenerator.Fill(keyBytes);
        var base64Key = Convert.ToBase64String(keyBytes);

        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["MAILBOX_CREDENTIAL_ENCRYPTION_KEY"]).Returns(base64Key);

        _encryptionService = new MailboxCredentialEncryptionService(mockConfig.Object);
        var logger = new Mock<ILogger<MailboxCredentialService>>().Object;

        _service = new MailboxCredentialService(_dbContext, _encryptionService, logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task StoreAndRetrieve_DecryptedTokensMatchOriginals()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var accessToken = "access_123456";
        var refreshToken = "refresh_789012";
        var scopes = new[] { "https://mail.google.com/", "email" };
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var credential = await _service.StoreCredentialAsync(
            _tenantId,
            connection.Id,
            "Gmail",
            "google_account_sub_123",
            accessToken,
            refreshToken,
            scopes,
            expiresAt);

        credential.Should().NotBeNull();
        credential.EncryptedAccessToken.Should().NotBe(accessToken);
        credential.EncryptedRefreshToken.Should().NotBe(refreshToken);

        var decrypted = await _service.GetDecryptedTokensAsync(credential.Id);
        decrypted.Should().NotBeNull();
        decrypted!.Value.accessToken.Should().Be(accessToken);
        decrypted!.Value.refreshToken.Should().Be(refreshToken);

        var updatedConnection = await _dbContext.MailboxConnections.FindAsync(connection.Id);
        updatedConnection.Should().NotBeNull();
        updatedConnection!.CredentialId.Should().Be(credential.Id);
    }

    [Fact]
    public async Task UpdateTokens_UpdatesCorrectlyAndEncrypts()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@outlook.com",
            Provider = "Microsoft",
            IsActive = false
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var credential = await _service.StoreCredentialAsync(
            _tenantId,
            connection.Id,
            "Microsoft",
            "microsoft_account_sub_123",
            "access_old",
            "refresh_old",
            new[] { "email" },
            DateTime.UtcNow.AddHours(1));

        await _service.UpdateTokensAsync(credential.Id, "access_new", "refresh_new", DateTime.UtcNow.AddHours(2));

        var decrypted = await _service.GetDecryptedTokensAsync(credential.Id);
        decrypted.Should().NotBeNull();
        decrypted!.Value.accessToken.Should().Be("access_new");
        decrypted!.Value.refreshToken.Should().Be("refresh_new");

        _dbContext.Entry(credential).State = EntityState.Detached;
        var dbCred = await _dbContext.MailboxCredentials.FindAsync(credential.Id);
        dbCred.Should().NotBeNull();
        dbCred!.EncryptedAccessToken.Should().NotBe("access_new");
    }

    [Fact]
    public async Task DeleteCredential_RemovesFromDbAndClearsLink()
    {
        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            EmailAddress = "test@gmail.com",
            Provider = "Gmail",
            IsActive = true
        };
        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        var credential = await _service.StoreCredentialAsync(
            _tenantId,
            connection.Id,
            "Gmail",
            "google_account_sub_123",
            "access_123",
            "refresh_123",
            new[] { "email" },
            DateTime.UtcNow.AddHours(1));

        await _service.DeleteCredentialAsync(credential.Id);

        var dbCred = await _dbContext.MailboxCredentials.FindAsync(credential.Id);
        dbCred.Should().BeNull();

        var dbConnection = await _dbContext.MailboxConnections.FindAsync(connection.Id);
        dbConnection.Should().NotBeNull();
        dbConnection!.CredentialId.Should().BeNull();
        dbConnection!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task TenantIsolation_EnforcesTenantFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var dbContextA = new AppDbContext(options, new MockTenantProvider(tenantA));
        var dbContextB = new AppDbContext(options, new MockTenantProvider(tenantB));

        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA,
            UserId = Guid.NewGuid(),
            EmailAddress = "tenantA@gmail.com",
            Provider = "Gmail",
            IsActive = true
        };
        dbContextA.MailboxConnections.Add(connection);
        await dbContextA.SaveChangesAsync();

        var serviceA = new MailboxCredentialService(dbContextA, _encryptionService, new Mock<ILogger<MailboxCredentialService>>().Object);
        var serviceB = new MailboxCredentialService(dbContextB, _encryptionService, new Mock<ILogger<MailboxCredentialService>>().Object);

        var credential = await serviceA.StoreCredentialAsync(
            tenantA,
            connection.Id,
            "Gmail",
            "google_account_sub_123",
            "access_123",
            "refresh_123",
            new[] { "email" },
            DateTime.UtcNow.AddHours(1));

        var retrievedA = await serviceA.GetDecryptedTokensAsync(credential.Id);
        retrievedA.Should().NotBeNull();

        var retrievedB = await serviceB.GetDecryptedTokensAsync(credential.Id);
        retrievedB.Should().BeNull();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
