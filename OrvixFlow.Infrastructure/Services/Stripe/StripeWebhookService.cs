using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services.Stripe;

/// <summary>
/// Stripe webhook handler service (Phase 5 - minimal implementation).
/// </summary>
public class StripeWebhookService
{
    private readonly AppDbContext _dbContext;
    private readonly ICompanySubscriptionService _subscriptionService;
    private readonly ILogger<StripeWebhookService> _logger;
    private readonly string _webhookSecret;

    public StripeWebhookService(
        AppDbContext dbContext,
        ICompanySubscriptionService subscriptionService,
        ILogger<StripeWebhookService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _logger = logger;
        _webhookSecret = configuration["Stripe:WebhookSecret"] ?? string.Empty;
    }

    public async Task<bool> ProcessWebhookAsync(string payload, string signature)
    {
        if (string.IsNullOrEmpty(_webhookSecret))
        {
            _logger.LogWarning("Stripe webhook secret not configured");
            return false;
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
            return await ProcessEventAsync(stripeEvent);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Invalid Stripe webhook signature");
            return false;
        }
    }

    public async Task<bool> ProcessEventAsync(Event stripeEvent)
    {
        _logger.LogInformation("Processing {EventType}", stripeEvent.Type);

        try
        {
            switch (stripeEvent.Type)
            {
                case "invoice.paid":
                    return await HandleInvoicePaidAsync(stripeEvent);
                case "invoice.payment_failed":
                    return await HandleInvoiceFailedAsync(stripeEvent);
                case "customer.subscription.updated":
                    return true;
                default:
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {EventType}", stripeEvent.Type);
            return false;
        }
    }

    private async Task<bool> HandleInvoicePaidAsync(Event stripeEvent)
    {
        // Get invoice from event - use dynamic to avoid type resolution issues
        dynamic invoiceData = stripeEvent.Data.Object;
        string? customerId = invoiceData?.CustomerId;
        
        if (string.IsNullOrEmpty(customerId))
        {
            _logger.LogWarning("Invoice event missing customer ID");
            return true;
        }

        var subscriptions = _dbContext.CompanySubscriptions
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();

        foreach (var sub in subscriptions)
        {
            if (sub.Status != SubscriptionState.Active)
            {
                sub.Status = SubscriptionState.Active;
                sub.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        if (subscriptions.Any())
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Subscription activated for customer {CustomerId}", customerId);
        }
        
        return true;
    }

    private async Task<bool> HandleInvoiceFailedAsync(Event stripeEvent)
    {
        dynamic invoiceData = stripeEvent.Data.Object;
        string? customerId = invoiceData?.CustomerId;
        
        if (string.IsNullOrEmpty(customerId))
        {
            return true;
        }

        var subscriptions = _dbContext.CompanySubscriptions
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();

        foreach (var sub in subscriptions)
        {
            sub.Status = SubscriptionState.PastDue;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        
        if (subscriptions.Any())
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogWarning("Subscription marked PastDue for customer {CustomerId}", customerId);
        }
        
        return true;
    }
}
