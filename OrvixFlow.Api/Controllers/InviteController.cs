using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InviteController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AppDbContext _db;
    private readonly IEntitlementResolver _entitlementResolver;

    public InviteController(IAuthService auth, AppDbContext db, IEntitlementResolver entitlementResolver)
    {
        _auth = auth;
        _db = db;
        _entitlementResolver = entitlementResolver;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetPendingInvites()
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var companyId = GetActiveCompanyId();
        var callerId = GetCurrentUserId();
        if (companyId == null || callerId == null)
            return Unauthorized();

        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        var query = _db.Invitations
            .Where(i => i.CompanyId == companyId.Value && i.Status == "Pending");

        if (!callerRole.IsCompanyAdminOrAbove())
        {
            var managedDepartmentIds = await GetManagedDepartmentIdsAsync(callerId.Value, companyId.Value);
            if (managedDepartmentIds.Count == 0)
                return Forbid();

            query = query.Where(i => i.DepartmentId.HasValue && managedDepartmentIds.Contains(i.DepartmentId.Value));
        }

        var invites = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Email,
                i.AssignedRole,
                i.DepartmentId,
                i.InvitedDepartmentRole,
                i.CreatedAt,
                i.ExpiresAt
            })
            .ToListAsync();

        return Ok(invites);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> RevokeInvite(Guid id)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var companyId = GetActiveCompanyId();
        var callerId = GetCurrentUserId();
        if (companyId == null || callerId == null)
            return Unauthorized();

        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        var invite = await _db.Invitations
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId.Value && i.Status == "Pending");

        if (invite == null)
            return NotFound(new { error = "Pending invitation not found." });

        if (!callerRole.IsCompanyAdminOrAbove())
        {
            if (!invite.DepartmentId.HasValue)
                return Forbid();

            var managedDepartmentIds = await GetManagedDepartmentIdsAsync(callerId.Value, companyId.Value);
            if (!managedDepartmentIds.Contains(invite.DepartmentId.Value))
                return Forbid();
        }

        invite.Status = "Revoked";
        await _db.SaveChangesAsync();

        return Ok(new { message = "Invitation revoked successfully." });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendInvite([FromBody] SendInviteDto dto)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var callerId = GetCurrentUserId();
        var companyId = GetActiveCompanyId();
        if (callerId == null || companyId == null)
            return Unauthorized();

        if (!callerRole.IsCompanyMemberOrAbove())
            return Forbid();

        if (dto.DepartmentId.HasValue)
        {
            var departmentExists = await _db.Departments
                .AnyAsync(d => d.Id == dto.DepartmentId.Value && d.CompanyId == companyId.Value);

            if (!departmentExists)
                return BadRequest(new { error = "Invalid department specified for this company." });
        }

        string assignedRole;
        string? invitedDepartmentRole = null;

        if (callerRole.IsCompanyAdminOrAbove())
        {
            var targetRole = UserRoleExtensions.ParseRole(dto.AssignedRole);
            if (!targetRole.IsCompanyScopedRole() || !UserRoleExtensions.CompanyRoleNames.Contains(dto.AssignedRole))
                return BadRequest(new { error = $"Invalid role: {dto.AssignedRole}" });

            if (!callerRole.CanAssignCompanyRole(targetRole))
                return BadRequest(new { error = "Cannot assign the requested company role." });

            assignedRole = targetRole.ToClaimValue();
            if (!string.IsNullOrWhiteSpace(dto.InvitedDepartmentRole))
            {
                var parsedDepartmentRole = UserRoleExtensions.ParseDeptRole(dto.InvitedDepartmentRole);
                if (!parsedDepartmentRole.IsDepartmentScopedRole())
                    return BadRequest(new { error = $"Invalid department role: {dto.InvitedDepartmentRole}" });

                invitedDepartmentRole = parsedDepartmentRole.ToDepartmentRoleValue();
            }
        }
        else
        {
            if (!dto.DepartmentId.HasValue)
                return BadRequest(new { error = "DepartmentManager must specify a department." });

            if (string.IsNullOrWhiteSpace(dto.InvitedDepartmentRole))
                return BadRequest(new { error = "DepartmentManager must specify a department role." });

            var managedDepartmentIds = await GetManagedDepartmentIdsAsync(callerId.Value, companyId.Value);
            if (!managedDepartmentIds.Contains(dto.DepartmentId.Value))
                return Forbid();

            var parsedDepartmentRole = UserRoleExtensions.ParseDeptRole(dto.InvitedDepartmentRole);
            if (!parsedDepartmentRole.IsDepartmentScopedRole())
                return BadRequest(new { error = $"Invalid department role: {dto.InvitedDepartmentRole}" });

            assignedRole = UserRole.CompanyMember.ToClaimValue();
            invitedDepartmentRole = parsedDepartmentRole.ToDepartmentRoleValue();
        }

        var currentMemberCount = await _db.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId.Value && m.Status == "Active");

        var canInvite = await _entitlementResolver.CanInviteUserAsync(companyId.Value, currentMemberCount);
        if (!canInvite)
            return BadRequest(new { error = "Seat limit exceeded. Please upgrade your plan to add more members." });

        var result = await _auth.InviteUserAsync(new InviteRequest(
            InvitedByUserId: callerId.Value,
            CompanyId: companyId.Value,
            Email: dto.Email,
            AssignedRole: assignedRole,
            DepartmentId: dto.DepartmentId,
            InvitedDepartmentRole: invitedDepartmentRole
        ));

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { id = result.InvitationId, message = $"Invitation sent to {dto.Email}." });
    }

    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteDto dto)
    {
        var result = await _auth.AcceptInvitationAsync(dto.Token, dto.DisplayName, dto.Password);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken });
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

    private async Task<System.Collections.Generic.List<Guid>> GetManagedDepartmentIdsAsync(Guid userId, Guid companyId)
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
}

public record SendInviteDto(string Email, string AssignedRole, Guid? DepartmentId = null, string? InvitedDepartmentRole = null);
public record AcceptInviteDto(string Token, string? DisplayName = null, string? Password = null);
