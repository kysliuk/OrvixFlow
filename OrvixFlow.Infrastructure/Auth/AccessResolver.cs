using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Auth;

public class AccessResolver : IAccessResolver
{
    private readonly AppDbContext _db;

    public AccessResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ModulePermissionResult> GetEffectivePermissionsAsync(Guid userId, Guid companyId, string moduleKey)
    {
        var userRole = await _db.UserCompanyMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active")
            .Select(m => m.CompanyRole)
            .FirstOrDefaultAsync();

        if (IsCompanyAdmin(userRole))
        {
            return new ModulePermissionResult(true, true, true, true, true, true, true, true);
        }

        var moduleId = await _db.ModuleDefinitions
            .Where(m => m.Key == moduleKey && m.IsActive)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();

        if (moduleId == Guid.Empty)
        {
            return Empty();
        }

        var departmentIds = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active")
            .Select(m => m.DepartmentId)
            .ToListAsync();

        var grants = await _db.ModulePermissionGrants
            .Where(g =>
                g.ModuleAssignment != null &&
                g.ModuleAssignment.ModuleDefinitionId == moduleId &&
                g.ModuleAssignment.CompanyId == companyId &&
                g.ModuleAssignment.IsEnabled &&
                (
                    (g.ModuleAssignment.Scope == "Company" && g.ModuleAssignment.DepartmentId == null && g.ModuleAssignment.UserId == null) ||
                    (g.ModuleAssignment.Scope == "Department" && g.ModuleAssignment.DepartmentId != null && departmentIds.Contains(g.ModuleAssignment.DepartmentId.Value)) ||
                    (g.ModuleAssignment.Scope == "User" && g.ModuleAssignment.UserId == userId)
                ))
            .ToListAsync();

        if (grants.Count == 0)
        {
            return Empty();
        }

        return new ModulePermissionResult(
            grants.Any(g => g.CanView),
            grants.Any(g => g.CanUse),
            grants.Any(g => g.CanTest),
            grants.Any(g => g.CanConfigure),
            grants.Any(g => g.CanManageIntegrations),
            grants.Any(g => g.CanManagePrompts),
            grants.Any(g => g.CanViewLogs),
            grants.Any(g => g.IsAdmin)
        );
    }

    public async Task<IReadOnlyList<string>> GetVisibleModulesAsync(Guid userId, Guid companyId)
    {
        var moduleKeys = await _db.ModuleDefinitions.Where(m => m.IsActive).Select(m => m.Key).ToListAsync();
        var visible = new List<string>();
        foreach (var key in moduleKeys)
        {
            var result = await GetEffectivePermissionsAsync(userId, companyId, key);
            if (result.CanView)
            {
                visible.Add(key);
            }
        }

        return visible;
    }

    private static bool IsCompanyAdmin(string? role)
    {
        return role == "Owner" || role == "CompanyAdmin" || role == "Admin" || role == "SuperAdmin";
    }

    private static ModulePermissionResult Empty()
    {
        return new ModulePermissionResult(false, false, false, false, false, false, false, false);
    }
}
