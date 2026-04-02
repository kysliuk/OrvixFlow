using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class ModuleDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Utility";
    public string Tier { get; set; } = "Utility";
    public string Visibility { get; set; } = "UserFacing";
    public bool IsOperational { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? IconKey { get; set; }
    public string? UpgradePromptText { get; set; }
    public int SortOrder { get; set; } = 0;

    public ICollection<ModuleAssignment> Assignments { get; set; } = new List<ModuleAssignment>();
    public ICollection<PlanModuleInclusion> PlanInclusions { get; set; } = new List<PlanModuleInclusion>();
}
