using System;

namespace OrvixFlow.Core.Entities;

public class CompanySubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid PlanTemplateId { get; set; }
    
    /// <summary>
    /// Subscription lifecycle state. Use SubscriptionState enum for type safety.
    /// </summary>
    public SubscriptionState Status { get; set; } = SubscriptionState.Trialing;
    
    /// <summary>
    /// Billing interval (Monthly/Yearly/Custom). Use BillingInterval enum.
    /// </summary>
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);
    public DateTime? TrialEndsAt { get; set; }
    public Guid? PendingPlanId { get; set; }
    public DateTime? PendingChangeAt { get; set; }
    
    /// <summary>
    /// External Stripe customer ID for payment integration.
    /// </summary>
    public string? ExternalCustomerId { get; set; }
    
    /// <summary>
    /// External Stripe subscription ID for payment integration.
    /// </summary>
    public string? ExternalSubscriptionId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Company { get; set; } = null!;
    public PlanTemplate PlanTemplate { get; set; } = null!;
    public PlanTemplate? PendingPlan { get; set; }
}
