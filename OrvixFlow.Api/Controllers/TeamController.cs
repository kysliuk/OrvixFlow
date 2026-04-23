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

    // ── GET api/team  ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetTeamMembers()
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        if (!Guid.TryParse(User.FindFirst("ActiveCompanyId")?.Value
                           ?? User.FindFirst("TenantId")?.Value, out var companyId))
            return Unauthorized();

        var members = await _db.UserCompanyMemberships
            .Include(m => m.User)
            .Where(m => m.CompanyId == companyId && m.Status == "Active")
            .Select(m => new {
                m.UserId,
                m.User.Email,
                m.User.DisplayName,
                m.CompanyRole,
                m.JoinedAt,
                // Fetch department IDs for this user in this company
                DepartmentIds = _db.UserDepartmentMemberships
                    .Where(d => d.UserId == m.UserId && d.CompanyId == companyId && d.Status == "Active")
                    .Select(d => d.DepartmentId)
                    .ToList()
            })
            .ToListAsync();

        return Ok(members);
    }

    // ── PUT api/team/{userId}/role  ───────────────────────────────────────────
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

        var departmentMemberships = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == userId && m.CompanyId == companyId.Value && m.Status == "Active")
            .ToListAsync();

        foreach (var departmentMembership in departmentMemberships)
            departmentMembership.DepartmentRole = newRole.ToDepartmentRoleValue();

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

            if (existingMembership.Status == "Active")
                existingMembership.DepartmentRole = targetRole.ToDepartmentRoleValue();
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
                DepartmentRole = targetRole.ToDepartmentRoleValue(),
                Status = "Active"
            });
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Department memberships updated successfully.", departmentIds = requestedDepartmentIds });
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
