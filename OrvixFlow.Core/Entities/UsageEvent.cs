using System;

namespace OrvixFlow.Core.Entities;

public class UsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? UserId { get; set; }
    public string ModuleKey { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty; // ai-tokens | n8n-nodes
    public decimal Quantity { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
