using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IAccessResolver
{
    Task<ModulePermissionResult> GetEffectivePermissionsAsync(Guid userId, Guid companyId, string moduleKey);
    Task<IReadOnlyList<string>> GetVisibleModulesAsync(Guid userId, Guid companyId);
}

public record ModulePermissionResult(
    bool CanView,
    bool CanUse,
    bool CanTest,
    bool CanConfigure,
    bool CanManageIntegrations,
    bool CanManagePrompts,
    bool CanViewLogs,
    bool IsAdmin
);
