using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeamController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetTeamMembers()
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        if (callerRole.IsCompanyAdminOrAbove())
        {
            var members = await GetMembersByUserIdsAsync(companyId.Value, null);
            return Ok(members);
        }

        var callerManagedDeptIds = await GetManagedDepartmentIdsAsync(callerUserId.Value, companyId.Value);
        if (callerManagedDeptIds.Count == 0)
            return Forbid();

        var visibleUserIds = await _db.UserDepartmentMemberships
            .Where(m => m.CompanyId == companyId.Value
                     && m.Status == "Active"
                     && callerManagedDeptIds.Contains(m.DepartmentId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();

        var scopedMembers = await GetMembersByUserIdsAsync(companyId.Value, visibleUserIds, callerManagedDeptIds);
        return Ok(scopedMembers);
    }

    [HttpPut("{userId}/role")]
    public async Task<IActionResult> UpdateRole(Guid userId, [FromBody] UpdateRoleDto dto)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        if (callerUserId.Value == userId)
            return BadRequest(new { error = "You cannot change your own company role." });

        var newRole = UserRoleExtensions.ParseRole(dto.NewRole);
        if (!newRole.IsCompanyScopedRole() || !UserRoleExtensions.CompanyRoleNames.Contains(dto.NewRole))
            return BadRequest(new { error = "Invalid role specified." });

        var membership = await _db.UserCompanyMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId.Value && m.Status == "Active");

        if (membership == null)
            return NotFound(new { error = "User is not an active member of this company." });

        var targetRole = UserRoleExtensions.ParseRole(membership.CompanyRole);
        if (!callerRole.CanManageCompanyTarget(targetRole))
            return StatusCode(403, new { error = "Cannot modify a user with equal or higher company authority." });

        if (!callerRole.CanAssignCompanyRole(newRole))
            return StatusCode(403, new { error = "Cannot assign the requested company role." });

        if (newRole == UserRole.CompanyOwner)
            return BadRequest(new { error = "CompanyOwner assignment is restricted to company bootstrap or platform-only flows." });

        membership.CompanyRole = newRole.ToClaimValue();
        await _db.SaveChangesAsync();

        return Ok(new { message = "Role updated successfully." });
    }

    [HttpDelete("{userId}")]
    public async Task<IActionResult> RemoveMember(Guid userId)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        if (callerUserId.Value == userId)
            return BadRequest(new { error = "You cannot remove yourself from the active company." });

        var membership = await _db.UserCompanyMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId.Value && m.Status == "Active");

        if (membership == null)
            return NotFound(new { error = "User is not an active member of this company." });

        var targetRole = UserRoleExtensions.ParseRole(membership.CompanyRole);
        if (!callerRole.CanManageCompanyTarget(targetRole))
            return StatusCode(403, new { error = "Cannot remove a user with equal or higher company authority." });

        if (targetRole == UserRole.CompanyOwner)
        {
            var ownerCount = await _db.UserCompanyMemberships
                .CountAsync(m => m.CompanyId == companyId.Value && m.Status == "Active" && m.CompanyRole == UserRole.CompanyOwner.ToClaimValue());

            if (ownerCount <= 1)
                return BadRequest(new { error = "Cannot remove the last CompanyOwner from the company." });
        }

        membership.Status = "Inactive";

        var departmentMemberships = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId.Value && m.Status == "Active")
            .ToListAsync();

        foreach (var departmentMembership in departmentMemberships)
            departmentMembership.Status = "Inactive";

        await _db.SaveChangesAsync();

        return Ok(new { message = "Member removed successfully." });
    }

    [HttpPut("{userId}/departments")]
    public async Task<IActionResult> UpdateDepartments(Guid userId, [FromBody] UpdateDepartmentsDto dto)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        var membership = await _db.UserCompanyMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId.Value && m.Status == "Active");

        if (membership == null)
            return NotFound(new { error = "User is not an active member of this company." });

        var targetRole = UserRoleExtensions.ParseRole(membership.CompanyRole);
        if (!callerRole.CanManageCompanyTarget(targetRole) && callerUserId.Value != userId)
            return StatusCode(403, new { error = "Cannot modify departments for a user with equal or higher company authority." });

        var requestedDepartmentIds = (dto.DepartmentIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var validDepartmentIds = await _db.Departments
            .Where(d => d.CompanyId == companyId.Value && requestedDepartmentIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync();

        if (validDepartmentIds.Count != requestedDepartmentIds.Count)
            return BadRequest(new { error = "One or more departments do not belong to the active company." });

        var existingMemberships = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId.Value)
            .ToListAsync();

        foreach (var existingMembership in existingMemberships)
        {
            existingMembership.Status = requestedDepartmentIds.Contains(existingMembership.DepartmentId)
                ? "Active"
                : "Inactive";

            if (existingMembership.Status == "Active" && string.IsNullOrWhiteSpace(existingMembership.DepartmentRole))
                existingMembership.DepartmentRole = UserRole.DepartmentOperator.ToDepartmentRoleValue();
        }

        var existingDepartmentIds = existingMemberships
            .Select(m => m.DepartmentId)
            .ToHashSet();

        foreach (var departmentId in requestedDepartmentIds.Where(id => !existingDepartmentIds.Contains(id)))
        {
            _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
            {
                UserId = userId,
                CompanyId = companyId.Value,
                DepartmentId = departmentId,
                DepartmentRole = UserRole.DepartmentOperator.ToDepartmentRoleValue(),
                Status = "Active"
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Department memberships updated successfully.", departmentIds = requestedDepartmentIds });
    }

    [HttpPut("{userId}/department-role")]
    public async Task<IActionResult> UpdateDepartmentRole(Guid userId, [FromBody] UpdateDepartmentRoleDto dto)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        var newDepartmentRole = UserRoleExtensions.ParseDeptRole(dto.NewDepartmentRole);
        if (!newDepartmentRole.IsDepartmentScopedRole())
            return BadRequest(new { error = "Invalid department role specified." });

        var departmentExists = await _db.Departments
            .AnyAsync(d => d.Id == dto.DepartmentId && d.CompanyId == companyId.Value);
        if (!departmentExists)
            return BadRequest(new { error = "Invalid department specified for this company." });

        if (!callerRole.IsCompanyAdminOrAbove())
        {
            var managedDepartmentIds = await GetManagedDepartmentIdsAsync(callerUserId.Value, companyId.Value);
            if (!managedDepartmentIds.Contains(dto.DepartmentId))
                return Forbid();

            if (newDepartmentRole == UserRole.DepartmentManager)
                return StatusCode(403, new { error = "Department managers cannot assign the DepartmentManager role." });
        }

        var targetMembership = await _db.UserDepartmentMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId
                                   && m.CompanyId == companyId.Value
                                   && m.DepartmentId == dto.DepartmentId
                                   && m.Status == "Active");

        if (targetMembership == null)
            return NotFound(new { error = "User is not assigned to the requested department." });

        if (!callerRole.IsCompanyAdminOrAbove()
            && (targetMembership.DepartmentRole == "DepartmentManager" || targetMembership.DepartmentRole == "Manager"))
        {
            return StatusCode(403, new { error = "Department managers cannot change another department manager's role." });
        }

        targetMembership.DepartmentRole = newDepartmentRole.ToDepartmentRoleValue();
        await _db.SaveChangesAsync();

        return Ok(new { message = "Department role updated successfully." });
    }

    [HttpPost("{userId}/departments/{departmentId}")]
    public async Task<IActionResult> AddUserToDepartment(Guid userId, Guid departmentId, [FromBody] AddUserToDepartmentDto dto)
    {
        var authorizationResult = await AuthorizeDepartmentManagementAsync(departmentId);
        if (authorizationResult != null)
            return authorizationResult;

        var companyId = GetActiveCompanyId()!.Value;
        var targetCompanyMembership = await _db.UserCompanyMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active");
        if (targetCompanyMembership == null)
            return NotFound(new { error = "User is not an active member of this company." });

        var departmentRole = UserRoleExtensions.ParseDeptRole(dto.DepartmentRole).ToDepartmentRoleValue();
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove() && departmentRole == UserRole.DepartmentManager.ToDepartmentRoleValue())
            return StatusCode(403, new { error = "Department managers cannot assign the DepartmentManager role." });

        var departmentMembership = await _db.UserDepartmentMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId && m.DepartmentId == departmentId);

        if (departmentMembership == null)
        {
            _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
            {
                UserId = userId,
                CompanyId = companyId,
                DepartmentId = departmentId,
                DepartmentRole = departmentRole,
                Status = "Active"
            });
        }
        else
        {
            departmentMembership.DepartmentRole = departmentRole;
            departmentMembership.Status = "Active";
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "User assigned to department successfully." });
    }

    [HttpDelete("{userId}/departments/{departmentId}")]
    public async Task<IActionResult> RemoveUserFromDepartment(Guid userId, Guid departmentId)
    {
        var authorizationResult = await AuthorizeDepartmentManagementAsync(departmentId);
        if (authorizationResult != null)
            return authorizationResult;

        var companyId = GetActiveCompanyId()!.Value;
        var departmentMembership = await _db.UserDepartmentMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId && m.DepartmentId == departmentId && m.Status == "Active");

        if (departmentMembership == null)
            return NotFound(new { error = "User is not assigned to the requested department." });

        departmentMembership.Status = "Inactive";
        await _db.SaveChangesAsync();
        return Ok(new { message = "User removed from department successfully." });
    }

    private async Task<List<object>> GetMembersByUserIdsAsync(Guid companyId, IReadOnlyCollection<Guid>? userIds, IReadOnlyCollection<Guid>? visibleDepartmentIds = null)
    {
        var query = _db.UserCompanyMemberships
            .Include(m => m.User)
            .Where(m => m.CompanyId == companyId && m.Status == "Active");

        if (userIds != null)
            query = query.Where(m => userIds.Contains(m.UserId));

        return await query
            .Select(m => new
            {
                m.UserId,
                m.User.Email,
                m.User.DisplayName,
                CompanyRole = m.CompanyRole,
                m.JoinedAt,
                DepartmentIds = _db.UserDepartmentMemberships
                    .Where(d => d.UserId == m.UserId
                             && d.CompanyId == companyId
                             && d.Status == "Active"
                             && (visibleDepartmentIds == null || visibleDepartmentIds.Contains(d.DepartmentId)))
                    .Select(d => d.DepartmentId)
                    .ToList(),
                Departments = _db.UserDepartmentMemberships
                    .Where(d => d.UserId == m.UserId
                             && d.CompanyId == companyId
                             && d.Status == "Active"
                             && (visibleDepartmentIds == null || visibleDepartmentIds.Contains(d.DepartmentId)))
                    .Select(d => new { d.DepartmentId, d.DepartmentRole })
                    .ToList()
            })
            .Cast<object>()
            .ToListAsync();
    }

    private async Task<IActionResult?> AuthorizeDepartmentManagementAsync(Guid departmentId)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        var companyId = GetActiveCompanyId();
        var callerUserId = GetCurrentUserId();
        if (companyId == null || callerUserId == null)
            return Unauthorized();

        var departmentExists = await _db.Departments.AnyAsync(d => d.Id == departmentId && d.CompanyId == companyId.Value);
        if (!departmentExists)
            return BadRequest(new { error = "Invalid department specified for this company." });

        if (callerRole.IsCompanyAdminOrAbove())
            return null;

        var managedDepartmentIds = await GetManagedDepartmentIdsAsync(callerUserId.Value, companyId.Value);
        return managedDepartmentIds.Contains(departmentId) ? null : Forbid();
    }

    private async Task<List<Guid>> GetManagedDepartmentIdsAsync(Guid userId, Guid companyId)
    {
        return await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId
                     && m.CompanyId == companyId
                     && m.Status == "Active"
                     && (m.DepartmentRole == "DepartmentManager" || m.DepartmentRole == "Manager"))
            .Select(m => m.DepartmentId)
            .Distinct()
            .ToListAsync();
    }

    private Guid? GetActiveCompanyId()
    {
        var claimValue = User.FindFirst("ActiveCompanyId")?.Value ?? User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(claimValue, out var companyId) ? companyId : null;
    }

    private Guid? GetCurrentUserId()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}

public record UpdateRoleDto(string NewRole);
public record UpdateDepartmentsDto(IReadOnlyList<Guid>? DepartmentIds);
public record UpdateDepartmentRoleDto(Guid DepartmentId, string NewDepartmentRole);
public record AddUserToDepartmentDto(string DepartmentRole);
