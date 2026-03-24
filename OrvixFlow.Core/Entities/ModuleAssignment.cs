using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class ModuleAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ModuleDefinitionId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? UserId { get; set; }
    public string Scope { get; set; } = "Company";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Company { get; set; }
    public ModuleDefinition? ModuleDefinition { get; set; }
    public Department? Department { get; set; }
    public User? User { get; set; }
    public ICollection<ModulePermissionGrant> PermissionGrants { get; set; } = new List<ModulePermissionGrant>();
}
