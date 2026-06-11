using System;

namespace OrvixFlow.Core.Entities;

public class MailboxCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid MailboxConnectionId { get; set; }
    public string Provider { get; set; } = string.Empty;  // "Gmail" | "Microsoft"
    public string ProviderAccountId { get; set; } = string.Empty; // subject claim
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime TokenExpiresAtUtc { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public MailboxConnection MailboxConnection { get; set; } = null!;
}
