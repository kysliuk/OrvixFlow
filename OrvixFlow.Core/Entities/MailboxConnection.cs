using System;

namespace OrvixFlow.Core.Entities;

public class MailboxConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    
    public string? N8nWorkflowId { get; set; }
    public string? N8nCredentialId { get; set; }
    public Guid? CredentialId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConnectedAtUtc { get; set; }
}
