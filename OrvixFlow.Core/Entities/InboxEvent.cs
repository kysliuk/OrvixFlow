using System;

namespace OrvixFlow.Core.Entities;

public class InboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public string MessageId { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public string? TraceId { get; set; }
    
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    
    public string? WebhookCallbackPath { get; set; }
    
    public DateTime ReceivedAtUtc { get; set; }
    public string Status { get; set; } = "Ingested";
}
