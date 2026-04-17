using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Auth;

/// <summary>
/// HTTP-request-scoped implementation of <see cref="IScopeContext"/>.
///
/// Reads the authenticated user's JWT claims and resolves their effective
/// data-access scope from the database.
///
/// Pattern: parse boundary strings to enums immediately; use typed enum everywhere.
/// </summary>
public sealed class ScopeContext : IScopeContext
{
    private readonly Lazy<Task<(bool companyWide, List<Guid> deptIds)>> _lazyScope;

    public Guid UserId { get; }
    public Guid CompanyId { get; }

    // Populated on first access via lazy resolution
    private bool _hasCompanyWideAccess;
    private IReadOnlyList<Guid> _allowedDepartmentIds = Array.Empty<Guid>();
    private bool _resolved;
    private readonly UserRole _role;

    public bool HasCompanyWideAccess
    {
        get
        {
            // Fallback for synchronous access (e.g., legacy code that hasn't been migrated yet)
            // This path exists for backward compatibility but should be avoided in new code.
            EnsureResolved();
            return _hasCompanyWideAccess;
        }
    }

    public IReadOnlyList<Guid> AllowedDepartmentIds
    {
        get
        {
            // Fallback for synchronous access (e.g., legacy code that hasn't been migrated yet)
            // This path exists for backward compatibility but should be avoided in new code.
            EnsureResolved();
            return _allowedDepartmentIds;
        }
    }

    public ScopeContext(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        var user = httpContextAccessor.HttpContext?.User;

        Guid.TryParse(user?.FindFirst("sub")?.Value
                      ?? user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                      out var userId);
        UserId = userId;

        Guid.TryParse(user?.FindFirst("ActiveCompanyId")?.Value
                      ?? user?.FindFirst("TenantId")?.Value,
                      out var companyId);
        CompanyId = companyId;

        // Parse role at the string-boundary
        var roleString = user?.FindFirst("Role")?.Value;
        _role = UserRoleExtensions.ParseRole(roleString);

        _lazyScope = new Lazy<Task<(bool, List<Guid>)>>(() =>
            ResolveAsync(_role, userId, companyId, db));
    }

    /// <summary>
    /// Initializes the scope context by resolving data boundaries asynchronously.
    /// This MUST be called before accessing <see cref="HasCompanyWideAccess"/> or <see cref="AllowedDepartmentIds"/>
    /// from an async context to avoid thread starvation from sync-over-async.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_resolved) return;

        var result = await _lazyScope.Value;
        _hasCompanyWideAccess = result.companyWide;
        _allowedDepartmentIds = result.deptIds;
        _resolved = true;
    }

    private static async Task<(bool companyWide, List<Guid> deptIds)> ResolveAsync(
        UserRole role, Guid userId, Guid companyId, AppDbContext db)
    {
        // Platform admins and company-level roles see all company data
        if (role.IsCompanyAdminOrAbove())
            return (true, new List<Guid>());

        // DepartmentManagers and below: only their assigned department IDs
        var deptIds = await db.UserDepartmentMemberships
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId
                        && m.CompanyId == companyId
                        && m.Status == "Active")
            .Select(m => m.DepartmentId)
            .ToListAsync();

        return (false, deptIds);
    }

    // Synchronous accessor — resolves async scope via .GetAwaiter().GetResult()
    // DEPRECATED: This method exists for backward compatibility only.
    // New code should use InitializeAsync() instead to avoid thread starvation.
    private void EnsureResolved()
    {
        if (_resolved) return;
        var result = _lazyScope.Value.GetAwaiter().GetResult();
        _hasCompanyWideAccess = result.companyWide;
        _allowedDepartmentIds = result.deptIds;
        _resolved = true;
    }
}
