using System;

namespace OrvixFlow.Core.Entities;

public class WorkflowLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "Pending";
    public int TokenUsage { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
