using System;

namespace OrvixFlow.Core.Entities;

public class CompanyModuleOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ModuleDefinitionId { get; set; }

    public bool IsEnabled { get; set; } = true;
    public string Note { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Company { get; set; } = null!;
    public ModuleDefinition ModuleDefinition { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
