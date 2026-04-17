using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services.Stripe;

/// <summary>
/// Stripe webhook handler service.
/// T1-1: Added IgnoreQueryFilters for webhook context (no JWT user)
/// T1-2: Added tenant sync after subscription status changes
/// T3-1: Added idempotency guard for duplicate invoice events
/// T3-3: Added Invoice record creation on invoice.paid
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
                    return await HandleSubscriptionUpdatedAsync(stripeEvent);
                case "customer.subscription.deleted":
                    return await HandleSubscriptionDeletedAsync(stripeEvent);
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
        string? subscriptionId = invoiceData?.Subscription;
        string externalInvoiceId = invoiceData?.Id ?? string.Empty;
        
        // T1-2: Extract period dates from invoice
        long? rawStart = invoiceData?.PeriodStart;
        long? rawEnd = invoiceData?.PeriodEnd;
        DateTime? periodStart = rawStart.HasValue 
            ? DateTimeOffset.FromUnixTimeSeconds(rawStart.Value).UtcDateTime 
            : null;
        DateTime? periodEnd = rawEnd.HasValue 
            ? DateTimeOffset.FromUnixTimeSeconds(rawEnd.Value).UtcDateTime 
            : null;
        
        if (string.IsNullOrEmpty(customerId))
        {
            _logger.LogWarning("Invoice event missing customer ID");
            return true;
        }

        // T1-1: Use IgnoreQueryFilters() to bypass tenant filter in webhook context
        // Webhook has no authenticated user, so _tenantProvider.GetTenantId() returns Guid.Empty
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();

        foreach (var sub in subscriptions)
        {
            if (sub.Status != SubscriptionState.Active)
            {
                sub.Status = SubscriptionState.Active;
                sub.UpdatedAt = DateTime.UtcNow;
            }

            // T1-2: Update period dates from Stripe event
            if (periodStart.HasValue) sub.CurrentPeriodStart = periodStart.Value;
            if (periodEnd.HasValue) sub.CurrentPeriodEnd = periodEnd.Value;

            // Sync ExternalSubscriptionId if provided
            if (!string.IsNullOrEmpty(subscriptionId))
                sub.ExternalSubscriptionId = subscriptionId;
        }
        
        if (subscriptions.Any())
        {
            await _dbContext.SaveChangesAsync();

            // T1-2: Sync Tenant denormalized fields (Plan, SubscriptionStatus)
            foreach (var sub in subscriptions)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

            // T3-1: Idempotency guard - check for duplicate invoice before creating
            // T3-3: Create Invoice record for the payment
            if (!string.IsNullOrEmpty(externalInvoiceId))
            {
                var existingInvoice = await _dbContext.Invoices
                    .IgnoreQueryFilters()
                    .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId);

                if (!existingInvoice)
                {
                    var invoice = new OrvixFlow.Core.Entities.Invoice
                    {
                        CompanyId = subscriptions.First().CompanyId,
                        ExternalInvoiceId = externalInvoiceId,
                        AmountCents = (int)(invoiceData?.AmountPaid ?? 0),
                        Currency = invoiceData?.Currency?.ToUpperInvariant() ?? "USD",
                        Status = InvoiceStatus.Paid,
                        InvoicePdfUrl = invoiceData?.InvoicePdf,
                        InvoiceUrl = invoiceData?.HostedInvoiceUrl,
                        PeriodStart = periodStart ?? DateTime.UtcNow,
                        PeriodEnd = periodEnd ?? DateTime.UtcNow,
                        PaidAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.Invoices.Add(invoice);
                    await _dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation(
                        "Created Invoice record {InvoiceId} for customer {CustomerId}",
                        externalInvoiceId, customerId);
                }
                else
                {
                    _logger.LogInformation(
                        "Duplicate invoice.paid event {InvoiceId} — skipped (idempotency)",
                        externalInvoiceId);
                }
            }

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

        // T1-1: Use IgnoreQueryFilters() for webhook context
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
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

            // T1-2: Sync Tenant denormalized fields
            foreach (var sub in subscriptions)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

            _logger.LogWarning(
                "Subscription marked PastDue for customer {CustomerId}. " +
                "Company owner notification pending (Phase 4 usage alerts).",
                customerId);
        }
        
        return true;
    }

    private async Task<bool> HandleSubscriptionUpdatedAsync(Event stripeEvent)
    {
        dynamic subData = stripeEvent.Data.Object;
        string? customerId = subData?.CustomerId;
        string? stripeStatus = subData?.Status; // "active", "past_due", "canceled", "trialing"
        string? subscriptionId = subData?.Id;

        if (string.IsNullOrEmpty(customerId))
        {
            return true;
        }

        // T1-1: Use IgnoreQueryFilters() for webhook context
        var subs = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();

        if (!subs.Any())
        {
            return true;
        }

        var mappedStatus = stripeStatus switch
        {
            "active" => SubscriptionState.Active,
            "past_due" => SubscriptionState.PastDue,
            "trialing" => SubscriptionState.Trialing,
            "canceled" => SubscriptionState.Cancelled,
            _ => (SubscriptionState?)null
        };

        foreach (var sub in subs)
        {
            if (mappedStatus.HasValue) sub.Status = mappedStatus.Value;
            if (!string.IsNullOrEmpty(subscriptionId)) sub.ExternalSubscriptionId = subscriptionId;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        foreach (var sub in subs)
            await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

        return true;
    }

    private async Task<bool> HandleSubscriptionDeletedAsync(Event stripeEvent)
    {
        dynamic subData = stripeEvent.Data.Object;
        string? customerId = subData?.CustomerId;

        if (string.IsNullOrEmpty(customerId))
        {
            return true;
        }

        // T1-1: Use IgnoreQueryFilters() for webhook context
        var subs = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();

        foreach (var sub in subs)
        {
            sub.Status = SubscriptionState.Cancelled;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        if (subs.Any())
        {
            await _dbContext.SaveChangesAsync();
            foreach (var sub in subs)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);
        }

        return true;
    }
}
