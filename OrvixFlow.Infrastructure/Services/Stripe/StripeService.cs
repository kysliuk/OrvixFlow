using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services.Stripe;

/// <summary>
/// Stripe payment integration service (Phase 5 - minimal implementation).
/// Uses Stripe.net SDK for checkout and customer management.
/// T2-2: CreatePortalSessionAsync now makes real Stripe API call
/// T4-2: CreateCheckoutSessionAsync uses owner email from UserCompanyMembership
/// T4-4: Uses PlanTemplate.Slug for price lookup
/// </summary>
public class StripeService : IStripeService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly ICompanySubscriptionService _subscriptionService;
    private readonly IPlanService _planService;
    private readonly bool _isConfigured;

    private global::Stripe.CustomerService? _customerService;
    private global::Stripe.SubscriptionService? _subscriptionServiceClient;

    public StripeService(
        IConfiguration configuration,
        ILogger<StripeService> logger,
        AppDbContext dbContext,
        ICompanySubscriptionService subscriptionService,
        IPlanService planService)
    {
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _planService = planService;
        
        var apiKey = _configuration["Stripe:SecretKey"];
        _isConfigured = !string.IsNullOrEmpty(apiKey);
        
        if (_isConfigured)
        {
            global::Stripe.StripeConfiguration.ApiKey = apiKey;
            _customerService = new global::Stripe.CustomerService();
            _subscriptionServiceClient = new global::Stripe.SubscriptionService();
        }
    }

    public async Task<string> CreateCustomerAsync(Guid companyId, string email, string? companyName = null)
    {
        if (!_isConfigured || _customerService == null)
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        var customer = await _customerService.CreateAsync(new global::Stripe.CustomerCreateOptions
        {
            Email = email,
            Description = companyName ?? $"Company {companyId}",
            Metadata = new() { ["company_id"] = companyId.ToString() }
        });
        
        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
        if (subscription != null)
        {
            subscription.ExternalCustomerId = customer.Id;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        
        _logger.LogInformation("Created Stripe customer {CustomerId} for company {CompanyId}", customer.Id, companyId);
        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Guid companyId, 
        Guid planTemplateId, 
        string successUrl, 
        string cancelUrl)
    {
        if (!_isConfigured)
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        var plan = await _planService.GetPlanByIdAsync(planTemplateId);
        if (plan == null)
        {
            throw new PlanNotFoundException(planTemplateId);
        }

        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
        string customerId;
        
        if (subscription?.ExternalCustomerId != null)
        {
            customerId = subscription.ExternalCustomerId;
        }
        else
        {
            var tenant = await _dbContext.Tenants.FindAsync(companyId);
            if (tenant == null)
            {
                throw new CompanyNotFoundException(companyId);
            }
            
            // T4-2: Use owner email from UserCompanyMembership instead of placeholder
            var ownerEmail = await _dbContext.UserCompanyMemberships
                .IgnoreQueryFilters()
                .Where(m => m.CompanyId == companyId && m.CompanyRole == "CompanyOwner")
                .Select(m => m.User.Email)
                .FirstOrDefaultAsync() ?? $"billing+{companyId}@orvixflow.com";
            
            customerId = await CreateCustomerAsync(companyId, ownerEmail, tenant.Name);
        }

        var priceId = GetPriceIdForPlan(plan);
        if (string.IsNullOrEmpty(priceId))
        {
            throw new InvalidOperationException($"No Stripe price for plan {planTemplateId}");
        }

        var session = await new global::Stripe.Checkout.SessionService().CreateAsync(new global::Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new() { "card" },
            LineItems = new() { new() { Price = priceId, Quantity = 1 } },
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = new() 
            { 
                ["company_id"] = companyId.ToString(),
                ["plan_template_id"] = planTemplateId.ToString() 
            }
        });
        
        _logger.LogInformation("Created checkout session {SessionId}", session.Id);
        return session.Url ?? throw new InvalidOperationException("Checkout URL is null");
    }

    /// <summary>
    /// T2-2: Creates a real Stripe Customer Portal session.
    /// Previously returned a fake URL (returnUrl + "?portal=dashboard").
    /// </summary>
    public async Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl)
    {
        if (!_isConfigured)
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
        if (subscription?.ExternalCustomerId == null)
        {
            throw new SubscriptionNotFoundException(companyId);
        }

        // T2-2: Make real Stripe Customer Portal API call
        var service = new global::Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(new global::Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = subscription.ExternalCustomerId,
            ReturnUrl = returnUrl
        });

        _logger.LogInformation("Created portal session for customer {CustomerId}", subscription.ExternalCustomerId);
        return session.Url ?? throw new InvalidOperationException("Portal URL is null");
    }

    public async Task<(string customerId, string subscriptionId)> CreateOrUpdateSubscriptionAsync(
        Guid companyId, 
        string priceId, 
        string? couponCode = null)
    {
        throw new NotImplementedException("Use checkout session for new subscriptions");
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, bool cancelAtPeriodEnd = true)
    {
        if (!_isConfigured || _subscriptionServiceClient == null)
        {
            throw new InvalidOperationException("Stripe is not configured.");
        }

        await _subscriptionServiceClient.CancelAsync(subscriptionId);
        _logger.LogInformation("Cancelled subscription {SubscriptionId}", subscriptionId);
    }

    public async Task<string?> GetCustomerIdAsync(Guid companyId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
        return subscription?.ExternalCustomerId;
    }

    public async Task<string?> GetSubscriptionIdAsync(Guid companyId)
    {
        var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
        return subscription?.ExternalSubscriptionId;
    }

    public Task ReactivateSubscriptionAsync(string subscriptionId)
    {
        throw new NotImplementedException();
    }

    public Task<SubscriptionDetails?> GetSubscriptionDetailsAsync(string subscriptionId)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// T4-4: Uses PlanTemplate.Slug instead of plan.Name for price lookup.
    /// This is more stable than name-based lookup.
    /// </summary>
    private string? GetPriceIdForPlan(PlanTemplate plan)
    {
        return plan.BillingInterval switch
        {
            BillingInterval.Monthly => plan.Slug.ToLowerInvariant() switch
            {
                "free" => null,
                "starter" => _configuration["Stripe:Prices:Starter:Monthly"],
                "growth" => _configuration["Stripe:Prices:Growth:Monthly"],
                "business" => _configuration["Stripe:Prices:Business:Monthly"],
                _ => null
            },
            BillingInterval.Yearly => plan.Slug.ToLowerInvariant() switch
            {
                "free" => null,
                "starter" => _configuration["Stripe:Prices:Starter:Yearly"],
                "growth" => _configuration["Stripe:Prices:Growth:Yearly"],
                "business" => _configuration["Stripe:Prices:Business:Yearly"],
                _ => null
            },
            _ => null
        };
    }
}
