using System;

namespace OrvixFlow.Core.Entities;

public class ActionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public Guid InboxEventId { get; set; }
    public InboxEvent? InboxEvent { get; set; }
    
    public string EvaluatedCategory { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public string DraftResponse { get; set; } = string.Empty;
    public string PolicyReason { get; set; } = string.Empty;
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    
    public string Status { get; set; } = "Pending";
    
    public uint RowVersion { get; set; }
}
