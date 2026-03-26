using System;

namespace OrvixFlow.Core.Entities;

public class BillingSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string StripeCustomerId { get; set; } = string.Empty;
    public string StripeSubscriptionId { get; set; } = string.Empty;
    public string Status { get; set; } = "Trialing";
    public string CurrentPlan { get; set; } = "Free";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
