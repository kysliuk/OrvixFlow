using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

/// <summary>
/// Processes scheduled plan changes when the pending period ends.
/// Runs at the start of each billing period to apply any pending plan upgrades/downgrades.
/// </summary>
public class PendingPlanChangeJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PendingPlanChangeJob> _logger;

    public PendingPlanChangeJob(IServiceProvider serviceProvider, ILogger<PendingPlanChangeJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processes all pending plan changes where the change date has passed.
    /// This job should run daily or at the start of each billing period.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ExecuteAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PendingPlanChangeJob>>();

        var now = DateTime.UtcNow;

        // Find all subscriptions with pending plan changes that should be applied
        var pendingChanges = await dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
            .Include(s => s.PendingPlan)
            .Where(s => s.PendingPlanId != null 
                     && s.PendingChangeAt.HasValue 
                     && s.PendingChangeAt.Value <= now)
            .ToListAsync();

        if (pendingChanges.Count == 0)
        {
            logger.LogInformation("No pending plan changes to process at {Now}", now);
            return;
        }

        logger.LogInformation("Processing {Count} pending plan changes", pendingChanges.Count);

        var processedCount = 0;
        var failedCount = 0;

        foreach (var subscription in pendingChanges)
        {
            try
            {
                var oldPlanName = subscription.PlanTemplate?.Name ?? "None";
                var newPlanName = subscription.PendingPlan?.Name ?? "Unknown";
                var companyId = subscription.CompanyId;

                // Apply the pending plan change
                var newPlan = subscription.PendingPlan;
                if (newPlan == null)
                {
                    logger.LogWarning(
                        "Pending plan not found for company {CompanyId}. Clearing pending change.",
                        companyId);
                    subscription.PendingPlanId = null;
                    subscription.PendingChangeAt = null;
                    subscription.UpdatedAt = now;
                    failedCount++;
                    continue;
                }

                // Check if the new plan is still active
                if (!newPlan.IsActive)
                {
                    logger.LogWarning(
                        "Pending plan '{PlanName}' is no longer active for company {CompanyId}. Clearing pending change.",
                        newPlan.Name, companyId);
                    subscription.PendingPlanId = null;
                    subscription.PendingChangeAt = null;
                    subscription.UpdatedAt = now;
                    failedCount++;
                    continue;
                }

                // Apply the plan change
                subscription.PlanTemplateId = newPlan.Id;
                subscription.Status = SubscriptionState.Active;
                subscription.PendingPlanId = null;
                subscription.PendingChangeAt = null;
                subscription.UpdatedAt = now;

                // Sync the tenant denormalized fields
                var tenant = await dbContext.Tenants.FindAsync(companyId);
                if (tenant != null)
                {
                    tenant.Plan = newPlan.Slug;
                    tenant.SubscriptionStatus = SubscriptionState.Active.ToClaimValue();
                }

                // Record audit trail
                dbContext.AuditTrails.Add(new AuditTrail
                {
                    Id = Guid.NewGuid(),
                    TenantId = companyId,
                    Action = "PlanChangeApplied",
                    Actor = "system",
                    EntityId = subscription.Id.ToString(),
                    PreviousState = oldPlanName,
                    NewState = newPlanName,
                    DecisionDetails = $"Scheduled plan change from '{oldPlanName}' to '{newPlanName}' has been applied.",
                    Timestamp = now
                });

                logger.LogInformation(
                    "Applied pending plan change for company {CompanyId}: {OldPlan} -> {NewPlan}",
                    companyId, oldPlanName, newPlanName);

                processedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to process pending plan change for company {CompanyId}",
                    subscription.CompanyId);
                failedCount++;
            }
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Completed processing pending plan changes. Processed: {Processed}, Failed: {Failed}",
            processedCount, failedCount);
    }

    /// <summary>
    /// Processes pending plan changes for a specific company.
    /// Can be called directly when a billing period ends.
    /// </summary>
    public async Task ProcessPendingChangeForCompanyAsync(Guid companyId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PendingPlanChangeJob>>();

        var subscription = await dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
            .Include(s => s.PendingPlan)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);

        if (subscription == null)
        {
            logger.LogWarning("No subscription found for company {CompanyId}", companyId);
            return;
        }

        if (subscription.PendingPlanId == null || !subscription.PendingChangeAt.HasValue)
        {
            logger.LogDebug("No pending plan change for company {CompanyId}", companyId);
            return;
        }

        var now = DateTime.UtcNow;

        if (subscription.PendingChangeAt.Value > now)
        {
            logger.LogDebug(
                "Pending change for company {CompanyId} is not yet due (scheduled for {ChangeAt})",
                companyId, subscription.PendingChangeAt.Value);
            return;
        }

        var oldPlanName = subscription.PlanTemplate?.Name ?? "None";
        var newPlan = subscription.PendingPlan;
        var newPlanName = newPlan?.Name ?? "Unknown";

        if (newPlan == null || !newPlan.IsActive)
        {
            logger.LogWarning(
                "Cannot apply pending change for company {CompanyId}: plan unavailable",
                companyId);
            subscription.PendingPlanId = null;
            subscription.PendingChangeAt = null;
            await dbContext.SaveChangesAsync();
            return;
        }

        // Apply the change
        subscription.PlanTemplateId = newPlan.Id;
        subscription.Status = SubscriptionState.Active;
        subscription.PendingPlanId = null;
        subscription.PendingChangeAt = null;
        subscription.UpdatedAt = now;

        var tenant = await dbContext.Tenants.FindAsync(companyId);
        if (tenant != null)
        {
            tenant.Plan = newPlan.Slug;
            tenant.SubscriptionStatus = SubscriptionState.Active.ToClaimValue();
        }

        dbContext.AuditTrails.Add(new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = companyId,
            Action = "PlanChangeApplied",
            Actor = "system",
            EntityId = subscription.Id.ToString(),
            PreviousState = oldPlanName,
            NewState = newPlanName,
            DecisionDetails = $"Scheduled plan change from '{oldPlanName}' to '{newPlanName}' has been applied.",
            Timestamp = now
        });

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Applied pending plan change for company {CompanyId}: {OldPlan} -> {NewPlan}",
            companyId, oldPlanName, newPlanName);
    }
}
