# Stripe Integration Fix Plan

**Author:** OrvixFlow Audit  
**Date:** 2026-04-17  
**Wave 1 Completed:** 2026-04-17  
**Status:** Wave 1-2 Complete — Ready for Wave 3  
**Based on:** Audit of `STRIPE_CONFIGURATION_PLAN.md`, `STRIPE_SETUP_GUIDE.md`, and last 7 commits  

---

## Context

The Phase 5 Stripe scaffolding (commits `b6b6384` and `bf059b7`) introduced three critical functional
bugs that make the webhook integration silently inoperative, plus several high-severity gaps (missing
endpoints, missing env config, missing EF migration). This plan resolves all issues in priority order.

---


## Wave 1 Completion Summary ✅

| Task | Status | Changes |
|------|--------|---------|
| T1-1: IgnoreQueryFilters | ✅ Complete | StripeWebhookService.cs |
| T1-2: Tenant Sync | ✅ Complete | ICompanySubscriptionService.cs, CompanySubscriptionService.cs |
| T1-3: EF Migration | ✅ Complete | AddInvoiceTable migration, unique index on ExternalInvoiceId |
| T1-4: Env Config | ✅ Complete | .env.example, docker-compose.yml |

**Verification:** `dotnet build` ✅ | `dotnet test` 350+ ✅ | `docker compose config` ✅

---

## Wave 2 Completion Summary ✅

| Task | Status | Changes |
|------|--------|---------|
| T2-1: Billing Endpoints | ✅ Complete | BillingController.cs - checkout, portal, invoices endpoints |
| T2-2: Real Portal Session | ✅ Complete | StripeService.cs - real BillingPortal API call |
| T2-3: subscription.updated handler | ✅ Complete | Already implemented in Wave 1 |
| T2-4: Startup Warning | ✅ Complete | Program.cs - Stripe config warnings |
| T2-5: invoice.payment_failed sync | ✅ Complete | Already implemented in Wave 1 |

**Verification:** `dotnet build` ✅ | `dotnet test` 360 ✅

## Summary of Problems Found

| Severity | Count | Areas |
|----------|-------|-------|
| Critical | 4 | Missing `IgnoreQueryFilters`, missing tenant sync, missing EF migration, missing env config |
| High | 5 | Missing endpoints, stub portal, `NotImplementedException`, missing event handlers, no startup guard |
| Medium | 4 | No idempotency, duplicate DI, `subscription.updated` no-op, `invoice.payment_failed` incomplete |
| Low | 4 | No tests, placeholder email, `InvoiceStatus` string consts, fragile plan name lookup |

---

## Wave 1 — Critical Fixes (Must complete before any Stripe traffic)

### T1-1: Add `IgnoreQueryFilters()` to `StripeWebhookService`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs`

**Problem:** `HandleInvoicePaidAsync` and `HandleInvoiceFailedAsync` query `CompanySubscriptions`
without bypassing the global tenant query filter. In a webhook context there is no authenticated
JWT user, so `_tenantProvider.GetTenantId()` returns `Guid.Empty`. The query always returns an
empty list — webhook events are silently ignored.

**Fix:** Add `.IgnoreQueryFilters()` to both queries:

```csharp
// HandleInvoicePaidAsync
var subscriptions = _dbContext.CompanySubscriptions
    .IgnoreQueryFilters()
    .Where(s => s.ExternalCustomerId == customerId)
    .ToList();

// HandleInvoiceFailedAsync
var subscriptions = _dbContext.CompanySubscriptions
    .IgnoreQueryFilters()
    .Where(s => s.ExternalCustomerId == customerId)
    .ToList();
```

**Verification:** `dotnet test`, confirm subscriptions are found for valid customer IDs.

---

### T1-2: Sync Tenant Denormalization on `invoice.paid`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs`

**Problem:** `HandleInvoicePaidAsync` sets `sub.Status = SubscriptionState.Active` but never calls
`SyncTenantDenormalizationAsync()`. `Tenant.Plan` and `Tenant.SubscriptionStatus` stay stale.
The JWT `Plan` claim will show the wrong tier after a real payment.

**Fix:** Inject `ICompanySubscriptionService` (already injected) and call sync after activating:

```csharp
foreach (var sub in subscriptions)
{
    if (sub.Status != SubscriptionState.Active)
    {
        sub.Status = SubscriptionState.Active;
        sub.UpdatedAt = DateTime.UtcNow;
    }

    // T1-2: Update period dates from Stripe event
    if (periodStart.HasValue) sub.CurrentPeriodStart = periodStart.Value;
    if (periodEnd.HasValue)   sub.CurrentPeriodEnd   = periodEnd.Value;

    // Sync ExternalSubscriptionId if provided
    if (!string.IsNullOrEmpty(subscriptionId))
        sub.ExternalSubscriptionId = subscriptionId;
}

if (subscriptions.Any())
{
    await _dbContext.SaveChangesAsync();

    // T1-2: Sync Tenant denormalized fields
    foreach (var sub in subscriptions)
        await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

    _logger.LogInformation("Subscription activated for customer {CustomerId}", customerId);
}
```

Also extract `periodStart`, `periodEnd` from the invoice object:

```csharp
long? rawStart = invoiceData?.PeriodStart;
long? rawEnd   = invoiceData?.PeriodEnd;
DateTime? periodStart = rawStart.HasValue ? DateTimeOffset.FromUnixTimeSeconds(rawStart.Value).UtcDateTime : null;
DateTime? periodEnd   = rawEnd.HasValue   ? DateTimeOffset.FromUnixTimeSeconds(rawEnd.Value).UtcDateTime   : null;
```

**Verification:** After `stripe trigger invoice.paid`, check `Tenant.Plan` and `Tenant.SubscriptionStatus` updated correctly.

---

### T1-3: Add EF Core Migration for `Invoice` Table

**Problem:** `Invoice` entity and `DbSet<Invoice> Invoices` exist in `AppDbContext` but no migration
was created. Any invoice write crashes at runtime with a table-not-found exception.

**Fix:**
```bash
dotnet ef migrations add AddInvoiceTable --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
dotnet ef database update --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
```

Also add a unique index on `ExternalInvoiceId` to support idempotency (see T3-1):

```csharp
// In AppDbContext.OnModelCreating
modelBuilder.Entity<Invoice>()
    .HasIndex(i => i.ExternalInvoiceId)
    .IsUnique();
```

**Verification:** `dotnet ef migrations list` shows `AddInvoiceTable`. `dotnet test` passes.

---

### T1-4: Add Stripe Env Vars to `.env.example` and `docker-compose.yml`

**Problem:** Phase 1.1 of `STRIPE_CONFIGURATION_PLAN.md` was never executed. Stripe secrets are
absent from `.env.example` and not mapped in `docker-compose.yml`, so Docker deployments cannot
pick them up even if the operator adds them to `.env`.

**Fix — `.env.example`:** Add section at the end:

```bash
# ─── Stripe Configuration (Test Mode) ────────────────────────────────────────
# Get keys from https://dashboard.stripe.com/test/apikeys
Stripe__SecretKey=sk_test_your_test_key_here
Stripe__WebhookSecret=whsec_your_webhook_secret_here

# Stripe Price IDs — create in Stripe Dashboard per STRIPE_SETUP_GUIDE.md
Stripe__Prices__Starter__Monthly=price_starter_monthly_id
Stripe__Prices__Starter__Yearly=price_starter_yearly_id
Stripe__Prices__Growth__Monthly=price_growth_monthly_id
Stripe__Prices__Growth__Yearly=price_growth_yearly_id
Stripe__Prices__Business__Monthly=price_business_monthly_id
Stripe__Prices__Business__Yearly=price_business_yearly_id
```

**Fix — `docker-compose.yml`:** Add to the `orvix-api` `environment:` block:

```yaml
Stripe__SecretKey: ${STRIPE_SECRET_KEY}
Stripe__WebhookSecret: ${STRIPE_WEBHOOK_SECRET}
Stripe__Prices__Starter__Monthly: ${STRIPE_PRICE_STARTER_MONTHLY}
Stripe__Prices__Starter__Yearly: ${STRIPE_PRICE_STARTER_YEARLY}
Stripe__Prices__Growth__Monthly: ${STRIPE_PRICE_GROWTH_MONTHLY}
Stripe__Prices__Growth__Yearly: ${STRIPE_PRICE_GROWTH_YEARLY}
Stripe__Prices__Business__Monthly: ${STRIPE_PRICE_BUSINESS_MONTHLY}
Stripe__Prices__Business__Yearly: ${STRIPE_PRICE_BUSINESS_YEARLY}
```

**Verification:** `docker compose config` shows the new variables. Grep confirms no Stripe secrets
appear in `appsettings.json` or `appsettings.Development.json`.

---

## Wave 2 — High Severity (Must complete before checkout flow works)

### T2-1: Implement Missing `BillingController` Endpoints

**File:** `OrvixFlow.Api/Controllers/BillingController.cs`

**Problem:** `POST /api/billing/checkout`, `GET /api/billing/portal`, and `GET /api/billing/invoices`
are described in the plan but none exist. `IStripeService` is also not injected into the controller.

**Fix:** Inject `IStripeService` and add the three endpoints:

```csharp
// Constructor addition
private readonly IStripeService _stripeService;

// POST /api/billing/checkout
[HttpPost("checkout")]
public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutRequest req)
{
    var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
    if (companyId == null) return Unauthorized();
    if (!IsCompanyAdminOrAbove()) return Forbid();

    var successUrl = req.SuccessUrl ?? $"{Request.Scheme}://{Request.Host}/billing/success";
    var cancelUrl  = req.CancelUrl  ?? $"{Request.Scheme}://{Request.Host}/billing";

    var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
        companyId.Value, req.PlanTemplateId, successUrl, cancelUrl);

    return Ok(new { checkoutUrl });
}

// GET /api/billing/portal
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

// GET /api/billing/invoices
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
            i.Id, i.ExternalInvoiceId, i.AmountCents, i.Currency,
            i.Status, i.InvoicePdfUrl, i.InvoiceUrl,
            i.PeriodStart, i.PeriodEnd, i.PaidAt, i.CreatedAt
        })
        .ToListAsync();

    return Ok(invoices);
}

public record CreateCheckoutRequest(Guid PlanTemplateId, string? SuccessUrl, string? CancelUrl);
```

**Verification:** `dotnet build` succeeds. Endpoints appear in Swagger.

---

### T2-2: Implement `CreatePortalSessionAsync` (remove stub)

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs`

**Problem:** Returns `returnUrl + "?portal=dashboard"` — a fake URL. No Stripe API call is made.

**Fix:** Call `BillingPortalSessionService`:

```csharp
public async Task<string> CreatePortalSessionAsync(Guid companyId, string returnUrl)
{
    if (!_isConfigured)
        throw new InvalidOperationException("Stripe is not configured.");

    var subscription = await _subscriptionService.GetSubscriptionAsync(companyId);
    if (subscription?.ExternalCustomerId == null)
        throw new SubscriptionNotFoundException(companyId);

    var service = new Stripe.BillingPortal.SessionService();
    var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
    {
        Customer  = subscription.ExternalCustomerId,
        ReturnUrl = returnUrl
    });

    return session.Url ?? throw new InvalidOperationException("Portal URL is null");
}
```

**Verification:** `stripe trigger customer.subscription.updated` then verify portal URL is a valid
Stripe-hosted URL.

---

### T2-3: Handle `customer.subscription.updated` and `customer.subscription.deleted`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs`

**Problem:** Both events return `true` without doing anything. Self-service plan changes via Stripe
portal never sync to the internal DB. Stripe-side cancellations leave subscriptions permanently Active.

**Fix:**

```csharp
case "customer.subscription.updated":
    return await HandleSubscriptionUpdatedAsync(stripeEvent);
case "customer.subscription.deleted":
    return await HandleSubscriptionDeletedAsync(stripeEvent);
```

```csharp
private async Task<bool> HandleSubscriptionUpdatedAsync(Event stripeEvent)
{
    dynamic subData = stripeEvent.Data.Object;
    string? customerId     = subData?.CustomerId;
    string? stripeStatus   = subData?.Status;   // "active", "past_due", "canceled", "trialing"
    string? subscriptionId = subData?.Id;

    if (string.IsNullOrEmpty(customerId)) return true;

    var subs = _dbContext.CompanySubscriptions
        .IgnoreQueryFilters()
        .Where(s => s.ExternalCustomerId == customerId)
        .ToList();

    if (!subs.Any()) return true;

    var mappedStatus = stripeStatus switch
    {
        "active"   => SubscriptionState.Active,
        "past_due" => SubscriptionState.PastDue,
        "trialing" => SubscriptionState.Trialing,
        "canceled" => SubscriptionState.Cancelled,
        _          => (SubscriptionState?)null
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

    if (string.IsNullOrEmpty(customerId)) return true;

    var subs = _dbContext.CompanySubscriptions
        .IgnoreQueryFilters()
        .Where(s => s.ExternalCustomerId == customerId)
        .ToList();

    foreach (var sub in subs)
    {
        sub.Status    = SubscriptionState.Cancelled;
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
```

**Verification:** `stripe trigger customer.subscription.deleted` → subscription becomes `Cancelled`.

---

### T2-4: Add Startup Warning for Missing Stripe Secret

**File:** `OrvixFlow.Api/Program.cs`

**Problem:** If `Stripe:WebhookSecret` is absent, inbound webhook events log a warning at
request-time but there is no visible boot-time alert. An unconfigured deployment silently
accepts webhook calls and returns 400.

**Fix:** Add after service registration (same pattern as F-18 virus scan warning):

```csharp
var webhookSecret = app.Configuration["Stripe:WebhookSecret"];
if (string.IsNullOrEmpty(webhookSecret))
{
    app.Logger.LogWarning(
        "SECURITY: Stripe:WebhookSecret is not configured. " +
        "Stripe webhook endpoint will reject all requests. " +
        "Configure Stripe__WebhookSecret in environment.");
}

var stripeKey = app.Configuration["Stripe:SecretKey"];
if (string.IsNullOrEmpty(stripeKey))
{
    app.Logger.LogWarning(
        "Stripe:SecretKey is not configured. " +
        "Checkout and portal endpoints will throw InvalidOperationException.");
}
```

**Also update `memory-risks.md`:** Replace the stale entry documenting the webhook as
`[Authorize(Policy = "SuperAdminOnly")]`. Document the current `[AllowAnonymous]` + signature
validation design with correct rationale.

---

### T2-5: Fix `invoice.payment_failed` — sync tenant and log clearly

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs`

**Problem:** `HandleInvoiceFailedAsync` changes status to `PastDue` but does not sync
`Tenant.SubscriptionStatus`. Company owners are not notified.

**Fix:** Call `SyncTenantDenormalizationAsync()` after marking `PastDue`:

```csharp
if (subscriptions.Any())
{
    await _dbContext.SaveChangesAsync();

    foreach (var sub in subscriptions)
        await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

    _logger.LogWarning(
        "Subscription marked PastDue for customer {CustomerId}. " +
        "Company owner notification pending (Phase 4 usage alerts).",
        customerId);
}
```

---

## Wave 3 — Medium Severity (Before production hardening)

### T3-1: Add Idempotency to Webhook Handlers

**Problem:** Stripe guarantees at-least-once delivery. A duplicate `invoice.paid` will create
a duplicate `Invoice` record once Phase 3 invoice storage is active.

**Fix:** Use the unique index on `Invoice.ExternalInvoiceId` (added in T1-3) as a guard:

```csharp
// In HandleInvoicePaidAsync, before creating Invoice record:
string externalInvoiceId = invoiceData?.Id ?? string.Empty;
var existing = await _dbContext.Invoices
    .IgnoreQueryFilters()
    .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId);

if (existing)
{
    _logger.LogInformation("Duplicate invoice.paid event {InvoiceId} — skipped", externalInvoiceId);
    return true;
}
```

---

### T3-2: Remove Duplicate DI Registrations

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

**Problem:** `IStripeService` and `StripeWebhookService` are registered twice (lines 44–45 and 174–175).

**Fix:** Remove the second pair of registrations (lines ~174–175). Keep only the first pair.

**Verification:** `dotnet build` succeeds. `dotnet test` passes.

---

### T3-3: Add Invoice Storage to `HandleInvoicePaidAsync`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeWebhookService.cs`

**Depends on:** T1-3 (migration), T3-1 (idempotency guard)

**Fix:** After activating subscription, create Invoice record:

```csharp
if (subscriptions.Any())
{
    string externalInvoiceId = invoiceData?.Id ?? string.Empty;

    if (!await _dbContext.Invoices.IgnoreQueryFilters()
            .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId))
    {
        var invoice = new Invoice
        {
            CompanyId         = subscriptions.First().CompanyId,
            ExternalInvoiceId = externalInvoiceId,
            AmountCents       = (int)(invoiceData?.AmountPaid ?? 0),
            Currency          = invoiceData?.Currency ?? "usd",
            Status            = InvoiceStatus.Paid,
            InvoicePdfUrl     = invoiceData?.InvoicePdf,
            InvoiceUrl        = invoiceData?.HostedInvoiceUrl,
            PeriodStart       = periodStart ?? DateTime.UtcNow,
            PeriodEnd         = periodEnd   ?? DateTime.UtcNow,
            PaidAt            = DateTime.UtcNow,
            CreatedAt         = DateTime.UtcNow,
            UpdatedAt         = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();
    }
}
```

---

### T3-4: Implement Phase 4 — Usage Alerts

**Depends on:** New `NotificationQueue` entity, `IUsageAlertService`, `UsageAlertService`,
`NotificationProcessorJob` — all as described in `STRIPE_CONFIGURATION_PLAN.md` Phase 4
(sections 4.1–4.8). Implementation is unambiguous from the plan; only the coding work is absent.

A new EF migration will be required:
```bash
dotnet ef migrations add AddNotificationQueue --project OrvixFlow.Infrastructure --startup-project OrvixFlow.Api
```

---

## Wave 4 — Low Severity / Polish

### T4-1: Write `StripeWebhookTests.cs`

**File:** `OrvixFlow.Tests/StripeWebhookTests.cs` (new)

Minimum required tests:
- `StripeWebhook_MissingSecret_ReturnsFalse`
- `StripeWebhook_InvoicePaid_ActivatesSubscription`
- `StripeWebhook_InvoicePaid_SyncsTenant`
- `StripeWebhook_InvoicePaid_CreatesInvoiceRecord`
- `StripeWebhook_InvoicePaid_Idempotent_DuplicateSkipped`
- `StripeWebhook_InvoiceFailed_MarksPastDue`
- `StripeWebhook_SubscriptionDeleted_CancelsSubscription`

Note: Stripe signature validation uses `EventUtility.ConstructEvent` which requires a real HMAC
computation. Either extract a `IStripeEventParser` seam for mocking, or use the Stripe test
helper to generate valid signed payloads.

---

### T4-2: Use Owner Email in `CreateCustomerAsync`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs`

**Problem:** New Stripe customers are created with `"billing@example.com"` as placeholder email.

**Fix:** Resolve the `CompanyOwner` email from `UserCompanyMembership`:

```csharp
var ownerEmail = await _dbContext.UserCompanyMemberships
    .IgnoreQueryFilters()
    .Where(m => m.CompanyId == companyId && m.CompanyRole == CompanyRole.CompanyOwner)
    .Select(m => m.User.Email)
    .FirstOrDefaultAsync() ?? $"billing+{companyId}@orvixflow.com";

customerId = await CreateCustomerAsync(companyId, ownerEmail, tenant.Name);
```

---

### T4-3: Convert `InvoiceStatus` to an Enum

**File:** `OrvixFlow.Core/Entities/Invoice.cs`

**Problem:** `InvoiceStatus` is a `static class` with `string` constants. Violates L1 lesson
(domain constants repeated 3+ times → enum).

**Fix:** Convert to `enum InvoiceStatus` with `[JsonConverter]` or stored as string via EF value
conversion — consistent with how `SubscriptionState` and `BillingInterval` are handled.

---

### T4-4: Use `PlanTemplate.Slug` in `GetPriceIdForPlan`

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs`

**Problem:** `GetPriceIdForPlan` uses `plan.Name.ToLowerInvariant()` switch — fragile to name
changes. `PlanTemplate.Slug` is the stable, lowercase identifier designed exactly for this.

**Fix:**

```csharp
private string? GetPriceIdForPlan(PlanTemplate plan)
{
    return plan.BillingInterval switch
    {
        BillingInterval.Monthly => plan.Slug switch
        {
            "free"     => null,
            "starter"  => _configuration["Stripe:Prices:Starter:Monthly"],
            "growth"   => _configuration["Stripe:Prices:Growth:Monthly"],
            "business" => _configuration["Stripe:Prices:Business:Monthly"],
            _          => null
        },
        BillingInterval.Yearly => plan.Slug switch
        {
            "free"     => null,
            "starter"  => _configuration["Stripe:Prices:Starter:Yearly"],
            "growth"   => _configuration["Stripe:Prices:Growth:Yearly"],
            "business" => _configuration["Stripe:Prices:Business:Yearly"],
            _          => null
        },
        _ => null
    };
}
```

---

## Files Changed Summary

| File | Action | Task |
|------|--------|------|
| `StripeWebhookService.cs` | Add `IgnoreQueryFilters()`, tenant sync, period sync, new event handlers, invoice creation | T1-1, T1-2, T2-3, T2-5, T3-1, T3-3 |
| `.env.example` | Add Stripe env var block | T1-4 |
| `docker-compose.yml` | Map Stripe env vars in `orvix-api` environment | T1-4 |
| `EF Migration` | `AddInvoiceTable` with unique index on `ExternalInvoiceId` | T1-3 |
| `BillingController.cs` | Inject `IStripeService`; add checkout/portal/invoices endpoints | T2-1 |
| `StripeService.cs` | Fix `CreatePortalSessionAsync`; fix email; use slug in price lookup | T2-2, T4-2, T4-4 |
| `Program.cs` | Startup warnings for missing Stripe config | T2-4 |
| `memory-risks.md` | Update stale webhook auth documentation | T2-4 |
| `DependencyInjection.cs` | Remove duplicate DI registrations | T3-2 |
| `NotificationQueue.cs` | New entity | T3-4 |
| `IUsageAlertService.cs` | New interface | T3-4 |
| `UsageAlertService.cs` | New service | T3-4 |
| `NotificationProcessorJob.cs` | New Hangfire job | T3-4 |
| `AppDbContext.cs` | Add `NotificationQueues` DbSet | T3-4 |
| `EF Migration` | `AddNotificationQueue` | T3-4 |
| `Invoice.cs` | Convert `InvoiceStatus` to enum | T4-3 |
| `OrvixFlow.Tests/StripeWebhookTests.cs` | New test file | T4-1 |

---

## Implementation Order

```
Wave 1 (all must land together — atomic):
  T1-1  IgnoreQueryFilters in webhook service
  T1-2  Tenant sync on invoice.paid
  T1-3  EF migration for Invoice table
  T1-4  Stripe vars in .env.example + docker-compose

Wave 2 (after Wave 1):
  T2-1  Checkout / portal / invoices endpoints
  T2-2  Real portal session (remove stub)
  T2-3  subscription.updated + subscription.deleted handlers
  T2-4  Startup warning + memory-risks.md update
  T2-5  invoice.payment_failed tenant sync

Wave 3 (after Wave 2):
  T3-1  Idempotency guard on invoice create
  T3-2  Remove duplicate DI registrations
  T3-3  Invoice record creation in HandleInvoicePaidAsync
  T3-4  Phase 4 usage alerts (NotificationQueue, service, job, migration)

Wave 4 (polish — any order):
  T4-1  StripeWebhookTests.cs
  T4-2  Owner email in CreateCustomerAsync
  T4-3  InvoiceStatus → enum
  T4-4  Use PlanTemplate.Slug in GetPriceIdForPlan
```

---

## Verification Checklist

After all waves:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes (expected: 350+ tests)
- [ ] Stripe keys configured in `.env`
- [ ] `stripe trigger invoice.paid` → subscription becomes `Active`, `Tenant.Plan` synced, Invoice record created
- [ ] `stripe trigger invoice.payment_failed` → subscription becomes `PastDue`, `Tenant.SubscriptionStatus` synced
- [ ] `stripe trigger customer.subscription.deleted` → subscription becomes `Cancelled`
- [ ] `POST /api/billing/checkout` returns real Stripe checkout URL
- [ ] `GET /api/billing/portal` returns real Stripe portal URL
- [ ] `GET /api/billing/invoices` returns invoice history
- [ ] Duplicate `invoice.paid` event is idempotent (no duplicate Invoice created)
- [ ] Boot log shows warning when `Stripe:SecretKey` or `Stripe:WebhookSecret` absent
- [ ] `memory-risks.md` reflects current webhook design (AllowAnonymous + signature validation)
- [ ] `docker compose config` shows all Stripe env vars mapped to `orvix-api`
