using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

/// <summary>
/// Periodic job to advance billing periods when CurrentPeriodEnd is reached.
/// Runs daily to check for expired subscriptions and roll over to the next billing period.
/// </summary>
public class UsagePeriodRolloverJob
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService? _auditService;
    private readonly ILogger<UsagePeriodRolloverJob> _logger;

    public UsagePeriodRolloverJob(
        AppDbContext dbContext,
        IAuditService? auditService,
        ILogger<UsagePeriodRolloverJob> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        
        // Find all active subscriptions with expired periods
        var expiredSubscriptions = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Include(s => s.PlanTemplate)
            .Where(s => s.Status == SubscriptionState.Active
                     && s.CurrentPeriodEnd <= now)
            .ToListAsync();

        if (expiredSubscriptions.Count == 0)
        {
            _logger.LogDebug("No expired subscriptions found for period rollover");
            return;
        }

        _logger.LogInformation("Processing {Count} expired subscriptions for period rollover", expiredSubscriptions.Count);

        foreach (var subscription in expiredSubscriptions)
        {
            try
            {
                var oldPeriodEnd = subscription.CurrentPeriodEnd;
                var intervalDays = subscription.BillingInterval switch
                {
                    BillingInterval.Yearly => 365,
                    BillingInterval.Monthly => 30,
                    BillingInterval.Custom => 30,
                    _ => 30
                };

                // Advance period
                subscription.CurrentPeriodStart = oldPeriodEnd;
                subscription.CurrentPeriodEnd = oldPeriodEnd.AddDays(intervalDays);
                subscription.UpdatedAt = now;

                _logger.LogInformation(
                    "Subscription {SubscriptionId} period rolled over: {OldStart} -> {NewStart}, {OldEnd} -> {NewEnd}",
                    subscription.Id,
                    subscription.CurrentPeriodStart, oldPeriodEnd,
                    oldPeriodEnd,
                    subscription.CurrentPeriodEnd);

                // Record audit event
                if (_auditService != null)
                {
                    await _auditService.RecordAsync(
                        subscription.CompanyId,
                        "PeriodRolledOver",
                        $"Billing period advanced to {subscription.CurrentPeriodEnd:O}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollover period for subscription {SubscriptionId}", subscription.Id);
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}
