using System;
using System.Linq;
using System.Threading.Tasks;
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
    public async Task ExecuteAsync()
    {
        var pending = await _db.NotificationQueues
            .Where(n => !n.Processed)
            .OrderBy(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
        
        if (!pending.Any())
        {
            _logger.LogDebug("No pending notifications to process");
            return;
        }
        
        _logger.LogInformation("Processing {Count} pending notifications", pending.Count);
        
        foreach (var notification in pending)
        {
            try
            {
                await SendEmailAsync(notification);
                notification.Processed = true;
                notification.ProcessedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Sent notification {Id} to {Recipient}",
                    notification.Id, notification.RecipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification {Id}", notification.Id);
            }
        }
        
        await _db.SaveChangesAsync();
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
}
