using System;

namespace OrvixFlow.Core.Entities;

public class DraftFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ActionRequestId { get; set; }
    
    public string OriginalDraft { get; set; } = string.Empty;
    public string FinalHumanDraft { get; set; } = string.Empty;
    public decimal EditDistance { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
