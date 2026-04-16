# Stripe Configuration Plan

**Author:** OrvixFlow Planning
**Date:** 2026-04-16
**Status:** Ready for Implementation
**Based on:** Phase 5 of `ORVIXFLOW_BILLING_IMPROVEMENTS.md`

---

## Overview

This plan covers the remaining Stripe configuration steps to complete Phase 5 of the billing system improvements. The implementation includes:
- Stripe SDK integration with test mode
- Checkout and portal endpoints
- Local invoice storage populated via webhooks
- Usage alerts at 80%/100% thresholds via email

---

## Configuration Preferences

| Setting | Choice |
|---------|--------|
| Notification Channel | Email only |
| Alert Recipients | All CompanyOwner users |
| Threshold Config | Hardcoded (80% warning, 100% critical) |

---

## Implementation Phases

### Phase 1: Environment & Documentation

#### 1.1 Update `.env.example`
Add Stripe and notification configuration:

```bash
# Stripe Configuration (Test Mode)
Stripe__SecretKey=sk_test_your_test_key_here
Stripe__WebhookSecret=whsec_your_webhook_secret_here

# Stripe Price IDs (create these in Stripe Dashboard)
Stripe__Prices__Starter__Monthly=price_starter_monthly_id
Stripe__Prices__Starter__Yearly=price_starter_yearly_id
Stripe__Prices__Growth__Monthly=price_growth_monthly_id
Stripe__Prices__Growth__Yearly=price_growth_yearly_id
Stripe__Prices__Business__Monthly=price_business_monthly_id
Stripe__Prices__Business__Yearly=price_business_yearly_id
```

#### 1.2 Create Stripe Setup Guide
New file: `tasks/STRIPE_SETUP_GUIDE.md`

Contents:
- How to create Stripe test account
- How to create products and prices in Stripe Dashboard
- How to configure webhook endpoint in Stripe
- How to get API keys (test mode)
- Testing checklist

---

### Phase 2: Missing Billing Endpoints

#### 2.1 Add `IStripeService` Injection
**File:** `BillingController.cs`

Add `IStripeService` to constructor and private field.

#### 2.2 Add Checkout Endpoint
**File:** `BillingController.cs`

```csharp
[HttpPost("checkout")]
public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest req)
{
    var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
    if (companyId == null) return Unauthorized();
    if (!IsCompanyAdminOrAbove()) return Forbid();

    var successUrl = req.SuccessUrl ?? $"{Request.Scheme}://{Request.Host}/billing/success";
    var cancelUrl = req.CancelUrl ?? $"{Request.Scheme}://{Request.Host}/billing";

    var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
        companyId.Value, req.PlanTemplateId, successUrl, cancelUrl);

    return Ok(new { checkoutUrl });
}

public record CreateCheckoutRequest(Guid PlanTemplateId, string? SuccessUrl, string? CancelUrl);
```

#### 2.3 Add Portal Endpoint
**File:** `BillingController.cs`

```csharp
[HttpGet("portal")]
public async Task<IActionResult> CreatePortalSession([FromQuery] string? returnUrl = null)
{
    var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
    if (companyId == null) return Unauthorized();
    if (!IsCompanyAdminOrAbove()) return Forbid();

    var url = returnUrl ?? $"{Request.Scheme}://{Request.Host}/billing";
    var portalUrl = await _stripeService.CreatePortalSessionAsync(companyId.Value, url);

    return Ok(new { portalUrl });
}
```

#### 2.4 Add Invoices Endpoint
**File:** `BillingController.cs`

```csharp
[HttpGet("invoices")]
public async Task<IActionResult> GetInvoices()
{
    var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
    if (companyId == null) return Unauthorized();
    if (!IsCompanyAdminOrAbove()) return Forbid();

    var invoices = await _db.Invoices
        .Where(i => i.CompanyId == companyId.Value)
        .OrderByDescending(i => i.CreatedAt)
        .Select(i => new {
            i.Id,
            i.ExternalInvoiceId,
            i.AmountCents,
            i.Currency,
            i.Status,
            i.InvoicePdfUrl,
            i.InvoiceUrl,
            i.PeriodStart,
            i.PeriodEnd,
            i.PaidAt,
            i.CreatedAt
        })
        .ToListAsync();

    return Ok(invoices);
}
```

---

### Phase 3: Invoice Storage Enhancement

#### 3.1 Enhance Webhook Invoice Creation
**File:** `StripeWebhookService.cs`

Update `HandleInvoicePaidAsync` to:
1. Extract invoice details from Stripe event
2. Create `Invoice` record in database
3. Update `ExternalSubscriptionId` on subscription

Key changes:
```csharp
private async Task<bool> HandleInvoicePaidAsync(Event stripeEvent)
{
    dynamic invoiceData = stripeEvent.Data.Object;
    string? customerId = invoiceData?.CustomerId;
    string? subscriptionId = invoiceData?.Subscription;
    
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
        
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            sub.ExternalSubscriptionId = subscriptionId;
        }
    }
    
    // Create Invoice record
    if (subscriptions.Any())
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = subscriptions.First().CompanyId,
            ExternalInvoiceId = invoiceData?.Id ?? string.Empty,
            AmountCents = invoiceData?.AmountPaid ?? 0,
            Currency = invoiceData?.Currency ?? "usd",
            Status = InvoiceStatus.Paid,
            InvoicePdfUrl = invoiceData?.InvoicePdf,
            InvoiceUrl = invoiceData?.HostedInvoiceUrl,
            PeriodStart = UnixToDateTime(invoiceData?.PeriodStart),
            PeriodEnd = UnixToDateTime(invoiceData?.PeriodEnd),
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
        
        _logger.LogInformation("Subscription activated and invoice recorded for customer {CustomerId}", customerId);
    }
    
    return true;
}

private static DateTime UnixToDateTime(dynamic unixSeconds)
{
    if (unixSeconds == null) return DateTime.UtcNow;
    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
}
```

#### 3.2 Add Customer Subscription ID Update
**File:** `StripeWebhookService.cs`

Handle `customer.subscription.updated` event to update `ExternalSubscriptionId`:
```csharp
case "customer.subscription.updated":
    return await HandleSubscriptionUpdatedAsync(stripeEvent);
```

---

### Phase 4: Usage Alerts (T4-4)

#### 4.1 Create NotificationQueue Entity
**New File:** `OrvixFlow.Core/Entities/NotificationQueue.cs`

```csharp
namespace OrvixFlow.Core.Entities;

public class NotificationQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Type { get; set; } = string.Empty;  // "UsageWarning80", "UsageCritical100"
    public string Channel { get; set; } = "Email";
    public string RecipientEmail { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public decimal CurrentUsage { get; set; }
    public decimal Limit { get; set; }
    public decimal Percentage { get; set; }
    public bool Processed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    
    public Tenant Company { get; set; } = null!;
}
```

#### 4.2 Create IUsageAlertService Interface
**New File:** `OrvixFlow.Core/Interfaces/IUsageAlertService.cs`

```csharp
using System;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public interface IUsageAlertService
{
    Task CheckAndAlertAsync(Guid companyId, string metricType, decimal currentUsage, decimal limit);
    Task<bool> HasAlertBeenSentThisPeriodAsync(Guid companyId, string alertType);
}
```

#### 4.3 Create UsageAlertService
**New File:** `OrvixFlow.Infrastructure/Services/UsageAlertService.cs`

```csharp
public class UsageAlertService : IUsageAlertService
{
    private const decimal WarningThreshold = 80m;
    private const decimal CriticalThreshold = 100m;
    
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<UsageAlertService> _logger;
    
    public async Task CheckAndAlertAsync(Guid companyId, string metricType, decimal currentUsage, decimal limit)
    {
        if (limit <= 0) return;
        
        var percentage = (currentUsage / limit) * 100;
        
        if (percentage >= CriticalThreshold)
        {
            await SendAlertAsync(companyId, metricType, currentUsage, limit, percentage, "UsageCritical100");
        }
        else if (percentage >= WarningThreshold)
        {
            await SendAlertAsync(companyId, metricType, currentUsage, limit, percentage, "UsageWarning80");
        }
    }
    
    private async Task SendAlertAsync(Guid companyId, string metricType, decimal current, decimal limit, decimal percentage, string alertType)
    {
        if (await HasAlertBeenSentThisPeriodAsync(companyId, alertType))
        {
            _logger.LogDebug("Alert {AlertType} already sent for company {CompanyId} this period", alertType, companyId);
            return;
        }
        
        var owners = await _db.UserCompanyMemberships
            .Where(m => m.CompanyId == companyId && m.CompanyRole == CompanyRole.CompanyOwner)
            .Select(m => m.User.Email)
            .ToListAsync();
        
        foreach (var email in owners)
        {
            var notification = new NotificationQueue
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Type = alertType,
                Channel = "Email",
                RecipientEmail = email,
                MetricType = metricType,
                CurrentUsage = current,
                Limit = limit,
                Percentage = percentage,
                CreatedAt = DateTime.UtcNow
            };
            _db.NotificationQueues.Add(notification);
        }
        
        await _db.SaveChangesAsync();
        _logger.LogInformation("Queued {AlertType} alert for company {CompanyId}", alertType, companyId);
    }
    
    public async Task<bool> HasAlertBeenSentThisPeriodAsync(Guid companyId, string alertType)
    {
        var subscription = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId);
        
        var periodStart = subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddDays(-30);
        
        return await _db.NotificationQueues
            .AnyAsync(n => n.CompanyId == companyId 
                && n.Type == alertType 
                && n.CreatedAt >= periodStart);
    }
}
```

#### 4.4 Register Services
**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

Add:
```csharp
services.AddScoped<IUsageAlertService, UsageAlertService>();
```

#### 4.5 Add NotificationProcessorJob
**New File:** `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`

```csharp
public class NotificationProcessorJob
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationProcessorJob> _logger;
    
    public async Task ExecuteAsync()
    {
        var pending = await _db.NotificationQueues
            .Where(n => !n.Processed)
            .OrderBy(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
        
        foreach (var notification in pending)
        {
            try
            {
                await SendEmailAsync(notification);
                notification.Processed = true;
                notification.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification {Id}", notification.Id);
            }
        }
        
        await _db.SaveChangesAsync();
    }
    
    private async Task SendEmailAsync(NotificationQueue notification)
    {
        var subject = notification.Type == "UsageWarning80"
            ? "⚠️ Usage Alert: 80% Threshold Reached"
            : "🚨 Usage Alert: 100% Limit Reached";
        
        var body = $@"
            Your usage has reached {notification.Percentage:F0}% of your {notification.MetricType} limit.
            
            Current usage: {notification.CurrentUsage:N0}
            Limit: {notification.Limit:N0}
            
            Please consider upgrading your plan or reducing usage.
            
            - OrvixFlow
        ";
        
        await _emailService.SendAsync(notification.RecipientEmail, subject, body);
    }
}
```

#### 4.6 Register Hangfire Job
**File:** `OrvixFlow.Api/Program.cs`

Add recurring job:
```csharp
recurringJobManager.AddOrUpdate<NotificationProcessorJob>(
    "notification-processor",
    job => job.ExecuteAsync(),
    "*/5 * * * *");  // Every 5 minutes
```

#### 4.7 Integrate into UsageService
**File:** `OrvixFlow.Infrastructure/Shadow/UsageService.cs`

Add alert check after token recording:
```csharp
public async Task RecordTokensAsync(...)
{
    await WriteEventAsync(...);
    
    var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(companyId);
    var currentUsage = await GetUsageForMetricAsync(companyId, "ai-tokens");
    await _usageAlertService.CheckAndAlertAsync(companyId, "ai-tokens", currentUsage, entitlements.MaxMonthlyTokens);
}
```

#### 4.8 Register NotificationQueue in DbContext
**File:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs`

Add:
```csharp
public DbSet<NotificationQueue> NotificationQueues => Set<NotificationQueue>();
```

---

### Phase 5: Testing & Documentation

#### 5.1 Add Stripe Tests
**New File:** `OrvixFlow.Tests/StripeWebhookTests.cs`

Tests:
- `StripeWebhook_ValidSignature_ProcessesSuccessfully`
- `StripeWebhook_InvalidSignature_ReturnsBadRequest`
- `StripeWebhook_InvoicePaid_CreatesInvoiceRecord`
- `StripeWebhook_InvoicePaid_ActivatesSubscription`

#### 5.2 Add Usage Alert Tests
**New File:** `OrvixFlow.Tests/UsageAlertTests.cs`

Tests:
- `UsageAlert_WarningAt80Percent_QueuesNotification`
- `UsageAlert_CriticalAt100Percent_QueuesNotification`
- `UsageAlert_NoDuplicateAlerts_SamePeriod`
- `UsageAlert_SendsToAllCompanyOwners`
- `UsageAlert_NoAlert_Below80Percent`

#### 5.3 Update Documentation
- Update `memory/memory-overview.md`
- Update `tasks/ORVIXFLOW_BILLING_IMPROVEMENTS.md` - mark Phase 5 complete

---

## Files Summary

| File | Action |
|------|--------|
| `.env.example` | Add Stripe + notification config |
| `tasks/STRIPE_SETUP_GUIDE.md` | New - Stripe setup documentation |
| `BillingController.cs` | Add checkout/portal/invoices endpoints |
| `StripeWebhookService.cs` | Add Invoice creation |
| `OrvixFlow.Core/Entities/NotificationQueue.cs` | New entity |
| `OrvixFlow.Core/Interfaces/IUsageAlertService.cs` | New interface |
| `OrvixFlow.Infrastructure/Services/UsageAlertService.cs` | New service |
| `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs` | New Hangfire job |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | Add NotificationQueue DbSet |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Register services |
| `OrvixFlow.Tests/StripeWebhookTests.cs` | New test file |
| `OrvixFlow.Tests/UsageAlertTests.cs` | New test file |

---

## Implementation Order

```
1. Phase 1: Environment & Documentation
2. Phase 2: Missing Billing Endpoints
3. Phase 3: Invoice Storage Enhancement
4. Phase 4: Usage Alerts (T4-4)
   4.1 Create NotificationQueue entity
   4.2 Create IUsageAlertService interface
   4.3 Create UsageAlertService implementation
   4.4 Register services in DI
   4.5 Create NotificationProcessorJob
   4.6 Register Hangfire job
   4.7 Integrate into UsageService
   4.8 Add DbSet to AppDbContext
5. Phase 5: Testing & Documentation
```

---

## Verification Checklist

After implementation:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes (expected: 350+ tests)
- [ ] Stripe keys configured in `.env`
- [ ] Price IDs configured in `.env`
- [ ] `POST /api/billing/checkout` returns valid Stripe URL
- [ ] `GET /api/billing/portal` returns valid portal URL
- [ ] `GET /api/billing/invoices` returns invoice history
- [ ] Webhook endpoint processes `invoice.paid` events
- [ ] Invoice records created in database on payment
- [ ] Usage alerts queued when crossing 80%/100% thresholds
- [ ] NotificationProcessorJob runs every 5 minutes
- [ ] Setup guide created in `tasks/STRIPE_SETUP_GUIDE.md`
