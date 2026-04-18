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
