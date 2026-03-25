using System;

namespace OrvixFlow.Core.Entities;

public class InboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public string MessageId { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    
    public DateTime ReceivedAtUtc { get; set; }
    public string Status { get; set; } = "Ingested"; // Ingested, Processing, Action_Required, Auto_Approved, Human_Approved, Human_Rejected, Completed, Failed
}
