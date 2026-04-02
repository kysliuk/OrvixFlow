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

public class TrialExpirationJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TrialExpirationJob> _logger;

    public TrialExpirationJob(IServiceProvider serviceProvider, ILogger<TrialExpirationJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task ExecuteAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TrialExpirationJob>>();

        var now = DateTime.UtcNow;

        var expiredTrials = await dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
            .Where(s => s.Status == SubscriptionStatus.Trialing && s.TrialEndsAt.HasValue && s.TrialEndsAt.Value <= now)
            .ToListAsync();

        if (expiredTrials.Count == 0)
        {
            logger.LogInformation("No expired trials found at {Now}", now);
            return;
        }

        var freePlan = await dbContext.PlanTemplates
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.IsFree && p.IsActive);

        if (freePlan == null)
        {
            logger.LogError("Free plan not found, cannot downgrade expired trials");
            return;
        }

        foreach (var subscription in expiredTrials)
        {
            var oldPlanName = subscription.PlanTemplate?.Name ?? "Unknown";
            var companyId = subscription.CompanyId;

            subscription.PlanTemplateId = freePlan.Id;
            subscription.Status = SubscriptionStatus.Active;
            subscription.BillingInterval = "Monthly";
            subscription.CurrentPeriodStart = now;
            subscription.CurrentPeriodEnd = now.AddMonths(1);
            subscription.TrialEndsAt = null;
            subscription.UpdatedAt = now;

            var tenant = await dbContext.Tenants.FindAsync(companyId);
            if (tenant != null)
            {
                tenant.Plan = freePlan.Slug;
                tenant.SubscriptionStatus = SubscriptionStatus.Active;
            }

            dbContext.AuditTrails.Add(new AuditTrail
            {
                Id = Guid.NewGuid(),
                TenantId = companyId,
                Action = "TrialExpired",
                Actor = "system",
                EntityId = subscription.Id.ToString(),
                PreviousState = oldPlanName,
                NewState = freePlan.Name,
                DecisionDetails = $"Trial expired for plan '{oldPlanName}'. Downgraded to Free plan.",
                Timestamp = now
            });

            logger.LogInformation(
                "Trial expired for company {CompanyId}. Downgraded from '{OldPlan}' to Free",
                companyId, oldPlanName);
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Processed {Count} expired trials", expiredTrials.Count);
    }
}
