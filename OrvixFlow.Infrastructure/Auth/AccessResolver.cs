using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Auth;

public class AccessResolver : IAccessResolver
{
    private readonly AppDbContext _db;
    private readonly IScopeContext _scope;
    private readonly IEntitlementResolver _entitlements;

    public AccessResolver(AppDbContext db, IScopeContext scope, IEntitlementResolver entitlements)
    {
        _db = db;
        _scope = scope;
        _entitlements = entitlements;
    }

    public async Task<ModulePermissionResult> GetEffectivePermissionsAsync(Guid userId, Guid companyId, string moduleKey)
    {
        // CRITICAL: Initialize scope context asynchronously to avoid thread starvation
        // This resolves data boundaries without blocking the thread.
        await _scope.InitializeAsync();

        // Check global role first — platform admins always have full access
        var user = await _db.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();

        var globalRole = UserRoleExtensions.ParseRole(user);
        if (globalRole.IsPlatformAdmin())
            return FullAccess();

        // Resolve company role and parse at boundary
        var userRoleString = await _db.UserCompanyMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active")
            .Select(m => m.CompanyRole)
            .FirstOrDefaultAsync();

        var role = UserRoleExtensions.ParseRole(userRoleString);

        // Company-admins and above have full access to all modules
        if (role.IsCompanyAdminOrAbove())
            return FullAccess();

        var moduleId = await _db.ModuleDefinitions
            .Where(m => m.Key == moduleKey && m.IsActive)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();

        if (moduleId == Guid.Empty)
            return Empty();

        // Scope: use IScopeContext to get allowed department IDs for this user
        // Now safely accessed after InitializeAsync() - no thread blocking!
        List<Guid> departmentIds;
        if (_scope.HasCompanyWideAccess)
        {
            // Should not reach here (caught above), but kept for safety
            return FullAccess();
        }
        else
        {
            departmentIds = _scope.AllowedDepartmentIds.ToList();
        }

        // Fetch grants for company-wide, department-scoped, or user-scoped assignments
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
            var canUse = await _entitlements.CanUseModuleWithOverridesAsync(companyId, moduleKey);
            if (canUse)
            {
                if (role is UserRole.Operator or UserRole.DepartmentManager)
                    return new ModulePermissionResult(true, true, false, false, false, false, false, false);
                if (role == UserRole.Viewer)
                    return new ModulePermissionResult(true, false, false, false, false, false, false, false);
            }
            return Empty();
        }

        // Union of grants (most permissive wins)
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
        var moduleKeys = await _db.ModuleDefinitions
            .Where(m => m.IsActive)
            .Select(m => m.Key)
            .ToListAsync();

        var visible = new List<string>();
        foreach (var key in moduleKeys)
        {
            var result = await GetEffectivePermissionsAsync(userId, companyId, key);
            if (result.CanView)
                visible.Add(key);
        }

        return visible;
    }

    private static ModulePermissionResult FullAccess() =>
        new(true, true, true, true, true, true, true, true);

    private static ModulePermissionResult Empty() =>
        new(false, false, false, false, false, false, false, false);
}
