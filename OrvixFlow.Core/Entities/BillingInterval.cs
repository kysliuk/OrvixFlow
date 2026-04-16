namespace OrvixFlow.Core.Entities;

/// <summary>
/// Billing interval for subscription periods.
/// Stored as string in DB via EF value converter.
/// </summary>
public enum BillingInterval
{
    Monthly,
    Yearly,
    Custom
}

/// <summary>
/// Extension methods for BillingInterval enum.
/// </summary>
public static class BillingIntervalExtensions
{
    /// <summary>Parse a string from DB into BillingInterval enum.</summary>
    public static BillingInterval ParseInterval(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out BillingInterval result)
            ? result
            : BillingInterval.Monthly;

    /// <summary>Serialize to canonical string for DB storage.</summary>
    public static string ToClaimValue(this BillingInterval interval) => interval.ToString();

    /// <summary>
    /// Get the number of days for this billing interval.
    /// Custom intervals default to 30 days (manual billing).
    /// </summary>
    public static int GetPeriodDays(this BillingInterval interval) => interval switch
    {
        BillingInterval.Yearly  => 365,
        BillingInterval.Monthly => 30,
        BillingInterval.Custom  => 30, // Manual billing - no automatic renewal
        _                       => 30
    };
}
