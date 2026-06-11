using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class MailboxCredentialService : IMailboxCredentialService
{
    private readonly AppDbContext _dbContext;
    private readonly MailboxCredentialEncryptionService _encryptionService;
    private readonly ILogger<MailboxCredentialService> _logger;

    public MailboxCredentialService(
        AppDbContext dbContext,
        MailboxCredentialEncryptionService encryptionService,
        ILogger<MailboxCredentialService> logger)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<MailboxCredential> StoreCredentialAsync(
        Guid tenantId, 
        Guid mailboxConnectionId, 
        string provider, 
        string providerAccountId, 
        string accessToken, 
        string refreshToken, 
        IEnumerable<string> scopes, 
        DateTime expiresAtUtc)
    {
        var encryptedAccessToken = _encryptionService.Encrypt(accessToken);
        var encryptedRefreshToken = _encryptionService.Encrypt(refreshToken);
        var scopesJoined = string.Join(" ", scopes);

        var credential = new MailboxCredential
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            MailboxConnectionId = mailboxConnectionId,
            Provider = provider,
            ProviderAccountId = providerAccountId,
            EncryptedAccessToken = encryptedAccessToken,
            EncryptedRefreshToken = encryptedRefreshToken,
            Scopes = scopesJoined,
            TokenExpiresAtUtc = expiresAtUtc,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.MailboxCredentials.Add(credential);
        await _dbContext.SaveChangesAsync();

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == mailboxConnectionId);

        if (connection != null)
        {
            connection.CredentialId = credential.Id;
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("Stored secure mailbox credential {CredentialId} for connection {ConnectionId}", credential.Id, mailboxConnectionId);
        return credential;
    }

    public async Task<(string accessToken, string refreshToken)?> GetDecryptedTokensAsync(Guid credentialId)
    {
        var credential = await _dbContext.MailboxCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential == null)
        {
            _logger.LogWarning("Credential {CredentialId} not found.", credentialId);
            return null;
        }

        try
        {
            var accessToken = _encryptionService.Decrypt(credential.EncryptedAccessToken);
            var refreshToken = _encryptionService.Decrypt(credential.EncryptedRefreshToken);
            return (accessToken, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt tokens for credential {CredentialId}", credentialId);
            return null;
        }
    }

    public async Task UpdateTokensAsync(Guid credentialId, string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        var credential = await _dbContext.MailboxCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential == null)
        {
            throw new ArgumentException($"Credential with ID {credentialId} not found.", nameof(credentialId));
        }

        credential.EncryptedAccessToken = _encryptionService.Encrypt(accessToken);
        credential.EncryptedRefreshToken = _encryptionService.Encrypt(refreshToken);
        credential.TokenExpiresAtUtc = expiresAtUtc;
        credential.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Updated tokens for credential {CredentialId}", credentialId);
    }

    public async Task DeleteCredentialAsync(Guid credentialId)
    {
        var credential = await _dbContext.MailboxCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId);

        if (credential == null)
        {
            return;
        }

        var connections = await _dbContext.MailboxConnections
            .Where(c => c.CredentialId == credentialId)
            .ToListAsync();

        foreach (var connection in connections)
        {
            connection.CredentialId = null;
            connection.IsActive = false;
        }

        _dbContext.MailboxCredentials.Remove(credential);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Deleted secure mailbox credential {CredentialId}", credentialId);
    }
}
