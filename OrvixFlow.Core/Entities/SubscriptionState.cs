namespace OrvixFlow.Core.Entities;

/// <summary>
/// Subscription lifecycle state for CompanySubscription.
/// Stored as string in DB via EF value converter (same pattern as UserRole).
/// </summary>
public enum SubscriptionState
{
    Trialing,
    Active,
    PastDue,
    Suspended,
    Cancelled
}

/// <summary>
/// Extension methods for SubscriptionState enum.
/// </summary>
public static class SubscriptionStateExtensions
{
    /// <summary>Parse a string from DB/JWT into SubscriptionState enum.</summary>
    public static SubscriptionState ParseState(string? value) =>
        Enum.TryParse(value, ignoreCase: true, out SubscriptionState result)
            ? result
            : SubscriptionState.Active;

    /// <summary>Serialize to canonical string for DB/JWT storage.</summary>
    public static string ToClaimValue(this SubscriptionState state) => state.ToString();

    /// <summary>Returns true if the subscription state allows module access.</summary>
    public static bool IsAccessAllowed(this SubscriptionState state) =>
        state is SubscriptionState.Trialing or SubscriptionState.Active;

    /// <summary>Returns true if the subscription is in a terminal state.</summary>
    public static bool IsTerminal(this SubscriptionState state) =>
        state is SubscriptionState.Cancelled;
}
