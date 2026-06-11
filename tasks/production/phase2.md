# Phase 2 — Stripe Live-Mode & Subscription Completeness

> **Status:** Not Started  
> **Estimated effort:** 1 week  
> **Dependencies:** Phase 0 complete  
> **Blocks:** Nothing (but Phase 5 production ops assumes billing is live)

---

## Goal

Move Stripe from test-mode to live-mode, implement the two missing `StripeService` methods, and replace the fake proration calculation with a real Stripe preview. After this phase, real money can flow through the system.

---

## Why

The Stripe integration is complete in test-mode (tested with 10+ test files, all green). Two gaps prevent production billing:

1. **Configuration gap:** No live-mode Stripe keys; `STRIPE_WEBHOOK_SECRET` is missing from `.env.example` (fixed in Phase 0) but no production webhook endpoint is registered in the Stripe dashboard
2. **Code gap:** `ReactivateSubscriptionAsync` and `GetSubscriptionDetailsAsync` throw `NotImplementedException`. Subscription recovery (PastDue → Active) is impossible without `ReactivateSubscriptionAsync`. This blocks dunning flows.
3. **UX gap:** Proration endpoint returns a fake estimate. Users get incorrect pricing information on plan changes.

---

## Scope

- Set up Stripe production account and live-mode credentials
- Create products and prices for all plans (via Admin UI, not code)
- Register production webhook endpoint in Stripe dashboard
- Implement `StripeService.ReactivateSubscriptionAsync`
- Implement `StripeService.GetSubscriptionDetailsAsync`
- Replace proration estimate with real Stripe proration preview
- Configure Stripe Customer Portal branding and allowed plan changes

---

## Out of Scope

- No new billing features
- No changes to `StripeWebhookService` (already handles `checkout.session.completed`, `customer.subscription.*`, `invoice.*`)
- No changes to `CompanySubscriptionService`, `EntitlementResolver`, or `PlanService`
- No frontend billing UI changes (existing billing page already works)
- No dunning email logic (usage of reactivation is through existing admin controls)

---

## Dependencies

- **Phase 0 complete** — `STRIPE_WEBHOOK_SECRET` must be in `.env.example` before operators can configure production
- **Stripe production account** — live-mode API keys required
- **Admin UI for plan management** — already exists; use it to create products/prices

---

## Files / Components Likely Involved

| File | Task |
|---|---|
| `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs` | P2-4, P2-5, P2-6: implement missing methods |
| `OrvixFlow.Api/Controllers/BillingController.cs` | P2-6: proration endpoint update |
| `OrvixFlow.Core/Interfaces/IStripeService.cs` | Read: understand the interface contract |
| `.env.example` | P2-8: add live-mode env var documentation |
| `docker-compose.yml` | P2-8: verify Stripe env var mapping |
| `OrvixFlow.Tests/StripeWebhookTests.cs` | Reference for test patterns |
| `OrvixFlow.Tests/BillingPhase1Tests.cs` | Reference for billing test patterns |

---

## Implementation Tasks

### P2-1 — Stripe Production Account & Live-Mode Keys

This is an operational task (no code change):

- [ ] Log in to Stripe (https://dashboard.stripe.com)
- [ ] Activate account (add business details if in test mode)
- [ ] Navigate to Developers → API keys
- [ ] Copy the live `sk_live_...` secret key
- [ ] Store in production `.env` as `STRIPE_SECRET_KEY=sk_live_...` (never commit)
- [ ] Update `.env.example` comment to clarify live vs test key format:
  ```
  # Stripe API Key
  # Test mode: sk_test_...
  # Live mode: sk_live_...
  STRIPE_SECRET_KEY=sk_test_your_test_key_here
  ```

### P2-2 — Create Stripe Products and Prices

This is operational work in the Stripe dashboard AND the OrvixFlow Admin UI:

**In Stripe dashboard:**
- [ ] Create a Product: "OrvixFlow Starter"
  - Add a Monthly recurring price (e.g., $29/month)
  - Add a Yearly recurring price (e.g., $290/year)
  - Note the price IDs: `price_starter_monthly_XXX`, `price_starter_yearly_XXX`
- [ ] Create a Product: "OrvixFlow Pro" (similar)
- [ ] Free plan has no Stripe product (it is `IsFree=true` in `PlanTemplate`)

**In OrvixFlow Admin UI (after deploying with live keys):**
- [ ] Navigate to Admin → Plans
- [ ] For each paid plan: set `StripeMonthlyPriceId` and `StripeYearlyPriceId` to the Stripe price IDs
- [ ] Confirm `IsFree=false` for paid plans

**Code note:** `StripeService.GetPriceIdForPlan()` (line 222 in `StripeService.cs`) reads `plan.StripeMonthlyPriceId` / `plan.StripeYearlyPriceId` from the `PlanTemplate` entity. The Admin UI already supports setting these. No code changes needed for this task.

### P2-3 — Register Webhook Endpoint in Stripe Dashboard

Operational task:

- [ ] In Stripe dashboard: Developers → Webhooks → Add endpoint
- [ ] URL: `https://your-production-domain.com/api/billing/stripe/webhook`
- [ ] Events to listen for (minimum):
  - `checkout.session.completed`
  - `customer.subscription.created`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `invoice.paid`
  - `invoice.payment_failed`
  - `customer.subscription.trial_will_end`
- [ ] Copy the webhook signing secret: `whsec_...`
- [ ] Store as `STRIPE_WEBHOOK_SECRET=whsec_...` in production `.env`

**Verification:** After registering, send a test event from the Stripe dashboard and verify the OrvixFlow backend returns 200 (not 400).

### P2-4 — Implement StripeService.ReactivateSubscriptionAsync

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs` (line 208)

**Current state:** `throw new NotImplementedException();`

**Implementation:**

```csharp
/// <summary>
/// P2-4: Reactivates a subscription that was set to cancel at period end.
/// Clears cancel_at_period_end = true by updating the subscription.
/// Used to recover PastDue → Active or to undo a scheduled cancellation.
/// </summary>
public async Task ReactivateSubscriptionAsync(string subscriptionId)
{
    if (!_isConfigured || _subscriptionServiceClient == null)
    {
        throw new InvalidOperationException("Stripe is not configured.");
    }

    await _subscriptionServiceClient.UpdateAsync(subscriptionId, new global::Stripe.SubscriptionUpdateOptions
    {
        CancelAtPeriodEnd = false
    });

    _logger.LogInformation("Reactivated subscription {SubscriptionId}", subscriptionId);
}
```

**Note:** This method cancels a _scheduled_ cancellation (`cancel_at_period_end=false`). For a truly `PastDue` subscription that failed payment, the admin would need to take action in the Stripe dashboard or use the Stripe billing portal. The reactivation here covers the "user changed their mind about cancelling" scenario.

**Add test:** In `OrvixFlow.Tests/`, extend `StripeWebhookTests.cs` or add a new `StripeServiceTests.cs`:
```csharp
[Fact]
public async Task ReactivateSubscriptionAsync_WhenConfigured_CallsStripeUpdate()
{
    // Because we can't mock Stripe.net easily, verify:
    // - method does not throw when Stripe is configured
    // - logs the reactivation
    // Use a mock IStripeService if testing through a service that calls this
}
```

### P2-5 — Implement StripeService.GetSubscriptionDetailsAsync

**File:** `OrvixFlow.Infrastructure/Services/Stripe/StripeService.cs` (line 213)

**Current state:** `throw new NotImplementedException();`

**Implementation:**

```csharp
/// <summary>
/// P2-5: Retrieves detailed subscription information from Stripe.
/// Used by admin panel and subscription status displays.
/// </summary>
public async Task<SubscriptionDetails?> GetSubscriptionDetailsAsync(string subscriptionId)
{
    if (!_isConfigured || _subscriptionServiceClient == null)
    {
        _logger.LogWarning("GetSubscriptionDetailsAsync called but Stripe is not configured.");
        return null;
    }

    var subscription = await _subscriptionServiceClient.GetAsync(subscriptionId);
    if (subscription == null) return null;

    return new SubscriptionDetails
    {
        SubscriptionId = subscription.Id,
        Status = subscription.Status,
        CurrentPeriodStart = subscription.CurrentPeriodStart,
        CurrentPeriodEnd = subscription.CurrentPeriodEnd,
        CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
        CanceledAt = subscription.CanceledAt
    };
}
```

**Note:** `SubscriptionDetails` is a record/class already defined in `OrvixFlow.Core/Interfaces/IStripeService.cs`. Read that file to understand the expected shape before implementing. If `SubscriptionDetails` is not yet defined, add it to `IStripeService.cs`:

```csharp
public record SubscriptionDetails(
    string SubscriptionId,
    string Status,
    DateTime CurrentPeriodStart,
    DateTime CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? CanceledAt
);
```

### P2-6 — Replace Fake Proration with Real Stripe Preview

**File:** `OrvixFlow.Api/Controllers/BillingController.cs` (lines 477–514)

**Current state:** Returns `{ isEstimate: true, message: "Proration will be calculated when Stripe is integrated" }` with a fake calculation.

**Implementation:**

Replace the fake calculation with a call to Stripe's `invoice.retrieveUpcoming` API to get the real proration amount:

```csharp
[HttpGet("proration")]
public async Task<IActionResult> CalculateProration([FromQuery] Guid newPlanId)
{
    var companyId = ParseGuid("ActiveCompanyId") ?? ParseGuid("TenantId");
    if (companyId == null) return Unauthorized();

    var currentSubscription = await _subscriptionService.GetSubscriptionAsync(companyId.Value);
    var newPlan = await _planService.GetPlanByIdAsync(newPlanId);
    if (newPlan == null) return NotFound(new { error = "Plan not found" });

    // If Stripe is not configured or no external subscription, return estimate
    var externalSubId = currentSubscription?.ExternalSubscriptionId;
    var newPriceId = /* resolve from newPlan.StripeMonthlyPriceId or StripeYearlyPriceId */;

    if (string.IsNullOrEmpty(externalSubId) || string.IsNullOrEmpty(newPriceId))
    {
        // Fallback: return simple estimate for free-tier or unconfigured scenario
        return Ok(new { prorationAmount = 0, isEstimate = true, message = "No active Stripe subscription found." });
    }

    try
    {
        var invoiceService = new global::Stripe.InvoiceService();
        var upcoming = await invoiceService.RetrieveUpcomingAsync(new global::Stripe.UpcomingInvoiceOptions
        {
            Customer = currentSubscription!.ExternalCustomerId,
            Subscription = externalSubId,
            SubscriptionItems = new List<global::Stripe.InvoiceSubscriptionItemOptions>
            {
                new() { Id = /* current subscription item ID */, Deleted = true },
                new() { Price = newPriceId, Quantity = 1 }
            },
            SubscriptionProrationDate = DateTime.UtcNow
        });

        return Ok(new
        {
            prorationAmount = upcoming.AmountDue,
            currency = upcoming.Currency,
            isEstimate = false,
            daysRemaining = currentSubscription.CurrentPeriodEnd != default
                ? (int)(currentSubscription.CurrentPeriodEnd - DateTime.UtcNow).TotalDays
                : 0
        });
    }
    catch (global::Stripe.StripeException ex)
    {
        _logger.LogWarning(ex, "Could not retrieve Stripe proration preview for company {CompanyId}", companyId);
        return Ok(new { prorationAmount = 0, isEstimate = true, message = "Proration preview unavailable." });
    }
}
```

> ⚠️ **IMPORTANT:** Stripe's `UpcomingInvoice` requires knowing the `SubscriptionItem.Id` (not the subscription ID). Retrieve the current subscription using `_stripeService.GetSubscriptionDetailsAsync()` and extract the item ID. If no item ID is available, fall back gracefully (as shown).

### P2-7 — Configure Stripe Customer Portal

Operational task in Stripe dashboard:

- [ ] Navigate to Stripe dashboard → Settings → Customer portal
- [ ] Enable the portal
- [ ] Configure allowed actions:
  - Update payment method: ✅ Enabled
  - Cancel subscriptions: ✅ Enabled (cancel at period end)
  - Update subscriptions: ✅ Enabled (upgrades and downgrades)
  - Download invoices: ✅ Enabled
- [ ] Set business information (name, privacy policy URL, terms of service URL)
- [ ] Set allowed plan changes to only the publicly visible plans

### P2-8 — Add STRIPE_WEBHOOK_SECRET to All Deployment Configurations

(Phase 0 added it to `.env.example`. This task ensures it is wired through all deployment paths.)

- [ ] Verify `docker-compose.yml` already maps `Stripe__WebhookSecret: ${STRIPE_WEBHOOK_SECRET}` (it does)
- [ ] When creating `docker-compose.prod.yml` in Phase 5, ensure the same mapping is present
- [ ] Verify `appsettings.json` has the `Stripe:WebhookSecret` key in the config section (check; if missing add empty string placeholder)
- [ ] Document in Phase 5 runbook: "How to rotate Stripe webhook secret"

---

## Architecture Rules

- All Stripe SDK calls must be through `IStripeService` — do not call Stripe SDK directly from controllers
- `StripeService` uses `global::Stripe.*` namespace prefix (because `Stripe` conflicts with local namespaces)
- Do not change `StripeWebhookService` — it handles event types correctly
- New `SubscriptionDetails` type belongs in `OrvixFlow.Core/Interfaces/IStripeService.cs` as a record (not in Infrastructure or Api)
- All Stripe exceptions must be caught and handled gracefully — never let `StripeException` propagate to controller without logging
- Do not add `using Stripe;` globally — always use `global::Stripe.` prefix to avoid namespace conflicts

---

## Tests Required

### New Tests

- `StripeService_ReactivateSubscriptionAsync_WhenNotConfigured_ThrowsInvalidOperation`
- `StripeService_GetSubscriptionDetailsAsync_WhenNotConfigured_ReturnsNull`
- `BillingController_Proration_WhenNoExternalSub_ReturnsEstimate`
- `BillingController_Proration_WhenStripeNotConfigured_ReturnsEstimate`

### Existing Tests to Re-Run

```bash
dotnet test --filter "FullyQualifiedName~StripeWebhook"
dotnet test --filter "FullyQualifiedName~Billing"
dotnet test --filter "FullyQualifiedName~CompanySubscription"
dotnet test --filter "FullyQualifiedName~PlanTemplate"
# Full suite
dotnet test
```

---

## Validation Checklist

- [ ] Live-mode `sk_live_` key configured in production env
- [ ] Stripe products and prices created for all paid plans
- [ ] `PlanTemplate.StripeMonthlyPriceId` / `StripeYearlyPriceId` set in Admin UI for each paid plan
- [ ] Webhook endpoint registered in Stripe dashboard
- [ ] Test event from Stripe dashboard returns 200 from OrvixFlow
- [ ] `POST /api/billing/checkout` creates a real Stripe checkout session (live mode)
- [ ] Completing checkout in Stripe test with test card activates `CompanySubscription`
- [ ] `GET /api/billing/subscription` shows updated plan and status after webhook
- [ ] `ReactivateSubscriptionAsync` does not throw `NotImplementedException`
- [ ] `GetSubscriptionDetailsAsync` does not throw `NotImplementedException`
- [ ] Proration endpoint returns `isEstimate: false` when Stripe subscription exists
- [ ] Stripe Customer Portal opens at `GET /api/billing/portal`
- [ ] `dotnet test` — all tests pass

---

## Definition of Done

1. A test subscription can be created via live Stripe checkout
2. Stripe webhooks are received and processed correctly
3. `ReactivateSubscriptionAsync` and `GetSubscriptionDetailsAsync` are implemented
4. Proration returns real Stripe data
5. All tests pass

---

## Common Mistakes

1. **Using test-mode keys in production** — `sk_test_` and `whsec_test_` keys will not process real payments
2. **Forgetting to set price IDs in `PlanTemplate` via Admin UI** — `StripeService.GetPriceIdForPlan()` returns null if price IDs are not set, causing checkout to fail with `InvalidOperationException`
3. **Registering the wrong webhook URL** — must be the production domain URL, not localhost
4. **Not registering all required webhook event types** — if `invoice.paid` is missing, `CompanySubscription.Status` will not update correctly
5. **Using `CreateOrUpdateSubscriptionAsync`** — this method intentionally throws `NotImplementedException("Use checkout session...")`. Do not try to implement or use it. Checkout sessions are the correct flow.

---

## Handoff to Phase 3

Before Phase 3 starts, confirm:

1. Real Stripe subscriptions can be created and managed
2. All billing tests pass
3. Webhook endpoint is verified

Phase 3 (Mailbox OAuth) requires Phase 0 and Phase 1. It does NOT require Phase 2. Phase 2 and Phase 3 can run concurrently if resources allow.
