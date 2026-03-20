using System;

namespace OrvixFlow.Core.Entities;

public class AuditTrail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string DecisionDetails { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
