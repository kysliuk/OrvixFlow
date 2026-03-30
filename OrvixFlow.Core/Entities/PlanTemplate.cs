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
    public string BillingInterval { get; set; } = "Monthly";
    public int? MaxSeats { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFree { get; set; }
    public bool IsTrialAllowed { get; set; } = true;
    public int TrialDays { get; set; } = 14;
    public bool LegacyLocked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }

    public ICollection<PlanModuleInclusion> ModuleInclusions { get; set; } = new List<PlanModuleInclusion>();
    public PlanEntitlements? Entitlements { get; set; }
    public ICollection<CompanySubscription> CompanySubscriptions { get; set; } = new List<CompanySubscription>();
}
