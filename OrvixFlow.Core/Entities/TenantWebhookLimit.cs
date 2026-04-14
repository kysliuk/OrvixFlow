using System;

namespace OrvixFlow.Core.Entities;

public class TenantWebhookLimit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int CallbackCount { get; set; } = 0;
    public DateTime WindowStartUtc { get; set; } = DateTime.UtcNow;
    public int Limit { get; set; } = 100;
    public DateTime LastResetUtc { get; set; } = DateTime.UtcNow;
}