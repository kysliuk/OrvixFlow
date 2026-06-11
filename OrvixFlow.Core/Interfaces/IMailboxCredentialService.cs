using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface IMailboxCredentialService
{
    Task<MailboxCredential> StoreCredentialAsync(
        Guid tenantId, 
        Guid mailboxConnectionId, 
        string provider, 
        string providerAccountId, 
        string accessToken, 
        string refreshToken, 
        IEnumerable<string> scopes, 
        DateTime expiresAtUtc);

    Task<(string accessToken, string refreshToken)?> GetDecryptedTokensAsync(Guid credentialId);

    Task UpdateTokensAsync(Guid credentialId, string accessToken, string refreshToken, DateTime expiresAtUtc);

    Task DeleteCredentialAsync(Guid credentialId);
}
