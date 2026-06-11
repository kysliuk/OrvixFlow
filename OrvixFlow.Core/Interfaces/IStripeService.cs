using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

/// <summary>
/// Stripe payment integration service.
/// Handles customer/subscription management and webhook event processing.
/// </summary>
public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe customer for the given company.
    /// </summary>
    Task<string> CreateCustomerAsync(Guid companyId, string email, string? companyName = null);

    /// <summary>
    /// Creates a Stripe checkout session for new subscription.
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(Guid companyId, Guid planTemplateId, string successUrl, string cancelUrl);

    /// <summary>
    /// Creates a Stripe customer portal session for self-service plan management.
    /// </summary>
    Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl);

    /// <summary>
    /// Creates or updates a Stripe subscription.
    /// </summary>
    Task<(string customerId, string subscriptionId)> CreateOrUpdateSubscriptionAsync(
        Guid companyId, 
        string priceId, 
        string? couponCode = null);

    /// <summary>
    /// Cancels a Stripe subscription.
    /// </summary>
    Task CancelSubscriptionAsync(string subscriptionId, bool cancelAtPeriodEnd = true);

    /// <summary>
    /// Gets the Stripe customer ID for a company.
    /// </summary>
    Task<string?> GetCustomerIdAsync(Guid companyId);

    /// <summary>
    /// Gets the Stripe subscription ID for a company.
    /// </summary>
    Task<string?> GetSubscriptionIdAsync(Guid companyId);

    /// <summary>
    /// Reacts a subscription that was cancelled but not yet expired.
    /// </summary>
    Task ReactivateSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Gets subscription details from Stripe.
    /// </summary>
    Task<SubscriptionDetails?> GetSubscriptionDetailsAsync(string subscriptionId);

    /// <summary>
    /// Preview/calculate proration for plan change.
    /// </summary>
    Task<ProrationPreview?> GetProrationPreviewAsync(Guid companyId, string newPriceId);
}

/// <summary>
/// Subscription details from Stripe API.
/// </summary>
public record SubscriptionDetails(
    string Id,
    string Status,
    DateTime? CurrentPeriodEnd,
    string? CustomerId,
    string? PriceId,
    decimal Amount,
    string Currency,
    DateTime Created,
    DateTime? CanceledAt,
    bool CancelAtPeriodEnd);

/// <summary>
/// Proration preview details from Stripe API.
/// </summary>
public record ProrationPreview(
    long AmountCents,
    string Currency,
    int DaysRemaining);
