using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/modules")]
[Authorize]
public class ModulesController : ControllerBase
{
    private readonly IAccessResolver _accessResolver;
    private readonly AppDbContext _db;

    public ModulesController(IAccessResolver accessResolver, AppDbContext db)
    {
        _accessResolver = accessResolver;
        _db = db;
    }

    [HttpGet("visible")]
    public async Task<IActionResult> GetVisible()
    {
        var userId = ParseGuid("sub");
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (userId == null || companyId == null)
        {
            return Unauthorized();
        }

        var modules = await _accessResolver.GetVisibleModulesAsync(userId.Value, companyId.Value);
        return Ok(new { modules });
    }

    [HttpGet("{moduleKey}/permissions")]
    public async Task<IActionResult> GetPermissions(string moduleKey)
    {
        var userId = ParseGuid("sub");
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (userId == null || companyId == null)
        {
            return Unauthorized();
        }

        var permissions = await _accessResolver.GetEffectivePermissionsAsync(userId.Value, companyId.Value, moduleKey);
        if (!permissions.CanView)
        {
            return NotFound();
        }

        return Ok(permissions);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignModuleRequest req)
    {
        var roleString = User.FindFirst("Role")?.Value;
        var role = UserRoleExtensions.ParseRole(roleString);
        if (!role.IsCompanyAdminOrAbove())
        {
            return Forbid();
        }

        var module = await _db.ModuleDefinitions.FirstOrDefaultAsync(m => m.Key == req.ModuleKey && m.IsActive);
        if (module == null)
        {
            return NotFound(new { error = "Unknown module." });
        }

        var scope = req.Scope ?? "Department";
        var assignment = new Core.Entities.ModuleAssignment
        {
            CompanyId = req.CompanyId,
            DepartmentId = req.DepartmentId,
            UserId = req.UserId,
            Scope = scope,
            ModuleDefinitionId = module.Id,
            IsEnabled = true
        };
        _db.ModuleAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        _db.ModulePermissionGrants.Add(new Core.Entities.ModulePermissionGrant
        {
            ModuleAssignmentId = assignment.Id,
            CanView = req.CanView,
            CanUse = req.CanUse,
            CanTest = req.CanTest,
            CanConfigure = req.CanConfigure,
            CanManageIntegrations = req.CanManageIntegrations,
            CanManagePrompts = req.CanManagePrompts,
            CanViewLogs = req.CanViewLogs,
            IsAdmin = req.IsAdmin
        });

        await _db.SaveChangesAsync();
        return Ok(new { assignmentId = assignment.Id });
    }

    private Guid? ParseGuid(string claimType)
    {
        var value = User.FindFirst(claimType)?.Value;
        if (value == null && claimType == "sub")
        {
            value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public record AssignModuleRequest(
    Guid CompanyId,
    string ModuleKey,
    string? Scope,
    Guid? DepartmentId,
    Guid? UserId,
    bool CanView,
    bool CanUse,
    bool CanTest,
    bool CanConfigure,
    bool CanManageIntegrations,
    bool CanManagePrompts,
    bool CanViewLogs,
    bool IsAdmin
);
