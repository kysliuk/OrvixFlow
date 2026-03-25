using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Core.Authorization;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] int limit = 100)
    {
        var role = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        if (!role.IsCompanyAdminOrAbove()) return Forbid();

        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var logs = await _db.AuditTrails
            .Where(a => a.TenantId == companyId.Value)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .Select(a => new
            {
                id = a.Id,
                action = a.Action,
                details = a.DecisionDetails,
                timestamp = a.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }

    private Guid? ParseGuid(string claimType)
    {
        var value = User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
