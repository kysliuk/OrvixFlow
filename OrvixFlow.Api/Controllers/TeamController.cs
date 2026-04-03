using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
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
        // Only SuperAdmin or CompanyOwner can elevate someone to CompanyAdmin/CompanyOwner
        // CompanyAdmin can update subordinate roles.
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        if (!Guid.TryParse(User.FindFirst("ActiveCompanyId")?.Value
                           ?? User.FindFirst("TenantId")?.Value, out var companyId))
            return Unauthorized();

        var newRole = UserRoleExtensions.ParseRole(dto.NewRole);
        if (!UserRoleExtensions.AllRoles.Contains(newRole))
            return BadRequest(new { error = "Invalid role specified." });

        if (newRole >= callerRole)
            return Forbid("Cannot assign a role equal to or higher than your own.");

        var membership = await _db.UserCompanyMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId && m.Status == "Active");

        if (membership == null)
            return NotFound(new { error = "User is not an active member of this company." });

        var targetRole = UserRoleExtensions.ParseRole(membership.CompanyRole);
        if (targetRole >= callerRole)
            return Forbid("Cannot modify the role of a user with a role equal to or higher than your own.");

        membership.CompanyRole = newRole.ToClaimValue();

        await _db.SaveChangesAsync();

        return Ok(new { message = "Role updated successfully." });
    }
}

public record UpdateRoleDto(string NewRole);
