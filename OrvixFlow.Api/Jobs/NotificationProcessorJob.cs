using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

/// <summary>
/// Hangfire job that processes pending notifications from the queue.
/// Runs every 5 minutes to send queued alerts via email.
/// </summary>
public class NotificationProcessorJob
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationProcessorJob> _logger;
    
    public NotificationProcessorJob(
        AppDbContext db,
        IEmailService emailService,
        ILogger<NotificationProcessorJob> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }
    
    /// <summary>
    /// Execute the job - process up to 50 pending notifications.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task ExecuteAsync()
    {
        var staleBefore = DateTime.UtcNow.Subtract(ProcessingLease);
        var pending = await _db.NotificationQueues
            .IgnoreQueryFilters()
            .Where(n => !n.Processed
                && !n.Failed
                && (!n.IsProcessing || (n.ProcessingStartedAt != null && n.ProcessingStartedAt < staleBefore)))
            .OrderBy(n => n.CreatedAt)
            .Take(BatchSize)
            .ToListAsync();
        
        if (!pending.Any())
        {
            _logger.LogDebug("No pending notifications to process");
            return;
        }
        
        _logger.LogInformation("Processing {Count} pending notifications", pending.Count);
        
        foreach (var notification in pending)
        {
            var attemptStartedAt = DateTime.UtcNow;
            notification.IsProcessing = true;
            notification.ProcessingStartedAt = attemptStartedAt;
            notification.LastAttemptedAt = attemptStartedAt;
            notification.AttemptCount += 1;
            notification.LastError = null;
            await _db.SaveChangesAsync();

            try
            {
                await SendEmailAsync(notification);
                notification.Processed = true;
                notification.ProcessedAt = DateTime.UtcNow;
                notification.IsProcessing = false;
                notification.ProcessingStartedAt = null;
                notification.LastError = null;
                await _db.SaveChangesAsync();
                _logger.LogInformation(
                    "Sent notification {Id} for company {CompanyId} to {Recipient}",
                    notification.Id, notification.CompanyId, notification.RecipientEmail);
            }
            catch (Exception ex)
            {
                if (notification.Processed)
                {
                    _db.Entry(notification).State = EntityState.Detached;
                    _logger.LogCritical(
                        ex,
                        "Notification {Id} for company {CompanyId} was delivered but delivery state could not be persisted. The row remains claimed until the processing lease expires.",
                        notification.Id,
                        notification.CompanyId);
                    continue;
                }

                notification.IsProcessing = false;
                notification.ProcessingStartedAt = null;
                notification.LastError = SanitizeError(ex.Message);
                notification.Failed = notification.AttemptCount >= MaxAttempts;

                _logger.LogError(ex, "Failed to send notification {Id} for company {CompanyId}", notification.Id, notification.CompanyId);
                await _db.SaveChangesAsync();
            }
        }
    }
    
    private async Task SendEmailAsync(Core.Entities.NotificationQueue notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Subject) && !string.IsNullOrWhiteSpace(notification.Body))
        {
            await _emailService.SendEmailAsync(notification.RecipientEmail, notification.Subject, notification.Body);
            return;
        }

        var subject = notification.Type switch
        {
            "UsageWarning80" => "⚠️ Usage Alert: 80% Threshold Reached",
            "UsageCritical100" => "🚨 Usage Alert: 100% Limit Reached",
            "SubscriptionPastDue" => "💳 Subscription Payment Failed",
            _ => $"OrvixFlow Notification: {notification.Type}"
        };
        
        var metricDisplayName = notification.MetricType switch
        {
            "ai-tokens" => "AI Tokens",
            "storage-mb" => "Storage (MB)",
            "inbox-messages" => "Inbox Messages",
            "knowledge-bases" => "Knowledge Bases",
            _ => notification.MetricType
        };
        
        var body = $@"Hello,

Your {metricDisplayName} usage has reached {notification.Percentage:F0}% of your limit.

Current usage: {notification.CurrentUsage:N0}
Limit: {notification.Limit:N0}

{(notification.Type == "UsageCritical100" ? "Your limit has been reached. Please consider upgrading your plan or reducing usage to avoid service interruption." : "Please consider upgrading your plan or reducing usage to avoid reaching your limit.")}

Visit your billing dashboard: {notification.MetricType}

- The OrvixFlow Team
";
        
        await _emailService.SendEmailAsync(notification.RecipientEmail, subject, body);
    }

    private static string SanitizeError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown email delivery error.";
        }

        const int maxLength = 500;
        return message.Length <= maxLength ? message : message[..maxLength];
    }
}
