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

        // T4-1: Use effective entitlements from resolver (respects overrides)
        var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(companyId.Value);
        
        // Get subscription for period dates
        var subscription = await _entitlementResolver.GetSubscriptionAsync(companyId.Value);
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddDays(-30);
        
        var used = await _db.UsageEvents
            .Where(e => e.CompanyId == companyId.Value && e.OccurredAt >= periodStart)
            .SumAsync(e => (decimal?)e.Quantity) ?? 0m;

        return Ok(new
        {
            used = used,
            limit = entitlements.MaxMonthlyTokens,
            plan = company.Plan ?? "Free",
            periodStart = periodStart.ToString("o"),
            currentPeriodEnd = subscription?.CurrentPeriodEnd.ToString("o")
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

    /// <summary>
    /// T4-1: Get subscription with effective entitlements (respects admin overrides).
    /// Fake billing history removed - will be available after Stripe integration.
    /// </summary>
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
        if (companyId == null)
        {
            return Unauthorized();
        }

        var subscription = await _entitlementResolver.GetSubscriptionAsync(companyId.Value);
        // T4-1: Use GetEffectiveEntitlementsAsync to respect admin overrides
        var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(companyId.Value);

        var memberCount = await _db.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId.Value && m.Status == "Active");

        var kbCount = await _db.KnowledgeBases
            .CountAsync(k => k.TenantId == companyId.Value);

        // Get inbox message count for this period
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddDays(-30);
        var inboxMessageCount = await _db.InboxEvents
            .Where(e => e.TenantId == companyId.Value && e.ReceivedAtUtc >= periodStart)
            .CountAsync();

        // Get mailbox connection count
        var mailboxConnectionCount = await _db.MailboxConnections
            .Where(m => m.TenantId == companyId.Value)
            .CountAsync();

        // T4-1: Removed fake billing history - returns empty array until Stripe integration
        return Ok(new
        {
            plan = subscription?.PlanTemplate != null ? new
            {
                name = subscription.PlanTemplate.Name,
                price = subscription.PlanTemplate.MonthlyPriceCents,
                interval = subscription.PlanTemplate.BillingInterval.ToClaimValue()
            } : null,
            status = subscription?.Status.ToClaimValue() ?? "Active",
            currentPeriodStart = subscription?.CurrentPeriodStart.ToString("o"),
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
                usedKnowledgeBases = kbCount,
                maxInboxMessagesPerMonth = entitlements.MaxInboxMessagesPerMonth,
                inboxMessagesUsedThisMonth = inboxMessageCount,
                maxMailboxConnections = entitlements.MaxMailboxConnections,
                mailboxConnectionsUsed = mailboxConnectionCount,
                // T4-1: Flag when override is active
                hasEntitlementOverride = entitlements.HasEntitlementOverride,
                overrideNote = entitlements.OverrideNote
            },
            // T4-1: Removed fake billing history
            billingHistoryNote = "Invoice history will be available once billing is activated."
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
        var currentSortOrder = subscription?.PlanTemplate?.SortOrder ?? 0;

        // Get only publicly visible and active plans
        var plans = await _planService.GetActivePlansAsync();
        var visiblePlans = plans.Where(p => p.IsPubliclyVisible).ToList();

        var planDtos = visiblePlans.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            slug = p.Slug,
            description = p.Description,
            monthlyPriceCents = p.MonthlyPriceCents,
            yearlyPriceCents = p.YearlyPriceCents,
            billingInterval = p.BillingInterval.ToClaimValue(),
            maxSeats = p.MaxSeats,
            isFree = p.IsFree,
            // Use SortOrder for upgrade/downgrade detection (not price)
            isUpgrade = p.SortOrder > currentSortOrder,
            isDowngrade = p.SortOrder < currentSortOrder,
            isCurrentPlan = p.Id == (currentPlanId ?? Guid.Empty),
            entitlements = new
            {
                maxMonthlyTokens = p.Entitlements?.MaxMonthlyTokens ?? 50000,
                maxApiRequestsPerDay = p.Entitlements?.MaxApiRequestsPerDay ?? 500,
                maxStorageMb = p.Entitlements?.MaxStorageMb ?? 100,
                maxKnowledgeBases = p.Entitlements?.MaxKnowledgeBases ?? 1,
                maxInboxMessagesPerMonth = p.Entitlements?.MaxInboxMessagesPerMonth ?? 0,
                maxMailboxConnections = p.Entitlements?.MaxMailboxConnections ?? 1
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
        catch (DowngradeNotAllowedException ex)
        {
            // T4-3: Return 409 Conflict with clear downgrade blocker info
            return Conflict(new
            {
                error = "DOWNGRADE_BLOCKED",
                message = ex.Message,
                exceededLimit = ex.ExceededLimit,
                currentValue = ex.CurrentValue,
                maxAllowed = ex.MaxAllowed,
                blockers = new[]
                {
                    new { limit = ex.ExceededLimit, current = ex.CurrentValue, maxAllowed = ex.MaxAllowed }
                }
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
            // T4-1: Mark as estimate until Stripe integration
            isEstimate = true,
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
        if (value == null && claimType == "sub")
        {
            value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}

public record TrackUsageRequest(string ModuleKey, string MetricType, decimal Quantity, Guid? DepartmentId);
public record StripeWebhookRequest(Guid CompanyId, string StripeCustomerId, string StripeSubscriptionId, string Plan, string Status);
public record ChangePlanRequest(Guid PlanTemplateId, bool Immediate = true);
public record ChangePlanResponse(bool Success, string Message, string? PendingChangeAt);
