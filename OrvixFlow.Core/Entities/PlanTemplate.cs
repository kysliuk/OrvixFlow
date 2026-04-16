using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class PlanTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MonthlyPriceCents { get; set; }
    public int YearlyPriceCents { get; set; }
    public string Currency { get; set; } = "USD";
    
    /// <summary>
    /// Default billing interval for this plan. Use BillingInterval enum.
    /// </summary>
    public BillingInterval BillingInterval { get; set; } = BillingInterval.Monthly;
    
    public int? MaxSeats { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFree { get; set; }
    public bool IsTrialAllowed { get; set; } = true;
    public int TrialDays { get; set; } = 14;
    public bool LegacyLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Sort order for plan comparison. Higher = more expensive/tiered.
    /// Free=0, Starter=1, Growth=2, Business=3, Enterprise=4
    /// </summary>
    public int SortOrder { get; set; } = 0;
    
    /// <summary>
    /// Whether this plan is visible in the public billing page.
    /// </summary>
    public bool IsPubliclyVisible { get; set; } = true;

    public ICollection<PlanModuleInclusion> ModuleInclusions { get; set; } = new List<PlanModuleInclusion>();
    public PlanEntitlements? Entitlements { get; set; }
    public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();
}
