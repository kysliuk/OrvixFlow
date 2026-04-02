using System;

namespace OrvixFlow.Core.Entities;

public class AuditTrail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public string DecisionDetails { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid? ActorUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? OverrideEntityId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
