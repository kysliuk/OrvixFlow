using System;

namespace OrvixFlow.Core.Entities;

public class PlanModuleInclusion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanTemplateId { get; set; }
    public Guid ModuleDefinitionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? MaxUsagePerMonth { get; set; }
    public int? MaxItemsTotal { get; set; }
    public string? LimitDescription { get; set; }

    public PlanTemplate PlanTemplate { get; set; } = null!;
    public ModuleDefinition ModuleDefinition { get; set; } = null!;
}
