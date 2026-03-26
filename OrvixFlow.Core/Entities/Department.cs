using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Company { get; set; }
    public ICollection<UserDepartmentMembership> UserMemberships { get; set; } = new List<UserDepartmentMembership>();
    public ICollection<ModuleAssignment> ModuleAssignments { get; set; } = new List<ModuleAssignment>();
}
