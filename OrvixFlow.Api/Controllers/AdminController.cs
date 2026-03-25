using System;
using System.Linq;
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
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    // Parse at JWT boundary; use typed enum for checks
    private UserRole CurrentUserRole() =>
        UserRoleExtensions.ParseRole(HttpContext.User.FindFirst("Role")?.Value);

    private bool IsAdmin() => CurrentUserRole().IsCompanyAdminOrAbove();

    [HttpGet("metrics")]
    public async Task<IActionResult> GetGlobalMetrics()
    {
        if (!IsAdmin()) return Forbid();

        var totalTenants = await _db.Tenants.IgnoreQueryFilters().CountAsync();
        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync();
        var totalMemoryChunks = await _db.KnowledgeBases.IgnoreQueryFilters().CountAsync();
        
        // Count active subscriptions
        var premiumTenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Plan == "Starter" || t.Plan == "Pro" || t.Plan == "Enterprise")
            .CountAsync();

        return Ok(new
        {
            totalTenants,
            totalUsers,
            totalMemoryChunks,
            premiumTenants
        });
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        if (!IsAdmin()) return Forbid();

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.CreatedAt,
                UserCount = t.Users.Count
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(tenants);
    }
}
