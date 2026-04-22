using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Queue entry for pending notifications (e.g., usage alerts).
/// Processed by NotificationProcessorJob.
/// </summary>
public class NotificationQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid CompanyId { get; set; }
    
    /// <summary>
    /// Notification type: "UsageWarning80", "UsageCritical100", "SubscriptionPastDue", etc.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Delivery channel: "Email", "Webhook", etc.
    /// </summary>
    public string Channel { get; set; } = "Email";
    
    /// <summary>
    /// Target email address or webhook URL.
    /// </summary>
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit subject for general-purpose queued email notifications.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Optional explicit HTML/text body for general-purpose queued email notifications.
    /// </summary>
    public string? Body { get; set; }
    
    /// <summary>
    /// The metric being reported (e.g., "ai-tokens", "storage-mb").
    /// </summary>
    public string MetricType { get; set; } = string.Empty;
    
    /// <summary>
    /// Current usage value at time of notification.
    /// </summary>
    public decimal CurrentUsage { get; set; }
    
    /// <summary>
    /// Limit/threshold at time of notification.
    /// </summary>
    public decimal Limit { get; set; }
    
    /// <summary>
    /// Percentage of limit consumed.
    /// </summary>
    public decimal Percentage { get; set; }
    
    /// <summary>
    /// Whether this notification has been processed.
    /// </summary>
    public bool Processed { get; set; } = false;

    /// <summary>
    /// Whether the notification should no longer be retried.
    /// </summary>
    public bool Failed { get; set; } = false;

    /// <summary>
    /// Whether a worker has currently claimed this notification for delivery.
    /// </summary>
    public bool IsProcessing { get; set; } = false;

    /// <summary>
    /// Number of delivery attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// When delivery was last attempted.
    /// </summary>
    public DateTime? LastAttemptedAt { get; set; }

    /// <summary>
    /// Short, sanitized description of the last delivery failure.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// When the current processing claim started.
    /// </summary>
    public DateTime? ProcessingStartedAt { get; set; }
     
    /// <summary>
    /// When the notification was queued.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the notification was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
    
    // Navigation
    public Tenant Company { get; set; } = null!;
}
