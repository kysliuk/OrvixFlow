using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class CompanySubscriptionService : ICompanySubscriptionService
{
    private readonly AppDbContext _dbContext;
    private readonly IPlanService _planService;
    private readonly IAuditService? _auditService;

    public CompanySubscriptionService(AppDbContext dbContext, IPlanService planService, IAuditService? auditService = null)
    {
        _dbContext = dbContext;
        _planService = planService;
        _auditService = auditService;
    }

    public async Task<CompanySubscription> CreateTrialSubscriptionAsync(Guid companyId, Guid planTemplateId)
    {
        var plan = await _planService.GetPlanByIdAsync(planTemplateId);
        if (plan == null)
        {
            throw new PlanNotFoundException(planTemplateId);
        }

        var existingSubscription = await _dbContext.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (existingSubscription != null)
        {
            throw new SubscriptionException("Company already has a subscription");
        }

        var trialDays = plan.TrialDays;
        var subscription = new CompanySubscription
        {
            CompanyId = companyId,
            PlanTemplateId = planTemplateId,
            Status = SubscriptionStatus.Trialing,
            BillingInterval = plan.BillingInterval,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(trialDays),
            TrialEndsAt = DateTime.UtcNow.AddDays(trialDays)
        };

        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization for new trial subscription
        await SyncTenantDenormalizationAsync(companyId, plan.Slug, SubscriptionStatus.Trialing);

        return subscription;
    }

    public async Task<CompanySubscription?> GetSubscriptionAsync(Guid companyId)
    {
        return await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p!.Entitlements)
            .Include(s => s.PlanTemplate)
                .ThenInclude(p => p!.ModuleInclusions)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);
    }

    public async Task<CompanySubscription> AssignPlanAsync(Guid companyId, Guid planTemplateId, string? billingInterval = null)
    {
        var plan = await _planService.GetPlanByIdAsync(planTemplateId);
        if (plan == null)
        {
            throw new PlanNotFoundException(planTemplateId);
        }

        if (!plan.IsActive)
        {
            throw new PlanNotActiveException(planTemplateId);
        }

        var existingSubscription = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        var memberCount = await _dbContext.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");

        if (plan.MaxSeats.HasValue && memberCount > plan.MaxSeats.Value)
        {
            throw new SeatLimitExceededException(memberCount, plan.MaxSeats.Value);
        }

        var interval = billingInterval ?? plan.BillingInterval;
        var periodDays = interval == "Yearly" ? 365 : 30;
        var targetStatus = plan.IsFree ? SubscriptionStatus.Active : SubscriptionStatus.Trialing;

        if (existingSubscription != null)
        {
            existingSubscription.PlanTemplateId = planTemplateId;
            existingSubscription.BillingInterval = interval;
            existingSubscription.CurrentPeriodStart = DateTime.UtcNow;
            existingSubscription.CurrentPeriodEnd = DateTime.UtcNow.AddDays(periodDays);
            existingSubscription.Status = targetStatus;
            existingSubscription.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            // Sync tenant denormalization (T1-2/T1-3)
            await SyncTenantDenormalizationAsync(companyId, plan.Slug, targetStatus);
            
            if (_auditService != null)
            {
                await _auditService.RecordAsync(companyId, "PlanAssigned", $"Plan '{plan.Name}' (ID: {planTemplateId}) assigned to company. Status: {existingSubscription.Status}");
            }
            
            return existingSubscription;
        }

        var subscription = new CompanySubscription
        {
            CompanyId = companyId,
            PlanTemplateId = planTemplateId,
            Status = targetStatus,
            BillingInterval = interval,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(periodDays),
            TrialEndsAt = plan.IsTrialAllowed ? DateTime.UtcNow.AddDays(plan.TrialDays) : null
        };

        _dbContext.CompanySubscriptions.Add(subscription);
        
        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, plan.Slug, targetStatus);

        if (_auditService != null)
        {
            await _auditService.RecordAsync(companyId, "PlanAssigned", $"Plan '{plan.Name}' (ID: {planTemplateId}) assigned to new subscription. Status: {subscription.Status}");
        }

        return subscription;
    }

    public async Task<CompanySubscription> ChangePlanAsync(Guid companyId, Guid newPlanTemplateId, bool immediate = false)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null)
        {
            throw new SubscriptionNotFoundException(companyId);
        }

        var newPlan = await _planService.GetPlanByIdAsync(newPlanTemplateId);
        if (newPlan == null)
        {
            throw new PlanNotFoundException(newPlanTemplateId);
        }

        if (!newPlan.IsActive)
        {
            throw new PlanNotActiveException(newPlanTemplateId);
        }

        var memberCount = await _dbContext.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");

        if (newPlan.MaxSeats.HasValue && memberCount > newPlan.MaxSeats.Value)
        {
            throw new SeatLimitExceededException(memberCount, newPlan.MaxSeats.Value);
        }

        var oldPlanName = subscription.PlanTemplate?.Name ?? "None";

        if (immediate)
        {
            subscription.PlanTemplateId = newPlanTemplateId;
            subscription.Status = SubscriptionStatus.Active;
            subscription.PendingPlanId = null;
            subscription.PendingChangeAt = null;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            
            // Sync tenant denormalization (T1-2/T1-3) - immediate change
            await SyncTenantDenormalizationAsync(companyId, newPlan.Slug, SubscriptionStatus.Active);
        }
        else
        {
            subscription.PendingPlanId = newPlanTemplateId;
            subscription.PendingChangeAt = subscription.CurrentPeriodEnd;
            subscription.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
        }

        if (_auditService != null)
        {
            await _auditService.RecordAsync(companyId, "PlanChanged", $"Plan changed from '{oldPlanName}' to '{newPlan.Name}' (ID: {newPlanTemplateId}). Immediate: {immediate}");
        }

        return subscription;
    }

    public async Task<CompanySubscription> SuspendSubscriptionAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null)
        {
            throw new SubscriptionNotFoundException(companyId);
        }

        var planName = subscription.PlanTemplate?.Name ?? "Unknown";
        subscription.Status = SubscriptionStatus.Suspended;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionStatus.Suspended);

        if (_auditService != null)
        {
            await _auditService.RecordAsync(companyId, "SubscriptionSuspended", $"Subscription for plan '{planName}' has been suspended");
        }

        return subscription;
    }

    public async Task<CompanySubscription> ReactivateSubscriptionAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null)
        {
            throw new SubscriptionNotFoundException(companyId);
        }

        var planName = subscription.PlanTemplate?.Name ?? "Unknown";
        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionStatus.Active);

        if (_auditService != null)
        {
            await _auditService.RecordAsync(companyId, "SubscriptionReactivated", $"Subscription for plan '{planName}' has been reactivated");
        }

        return subscription;
    }

    public async Task<CompanySubscription> CancelSubscriptionAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null)
        {
            throw new SubscriptionNotFoundException(companyId);
        }

        var planName = subscription.PlanTemplate?.Name ?? "Unknown";
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionStatus.Cancelled);

        if (_auditService != null)
        {
            await _auditService.RecordAsync(companyId, "SubscriptionCancelled", $"Subscription for plan '{planName}' has been cancelled");
        }

        return subscription;
    }

    public async Task<bool> IsPlanChangeAllowedAsync(Guid companyId, Guid newPlanTemplateId)
    {
        var newPlan = await _planService.GetPlanByIdAsync(newPlanTemplateId);
        if (newPlan == null || !newPlan.IsActive)
        {
            return false;
        }

        var memberCount = await _dbContext.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");

        return !newPlan.MaxSeats.HasValue || memberCount <= newPlan.MaxSeats.Value;
    }

    /// <summary>
    /// Syncs Tenant denormalized fields (Plan, SubscriptionStatus) with the authoritative CompanySubscription.
    /// This ensures Tenant.Plan and Tenant.SubscriptionStatus stay in sync with the subscription lifecycle.
    /// </summary>
    private async Task SyncTenantDenormalizationAsync(Guid companyId, string planSlug, string status)
    {
        var tenant = await _dbContext.Tenants.FindAsync(companyId);
        if (tenant != null)
        {
            tenant.Plan = planSlug;
            tenant.SubscriptionStatus = status;
        }
    }
}
