using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

/// <summary>
/// Service for checking usage thresholds and queuing alerts.
/// Implements idempotency to prevent duplicate alerts within a billing period.
/// </summary>
public class UsageAlertService : IUsageAlertService
{
    private const decimal WarningThreshold = 80m;
    private const decimal CriticalThreshold = 100m;
    
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<UsageAlertService> _logger;
    
    public UsageAlertService(
        AppDbContext db,
        IEmailService emailService,
        ILogger<UsageAlertService> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task CheckAndAlertAsync(Guid companyId, string metricType, decimal currentUsage, decimal limit)
    {
        if (limit <= 0)
        {
            _logger.LogDebug("Skipping alert check for {MetricType} - limit is {Limit}", metricType, limit);
            return;
        }
        
        var percentage = (currentUsage / limit) * 100;
        
        if (percentage >= CriticalThreshold)
        {
            await SendAlertAsync(companyId, metricType, currentUsage, limit, percentage, "UsageCritical100");
        }
        else if (percentage >= WarningThreshold)
        {
            await SendAlertAsync(companyId, metricType, currentUsage, limit, percentage, "UsageWarning80");
        }
    }
    
    private async Task SendAlertAsync(
        Guid companyId,
        string metricType,
        decimal currentUsage,
        decimal limit,
        decimal percentage,
        string alertType)
    {
        // Idempotency: skip if already sent this billing period
        if (await HasAlertBeenSentThisPeriodAsync(companyId, alertType))
        {
            _logger.LogDebug(
                "Alert {AlertType} already sent for company {CompanyId} this period",
                alertType, companyId);
            return;
        }
        
        // Find all company owners
        var owners = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId && m.CompanyRole == "CompanyOwner")
            .Select(m => m.User.Email)
            .ToListAsync();
        
        if (!owners.Any())
        {
            _logger.LogWarning("No company owners found for company {CompanyId}", companyId);
            return;
        }
        
        // Queue notification for each owner
        foreach (var email in owners)
        {
            var notification = new NotificationQueue
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Type = alertType,
                Channel = "Email",
                RecipientEmail = email,
                MetricType = metricType,
                CurrentUsage = currentUsage,
                Limit = limit,
                Percentage = percentage,
                CreatedAt = DateTime.UtcNow
            };
            _db.NotificationQueues.Add(notification);
        }
        
        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Queued {AlertType} alert for company {CompanyId} ({Count} recipients)",
            alertType, companyId, owners.Count);
    }
    
    /// <inheritdoc />
    public async Task<bool> HasAlertBeenSentThisPeriodAsync(Guid companyId, string alertType)
    {
        // Get the current billing period
        var subscription = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);
        
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddDays(-30);
        
        // Check if any notification of this type was sent in the current period
        return await _db.NotificationQueues
            .AnyAsync(n => n.CompanyId == companyId 
                && n.Type == alertType 
                && n.CreatedAt >= periodStart);
    }
}
