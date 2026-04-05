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
[Route("api/billing")]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEntitlementResolver _entitlementResolver;
    private readonly ICompanySubscriptionService _subscriptionService;
    private readonly IPlanService _planService;

    public BillingController(
        AppDbContext db, 
        IEntitlementResolver entitlementResolver,
        ICompanySubscriptionService subscriptionService,
        IPlanService planService)
    {
        _db = db;
        _entitlementResolver = entitlementResolver;
        _subscriptionService = subscriptionService;
        _planService = planService;
    }

    private UserRole CurrentUserRole() =>
        UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);

    private bool IsCompanyAdminOrAbove() =>
        CurrentUserRole().IsCompanyAdminOrAbove();

    [HttpPost("usage")]
    public async Task<IActionResult> TrackUsage([FromBody] TrackUsageRequest req)
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var role = User.FindFirst("Role")?.Value;
        if (!Roles.IsElevated(role))
        {
            return Forbid();
        }

        _db.UsageEvents.Add(new Core.Entities.UsageEvent
        {
            CompanyId = companyId.Value,
            DepartmentId = req.DepartmentId,
            UserId = ParseGuid("sub"),
            ModuleKey = req.ModuleKey,
            MetricType = req.MetricType,
            Quantity = req.Quantity,
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage()
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var company = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == companyId);
        if (company == null) return NotFound(new { error = "Company not found." });

        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        
        var used = await _db.UsageEvents
            .Where(e => e.CompanyId == companyId.Value && e.OccurredAt >= startOfMonth)
            .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

        int limit = company.Plan?.ToLowerInvariant() switch
        {
            "free" => 50000,
            "trialing" => 50000,
            "starter" => 1000000,
            "pro" => 1000000,
            "enterprise" => 10000000,
            _ => 50000
        };

        var renewalDate = startOfMonth.AddMonths(1);

        return Ok(new
        {
            used = used,
            limit = limit,
            plan = company.Plan ?? "Free",
            renewalDate = renewalDate
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var role = User.FindFirst("Role")?.Value;
        if (!Roles.IsAdmin(role))
        {
            return Forbid();
        }

        var start = from ?? DateTime.UtcNow.AddDays(-30);
        var end = to ?? DateTime.UtcNow;
        var summary = await _db.UsageEvents
            .Where(e => e.CompanyId == companyId && e.OccurredAt >= start && e.OccurredAt <= end)
            .GroupBy(e => new { e.ModuleKey, e.MetricType })
            .Select(g => new { g.Key.ModuleKey, g.Key.MetricType, quantity = g.Sum(x => x.Quantity) })
            .ToListAsync();
        return Ok(summary);
    }

    [AllowAnonymous]
    [HttpPost("stripe/webhook")]
    public async Task<IActionResult> StripeWebhook([FromBody] StripeWebhookRequest req)
    {
        // Minimal webhook sink for subscription sync. Signature validation should be added for production.
        var company = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == req.CompanyId);
        if (company == null)
        {
            return NotFound();
        }

        company.Plan = req.Plan;
        company.SubscriptionStatus = req.Status;

        var subscription = await _db.BillingSubscriptions.FirstOrDefaultAsync(s => s.CompanyId == req.CompanyId);
        if (subscription == null)
        {
            subscription = new Core.Entities.BillingSubscription
            {
                CompanyId = req.CompanyId
            };
            _db.BillingSubscriptions.Add(subscription);
        }

        subscription.StripeCustomerId = req.StripeCustomerId;
        subscription.StripeSubscriptionId = req.StripeSubscriptionId;
        subscription.CurrentPlan = req.Plan;
        subscription.Status = req.Status;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var subscription = await _entitlementResolver.GetSubscriptionAsync(companyId.Value);
        var entitlements = await _entitlementResolver.GetEntitlementsAsync(companyId.Value);

        var memberCount = await _db.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId.Value && m.Status == "Active");

        var kbCount = await _db.KnowledgeBases
            .CountAsync(k => k.TenantId == companyId.Value);

        var billingHistory = subscription?.PlanTemplate != null ? new[]
        {
            new {
                id = "inv_" + Guid.NewGuid().ToString("N")[..8],
                amount = subscription.PlanTemplate.MonthlyPriceCents,
                date = subscription.CurrentPeriodStart.ToString("o"),
                description = $"{subscription.PlanTemplate.Name} Plan - {subscription.BillingInterval}",
                status = "Paid"
            }
        } : Array.Empty<object>();

        return Ok(new
        {
            plan = subscription?.PlanTemplate != null ? new
            {
                name = subscription.PlanTemplate.Name,
                price = subscription.PlanTemplate.MonthlyPriceCents,
                interval = subscription.PlanTemplate.BillingInterval
            } : null,
            status = subscription?.Status ?? "Active",
            currentPeriodEnd = subscription?.CurrentPeriodEnd != default ? subscription.CurrentPeriodEnd.ToString("o") : null,
            pendingPlanId = subscription?.PendingPlanId,
            pendingChangeAt = subscription?.PendingChangeAt?.ToString("o"),
            entitlements = new
            {
                maxSeats = entitlements.MaxSeats,
                usedSeats = memberCount,
                maxMonthlyTokens = entitlements.MaxMonthlyTokens,
                usedTokens = entitlements.TokensUsedThisPeriod,
                maxStorageMb = entitlements.MaxStorageMb,
                usedStorageMb = entitlements.StorageUsedMb,
                maxKnowledgeBases = entitlements.MaxKnowledgeBases,
                usedKnowledgeBases = kbCount
            },
            billingHistory = billingHistory
        });
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetAvailablePlans()
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId.Value);
        var currentPlanId = subscription?.PlanTemplateId;
        var currentPlanName = subscription?.PlanTemplate?.Name ?? "Free";
        var currentPriceCents = subscription?.PlanTemplate?.MonthlyPriceCents ?? 0;

        var plans = await _planService.GetActivePlansAsync();
        var planDtos = plans.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            slug = p.Slug,
            description = p.Description,
            monthlyPriceCents = p.MonthlyPriceCents,
            yearlyPriceCents = p.YearlyPriceCents,
            billingInterval = p.BillingInterval,
            maxSeats = p.MaxSeats,
            isFree = p.IsFree,
            isUpgrade = p.MonthlyPriceCents > currentPriceCents,
            isDowngrade = p.MonthlyPriceCents < currentPriceCents,
            isCurrentPlan = p.Id == (currentPlanId ?? Guid.Empty),
            entitlements = new
            {
                maxMonthlyTokens = p.Entitlements?.MaxMonthlyTokens ?? 50000,
                maxApiRequestsPerDay = p.Entitlements?.MaxApiRequestsPerDay ?? 500,
                maxStorageMb = p.Entitlements?.MaxStorageMb ?? 100,
                maxKnowledgeBases = p.Entitlements?.MaxKnowledgeBases ?? 1
            }
        }).ToList();

        return Ok(new
        {
            currentPlanId = currentPlanId,
            currentPlanName = currentPlanName,
            plans = planDtos
        });
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest req)
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        if (!IsCompanyAdminOrAbove())
        {
            return Forbid();
        }

        try
        {
            var subscription = await _subscriptionService.ChangePlanAsync(
                companyId.Value, 
                req.PlanTemplateId, 
                req.Immediate);

            var pendingChangeAt = subscription.PendingChangeAt?.ToString("o");

            return Ok(new
            {
                success = true,
                message = req.Immediate 
                    ? $"Plan changed successfully to {subscription.PlanTemplate?.Name}"
                    : $"Plan change scheduled for {pendingChangeAt}. You will be charged on your next billing cycle.",
                pendingChangeAt = pendingChangeAt
            });
        }
        catch (SeatLimitExceededException ex)
        {
            return BadRequest(new
            {
                error = "SEAT_LIMIT_EXCEEDED",
                message = $"Cannot change to this plan: {ex.CurrentSeats} seats exceed the plan limit of {ex.MaxSeats}"
            });
        }
        catch (PlanNotFoundException)
        {
            return NotFound(new { error = "Plan not found" });
        }
        catch (PlanNotActiveException)
        {
            return BadRequest(new { error = "Plan is not available" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("proration")]
    public async Task<IActionResult> CalculateProration([FromQuery] Guid newPlanId)
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var currentSubscription = await _subscriptionService.GetSubscriptionAsync(companyId.Value);
        var newPlan = await _planService.GetPlanByIdAsync(newPlanId);

        if (newPlan == null)
        {
            return NotFound(new { error = "Plan not found" });
        }

        var currentPrice = currentSubscription?.PlanTemplate?.MonthlyPriceCents ?? 0;
        var newPrice = newPlan.MonthlyPriceCents;
        var daysRemaining = 0;

        if (currentSubscription?.CurrentPeriodEnd != default)
        {
            daysRemaining = (int)(currentSubscription.CurrentPeriodEnd - DateTime.UtcNow).TotalDays;
        }

        var prorationAmount = Math.Max(0, newPrice - currentPrice);

        return Ok(new
        {
            currentPrice = currentPrice,
            newPrice = newPrice,
            prorationAmount = prorationAmount,
            daysRemaining = daysRemaining,
            message = "Proration will be calculated when Stripe is integrated"
        });
    }

    [HttpGet("limits/{limitType}")]
    public async Task<IActionResult> CheckLimit(string limitType, [FromQuery] int amount = 1)
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null) return Unauthorized();

        var result = await _entitlementResolver.CheckLimitAsync(companyId.Value, limitType, amount);
        if (!result.Allowed)
        {
            return StatusCode(402, result);
        }
        return Ok(result);
    }

    private Guid? ParseGuid(string claimType)
    {
        var value = User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public record TrackUsageRequest(string ModuleKey, string MetricType, decimal Quantity, Guid? DepartmentId);
public record StripeWebhookRequest(Guid CompanyId, string StripeCustomerId, string StripeSubscriptionId, string Plan, string Status);
public record ChangePlanRequest(Guid PlanTemplateId, bool Immediate = true);
public record ChangePlanResponse(bool Success, string Message, string? PendingChangeAt);
