using System;

namespace OrvixFlow.Core.Entities;

public class ModulePermissionGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModuleAssignmentId { get; set; }
    public bool CanView { get; set; }
    public bool CanUse { get; set; }
    public bool CanTest { get; set; }
    public bool CanConfigure { get; set; }
    public bool CanManageIntegrations { get; set; }
    public bool CanManagePrompts { get; set; }
    public bool CanViewLogs { get; set; }
    public bool IsAdmin { get; set; }

    public ModuleAssignment? ModuleAssignment { get; set; }
}
