using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;

using Microsoft.EntityFrameworkCore;
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
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        if (!Guid.TryParse(User.FindFirst("ActiveCompanyId")?.Value
                           ?? User.FindFirst("TenantId")?.Value, out var companyId))
            return Unauthorized();

        var invites = await _db.Invitations
            .Where(i => i.CompanyId == companyId && i.Status == "Pending")
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new {
                i.Id,
                i.Email,
                i.AssignedRole,
                i.DepartmentId,
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
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        if (!Guid.TryParse(User.FindFirst("ActiveCompanyId")?.Value
                           ?? User.FindFirst("TenantId")?.Value, out var companyId))
            return Unauthorized();

        var invite = await _db.Invitations
            .FirstOrDefaultAsync(i => i.Id == id && i.CompanyId == companyId && i.Status == "Pending");

        if (invite == null)
            return NotFound(new { error = "Pending invitation not found." });

        invite.Status = "Revoked";
        await _db.SaveChangesAsync();

        return Ok(new { message = "Invitation revoked successfully." });
    }

    // ── POST api/invite  ──────────────────────────────────────────────────────
    // Only CompanyAdmin or above can send invitations.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SendInvite([FromBody] SendInviteDto dto)
    {
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!callerRole.IsCompanyAdminOrAbove())
            return Forbid();

        if (!Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("sub")?.Value, out var callerId))
            return Unauthorized();

        if (!Guid.TryParse(User.FindFirst("ActiveCompanyId")?.Value
                           ?? User.FindFirst("TenantId")?.Value, out var companyId))
            return Unauthorized();

        var currentMemberCount = await _db.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");

        var canInvite = await _entitlementResolver.CanInviteUserAsync(companyId, currentMemberCount);
        if (!canInvite)
        {
            return BadRequest(new { error = "Seat limit exceeded. Please upgrade your plan to add more members." });
        }

        var result = await _auth.InviteUserAsync(new InviteRequest(
            InvitedByUserId: callerId,
            CompanyId:       companyId,
            Email:           dto.Email,
            AssignedRole:    dto.AssignedRole,
            DepartmentId:    dto.DepartmentId
        ));

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        // In production this token would be e-mailed. For now return it directly
        // so the frontend/tests can act immediately.
        return Ok(new { token = result.Token, message = "Invitation created." });
    }

    // ── POST api/invite/accept  ───────────────────────────────────────────────
    // Public endpoint — called when the user clicks the invitation link.
    [HttpPost("accept")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteDto dto)
    {
        var result = await _auth.AcceptInvitationAsync(dto.Token, dto.DisplayName, dto.Password);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error });

        return Ok(new { token = result.Token, profile = result.Profile });
    }
}

public record SendInviteDto(string Email, string AssignedRole, Guid? DepartmentId = null);
public record AcceptInviteDto(string Token, string? DisplayName = null, string? Password = null);
