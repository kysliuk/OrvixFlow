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

        var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(id);

        return Ok(new
        {
            entitlements.MaxSeats,
            entitlements.MaxMonthlyTokens,
            entitlements.MaxApiRequestsPerDay,
            entitlements.MaxStorageMb,
            entitlements.MaxKnowledgeBases,
            entitlements.MaxInboxMessagesPerMonth,
            entitlements.MaxMailboxConnections,
            entitlements.TokensUsedThisPeriod,
            entitlements.ApiRequestsUsedToday,
            entitlements.StorageUsedMb,
            entitlements.KnowledgeBasesCount,
            entitlements.HasEntitlementOverride,
            entitlements.OverrideNote
        });
    }

    // --- Entitlement Override Endpoints ---

    public class EntitlementOverrideRequest
    {
        public int? MaxSeats { get; set; }
        public int? MaxMonthlyTokens { get; set; }
        public int? MaxApiRequestsPerDay { get; set; }
        public int? MaxStorageMb { get; set; }
        public int? MaxKnowledgeBases { get; set; }
        public int? MaxInboxMessages { get; set; }
        public int? MaxMailboxConnections { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    [HttpGet("companies/{id}/entitlements")]
    public async Task<IActionResult> GetEntitlementOverride(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var overrideEntity = await _entitlementResolver.GetEntitlementOverrideAsync(id);
        if (overrideEntity == null)
        {
            return Ok(new { hasOverride = false });
        }

        return Ok(new
        {
            hasOverride = true,
            overrideEntity.Id,
            overrideEntity.MaxSeats,
            overrideEntity.MaxMonthlyTokens,
            overrideEntity.MaxApiRequestsPerDay,
            overrideEntity.MaxStorageMb,
            overrideEntity.MaxKnowledgeBases,
            overrideEntity.MaxInboxMessages,
            overrideEntity.MaxMailboxConnections,
            overrideEntity.Note,
            overrideEntity.CreatedAt,
            overrideEntity.UpdatedAt
        });
    }

    [HttpPut("companies/{id}/entitlements")]
    public async Task<IActionResult> SetEntitlementOverride(Guid id, [FromBody] EntitlementOverrideRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString());

        var existing = await _db.CompanyEntitlementOverrides
            .FirstOrDefaultAsync(o => o.CompanyId == id);

        if (existing != null)
        {
            existing.MaxSeats = request.MaxSeats;
            existing.MaxMonthlyTokens = request.MaxMonthlyTokens;
            existing.MaxApiRequestsPerDay = request.MaxApiRequestsPerDay;
            existing.MaxStorageMb = request.MaxStorageMb;
            existing.MaxKnowledgeBases = request.MaxKnowledgeBases;
            existing.MaxInboxMessages = request.MaxInboxMessages;
            existing.MaxMailboxConnections = request.MaxMailboxConnections;
            existing.Note = request.Note ?? string.Empty;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newOverride = new CompanyEntitlementOverride
            {
                Id = Guid.NewGuid(),
                CompanyId = id,
                MaxSeats = request.MaxSeats,
                MaxMonthlyTokens = request.MaxMonthlyTokens,
                MaxApiRequestsPerDay = request.MaxApiRequestsPerDay,
                MaxStorageMb = request.MaxStorageMb,
                MaxKnowledgeBases = request.MaxKnowledgeBases,
                MaxInboxMessages = request.MaxInboxMessages,
                MaxMailboxConnections = request.MaxMailboxConnections,
                Note = request.Note ?? string.Empty,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.CompanyEntitlementOverrides.Add(newOverride);
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Entitlement override saved" });
    }

    [HttpDelete("companies/{id}/entitlements")]
    public async Task<IActionResult> DeleteEntitlementOverride(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _db.CompanyEntitlementOverrides
            .FirstOrDefaultAsync(o => o.CompanyId == id);

        if (existing == null)
        {
            return NotFound(new { error = "No override found for this company" });
        }

        _db.CompanyEntitlementOverrides.Remove(existing);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Entitlement override removed, reverting to plan defaults" });
    }

    // --- Module Override Endpoints ---

    public class ModuleOverrideRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid ModuleDefinitionId { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string Note { get; set; } = string.Empty;
    }

    [HttpGet("companies/{id}/modules")]
    public async Task<IActionResult> GetModuleOverrides(Guid id)
    {
        if (!IsSuperAdmin()) return Forbid();

        var overrides = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .Where(o => o.CompanyId == id)
            .Include(o => o.ModuleDefinition)
            .Select(o => new
            {
                o.Id,
                o.ModuleDefinitionId,
                ModuleKey = o.ModuleDefinition.Key,
                ModuleName = o.ModuleDefinition.DisplayName,
                o.IsEnabled,
                o.Note,
                o.CreatedAt
            })
            .ToListAsync();

        return Ok(overrides);
    }

    [HttpPost("companies/{id}/modules")]
    public async Task<IActionResult> AddModuleOverride(Guid id, [FromBody] ModuleOverrideRequest request)
    {
        if (!IsSuperAdmin()) return Forbid();

        var module = await _db.ModuleDefinitions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == request.ModuleDefinitionId);

        if (module == null)
        {
            return NotFound(new { error = "Module not found" });
        }

        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString());

        var existing = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.CompanyId == id && o.ModuleDefinitionId == request.ModuleDefinitionId);

        if (existing != null)
        {
            existing.IsEnabled = request.IsEnabled;
            existing.Note = request.Note ?? string.Empty;
        }
        else
        {
            var newOverride = new CompanyModuleOverride
            {
                Id = Guid.NewGuid(),
                CompanyId = id,
                ModuleDefinitionId = request.ModuleDefinitionId,
                IsEnabled = request.IsEnabled,
                Note = request.Note ?? string.Empty,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _db.CompanyModuleOverrides.Add(newOverride);
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Module override {(request.IsEnabled ? "granted" : "suppressed")}" });
    }

    [HttpDelete("companies/{id}/modules/{moduleId}")]
    public async Task<IActionResult> RemoveModuleOverride(Guid id, Guid moduleId)
    {
        if (!IsSuperAdmin()) return Forbid();

        var existing = await _db.CompanyModuleOverrides
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.CompanyId == id && o.ModuleDefinitionId == moduleId);

        if (existing == null)
        {
            return NotFound(new { error = "Module override not found" });
        }

        _db.CompanyModuleOverrides.Remove(existing);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Module override removed, reverting to plan default" });
    }
}

public record AssignPlanRequest(Guid PlanTemplateId, string? BillingInterval);
