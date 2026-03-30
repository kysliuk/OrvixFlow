using System;

namespace OrvixFlow.Core.Entities;

public class CompanySubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid PlanTemplateId { get; set; }
    public string Status { get; set; } = "Trialing";
    public string BillingInterval { get; set; } = "Monthly";
    public DateTime CurrentPeriodStart { get; set; } = DateTime.UtcNow;
    public DateTime CurrentPeriodEnd { get; set; } = DateTime.UtcNow.AddMonths(1);
    public DateTime? TrialEndsAt { get; set; }
    public Guid? PendingPlanId { get; set; }
    public DateTime? PendingChangeAt { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Company { get; set; } = null!;
    public PlanTemplate PlanTemplate { get; set; } = null!;
    public PlanTemplate? PendingPlan { get; set; }
}

public static class SubscriptionStatus
{
    public const string Trialing = "Trialing";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Suspended = "Suspended";
    public const string Cancelled = "Cancelled";
}
