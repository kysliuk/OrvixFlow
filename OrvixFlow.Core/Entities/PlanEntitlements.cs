using System;

namespace OrvixFlow.Core.Entities;

public class PlanEntitlements
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanTemplateId { get; set; }
    public int MaxMonthlyTokens { get; set; } = 100000;
    public int MaxApiRequestsPerDay { get; set; } = 1000;
    public int MaxStorageMb { get; set; } = 500;
    public int MaxKnowledgeBases { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlanTemplate PlanTemplate { get; set; } = null!;
}
