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
            Status = SubscriptionState.Trialing,
            BillingInterval = plan.BillingInterval,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(trialDays),
            TrialEndsAt = DateTime.UtcNow.AddDays(trialDays)
        };

        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization for new trial subscription
        await SyncTenantDenormalizationAsync(companyId, plan.Slug, SubscriptionState.Trialing);

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

    public async Task<CompanySubscription> AssignPlanAsync(Guid companyId, Guid planTemplateId, string? billingInterval = null, string? targetStatus = null)
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

        // Parse billing interval from string if provided, otherwise use plan default
        var interval = !string.IsNullOrEmpty(billingInterval) 
            ? BillingIntervalExtensions.ParseInterval(billingInterval)
            : plan.BillingInterval;
        var periodDays = interval.GetPeriodDays();
        
        // Determine target status: admin-provided status overrides default
        SubscriptionState resolvedStatus;
        if (!string.IsNullOrEmpty(targetStatus))
        {
            resolvedStatus = SubscriptionStateExtensions.ParseState(targetStatus);
        }
        else
        {
            // Default: Free plans are Active, paid plans start as Trialing
            resolvedStatus = plan.IsFree ? SubscriptionState.Active : SubscriptionState.Trialing;
        }

        if (existingSubscription != null)
        {
            existingSubscription.PlanTemplateId = planTemplateId;
            existingSubscription.BillingInterval = interval;
            existingSubscription.CurrentPeriodStart = DateTime.UtcNow;
            existingSubscription.CurrentPeriodEnd = DateTime.UtcNow.AddDays(periodDays);
            existingSubscription.Status = resolvedStatus;
            existingSubscription.UpdatedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            // Sync tenant denormalization (T1-2/T1-3)
            await SyncTenantDenormalizationAsync(companyId, plan.Slug, resolvedStatus);
            
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
            Status = resolvedStatus,
            BillingInterval = interval,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(periodDays),
            TrialEndsAt = plan.IsTrialAllowed ? DateTime.UtcNow.AddDays(plan.TrialDays) : null
        };

        _dbContext.CompanySubscriptions.Add(subscription);
        
        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, plan.Slug, resolvedStatus);

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

        // T4-3: Check seat limit
        var memberCount = await _dbContext.UserCompanyMemberships
            .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");

        if (newPlan.MaxSeats.HasValue && memberCount > newPlan.MaxSeats.Value)
        {
            throw new SeatLimitExceededException(memberCount, newPlan.MaxSeats.Value);
        }

        // T4-3: Check downgrade safety - KB count
        var kbCount = await _dbContext.KnowledgeBaseDocuments
            .CountAsync(k => k.TenantId == companyId);
        
        if (newPlan.Entitlements != null && kbCount > newPlan.Entitlements.MaxKnowledgeBases)
        {
            throw new DowngradeNotAllowedException("KBs", kbCount, newPlan.Entitlements.MaxKnowledgeBases);
        }

        // T4-3: Check downgrade safety - storage
        var storageUsed = await _dbContext.UsageEvents
            .Where(e => e.CompanyId == companyId && e.MetricType == UsageMetric.StorageMb)
            .SumAsync(e => e.Quantity);
        
        if (newPlan.Entitlements != null && storageUsed > newPlan.Entitlements.MaxStorageMb)
        {
            throw new DowngradeNotAllowedException("storageMB", (int)storageUsed, newPlan.Entitlements.MaxStorageMb);
        }

        var oldPlanName = subscription.PlanTemplate?.Name ?? "None";

        if (immediate)
        {
            subscription.PlanTemplateId = newPlanTemplateId;
            subscription.Status = SubscriptionState.Active;
            subscription.PendingPlanId = null;
            subscription.PendingChangeAt = null;
            subscription.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            
            // Sync tenant denormalization (T1-2/T1-3) - immediate change
            await SyncTenantDenormalizationAsync(companyId, newPlan.Slug, SubscriptionState.Active);
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
        subscription.Status = SubscriptionState.Suspended;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionState.Suspended);

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
        subscription.Status = SubscriptionState.Active;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionState.Active);

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
        subscription.Status = SubscriptionState.Cancelled;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Sync tenant denormalization (T1-2/T1-3)
        await SyncTenantDenormalizationAsync(companyId, subscription.PlanTemplate?.Slug ?? "Free", SubscriptionState.Cancelled);

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

        if (newPlan.MaxSeats.HasValue && memberCount > newPlan.MaxSeats.Value)
        {
            return false;
        }

        // Check KB count
        var kbCount = await _dbContext.KnowledgeBaseDocuments
            .CountAsync(k => k.TenantId == companyId);
        
        if (newPlan.Entitlements != null && kbCount > newPlan.Entitlements.MaxKnowledgeBases)
        {
            return false;
        }

        // Check storage
        var storageUsed = await _dbContext.UsageEvents
            .Where(e => e.CompanyId == companyId && e.MetricType == UsageMetric.StorageMb)
            .SumAsync(e => e.Quantity);
        
        if (newPlan.Entitlements != null && storageUsed > newPlan.Entitlements.MaxStorageMb)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Public sync method exposed for webhook handlers.
    /// </summary>
    public async Task SyncTenantDenormalizationAsync(Guid companyId)
    {
        var subscription = await GetSubscriptionAsync(companyId);
        if (subscription == null) return;

        var planSlug = subscription.PlanTemplate?.Slug ?? "Free";
        await SyncTenantDenormalizationAsync(companyId, planSlug, subscription.Status);
    }

    /// <summary>
    /// Syncs Tenant denormalized fields (Plan, SubscriptionStatus) with the authoritative CompanySubscription.
    /// This ensures Tenant.Plan and Tenant.SubscriptionStatus stay in sync with the subscription lifecycle.
    /// </summary>
    private async Task SyncTenantDenormalizationAsync(Guid companyId, string planSlug, SubscriptionState status)
    {
        var tenant = await _dbContext.Tenants.FindAsync(companyId);
        if (tenant != null)
        {
            tenant.Plan = planSlug;
            tenant.SubscriptionStatus = status.ToClaimValue();
        }
    }
}
