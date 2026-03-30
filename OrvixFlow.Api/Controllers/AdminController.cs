using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICompanySubscriptionService _subscriptionService;
    private readonly IEntitlementResolver _entitlementResolver;

    public AdminController(
        AppDbContext db, 
        ICompanySubscriptionService subscriptionService,
        IEntitlementResolver entitlementResolver)
    {
        _db = db;
        _subscriptionService = subscriptionService;
        _entitlementResolver = entitlementResolver;
    }

    private UserRole CurrentUserRole() =>
        UserRoleExtensions.ParseRole(HttpContext.User.FindFirst("Role")?.Value);

    private bool IsSuperAdmin() => CurrentUserRole() == UserRole.SuperAdmin;
    private bool IsAdmin() => CurrentUserRole().IsCompanyAdminOrAbove();

    [HttpGet("metrics")]
    public async Task<IActionResult> GetGlobalMetrics()
    {
        if (!IsSuperAdmin()) return Forbid();

        var totalTenants = await _db.Tenants.IgnoreQueryFilters().CountAsync();
        var totalUsers = await _db.Users.IgnoreQueryFilters().CountAsync();
        var totalMemoryChunks = await _db.KnowledgeBases.IgnoreQueryFilters().CountAsync();
        
        var premiumTenants = await _db.Tenants.IgnoreQueryFilters()
            .Where(t => t.Plan == "Starter" || t.Plan == "Pro" || t.Plan == "Enterprise")
            .CountAsync();

        var activeSubscriptions = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .CountAsync(s => s.Status == "Active" || s.Status == "Trialing");

        return Ok(new
        {
            totalTenants,
            totalUsers,
            totalMemoryChunks,
            premiumTenants,
            activeSubscriptions
        });
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants()
    {
        if (!IsSuperAdmin()) return Forbid();

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

    [HttpGet("companies")]
    public async Task<IActionResult> ListCompanies()
    {
        if (!IsSuperAdmin()) return Forbid();

        var companies = await _db.Tenants
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.CreatedAt,
                UserCount = t.Users.Count,
                HasSubscription = _db.CompanySubscriptions
                    .IgnoreQueryFilters()
                    .Any(s => s.CompanyId == t.Id)
            })
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(companies);
    }

    [HttpGet("companies/{id}")]
    public async Task<IActionResult> GetCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var company = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Plan,
                t.SubscriptionStatus,
                t.CreatedAt,
                t.WebhookSecret,
                UserCount = t.Users.Count,
                Members = t.Users.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (company == null) return NotFound();

        var subscription = await _subscriptionService.GetSubscriptionAsync(id);
        var entitlements = await _entitlementResolver.GetEntitlementsAsync(id);

        return Ok(new
        {
            company,
            subscription = subscription != null ? new
            {
                subscription.Id,
                subscription.Status,
                subscription.BillingInterval,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.TrialEndsAt,
                Plan = subscription.PlanTemplate != null ? new
                {
                    subscription.PlanTemplate.Id,
                    subscription.PlanTemplate.Name,
                    subscription.PlanTemplate.Slug,
                    subscription.PlanTemplate.MonthlyPriceCents,
                    subscription.PlanTemplate.MaxSeats
                } : null
            } : null,
            entitlements
        });
    }

    [HttpPut("companies/{id}/plan")]
    public async Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignPlanRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            var subscription = await _subscriptionService.AssignPlanAsync(id, request.PlanTemplateId, request.BillingInterval);
            return Ok(new
            {
                subscription.Id,
                subscription.Status,
                subscription.PlanTemplateId
            });
        }
        catch (PlanNotFoundException)
        {
            return NotFound(new { error = "Plan not found" });
        }
        catch (PlanNotActiveException)
        {
            return BadRequest(new { error = "Plan is not active" });
        }
        catch (SeatLimitExceededException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("companies/{id}/suspend")]
    public async Task<IActionResult> SuspendCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _subscriptionService.SuspendSubscriptionAsync(id);
            return Ok(new { message = "Company suspended" });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
    }

    [HttpPost("companies/{id}/reactivate")]
    public async Task<IActionResult> ReactivateCompany(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        try
        {
            await _subscriptionService.ReactivateSubscriptionAsync(id);
            return Ok(new { message = "Company reactivated" });
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found" });
        }
    }

    [HttpGet("companies/{id}/usage")]
    public async Task<IActionResult> GetCompanyUsage(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var entitlements = await _entitlementResolver.GetEntitlementsAsync(id);

        return Ok(new
        {
            entitlements.MaxSeats,
            entitlements.MaxMonthlyTokens,
            entitlements.MaxApiRequestsPerDay,
            entitlements.MaxStorageMb,
            entitlements.MaxKnowledgeBases,
            entitlements.TokensUsedThisPeriod,
            entitlements.ApiRequestsUsedToday,
            entitlements.StorageUsedMb,
            entitlements.KnowledgeBasesCount
        });
    }
}

public record AssignPlanRequest(Guid PlanTemplateId, string? BillingInterval);
