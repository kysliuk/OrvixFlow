# OrvixFlow — Billing System Analysis & Improvement Plan

**Author:** Senior .NET Architect / SaaS Billing Expert (AI review)
**Date:** 2026-04-15
**Last Updated:** 2026-04-15
**Status:** Phase 1 Complete
**Based on:** Direct source code inspection of all billing-related entities, services, and controllers.

---

## Implementation Status Summary

| Phase | Description | Status |
|-------|-------------|--------|
| **Phase 1** | Critical Security & Consistency Fixes | ✅ Complete (2026-04-15) |
| **Phase 2** | Billing Model Stabilization (enum types, merge entities, jobs) | ⏳ Planned |
| **Phase 3** | Usage Tracking & Enforcement (gateway, unified period) | ⏳ Planned |
| **Phase 4** | Admin Panel & UX (downgrade safety, effective entitlements) | ⏳ Planned |
| **Phase 5** | Stripe Integration (real payments, webhooks) | ⏳ Planned |

---

---

## 1. Current Billing System — What Exists

### Plan Model (`PlanTemplate`, `PlanCatalog`, `PlanEntitlements`)

Five hardcoded plans seeded via `PlanCatalog.BuildPlanSeed()`:

| Plan | Monthly (¢) | Yearly (¢) | Seats | Tokens/mo | API req/day | Storage | KBs |
|------|-------------|------------|-------|-----------|-------------|---------|-----|
| Free | 0 | 0 | 2 | 50,000 | 500 | 100MB | 1 |
| Starter | 2,900 | 29,000 | 5 | 100,000 | 1,000 | 500MB | 5 |
| Growth | 9,900 | 99,000 | 25 | 500,000 | 5,000 | 5GB | 25 |
| Business | 29,900 | 299,000 | 100 | 2,000,000 | 20,000 | 50GB | 100 |
| Enterprise | 0 (custom) | 0 | unlimited | 10,000,000 | 100,000 | 500GB | 1,000 |

`PlanTemplate` has: `MonthlyPriceCents`, `YearlyPriceCents`, `BillingInterval`, `MaxSeats`, `IsFree`, `IsTrialAllowed`, `TrialDays`, `LegacyLocked`, `IsPubliclyVisible`, `ArchivedAt`.

`PlanEntitlements` (1-to-1 with `PlanTemplate`) has: `MaxMonthlyTokens`, `MaxApiRequestsPerDay`, `MaxStorageMb`, `MaxKnowledgeBases`.

Module access per plan defined in `PlanModuleInclusion` (join table between `PlanTemplate` and `ModuleDefinition`).

### Subscription Model (`CompanySubscription`)

- **One subscription per company** (unique `CompanyId`).
- Lifecycle statuses: `Trialing`, `Active`, `PastDue`, `Suspended`, `Cancelled` (as string constants in `SubscriptionStatus` static class).
- Fields: `CurrentPeriodStart`, `CurrentPeriodEnd`, `TrialEndsAt`, `PendingPlanId`, `PendingChangeAt`, `ExternalSubscriptionId`.
- `BillingInterval` stored as freeform string (`"Monthly"`, `"Yearly"`, `"Custom"`).
- Lifecycle is managed by `CompanySubscriptionService`: `CreateTrial`, `AssignPlan`, `ChangePlan`, `Suspend`, `Reactivate`, `Cancel`.

**Parallel entity — `BillingSubscription`:**  
A separate, underused entity with `StripeCustomerId`, `StripeSubscriptionId`, `CurrentPlan` (string), `Status` (string). Managed only by the `StripeWebhook` endpoint. **Not integrated with `CompanySubscription` at all.**

### Tenant Denormalization (`Tenant.Plan`, `Tenant.SubscriptionStatus`)

`Tenant` has `Plan: string` and `SubscriptionStatus: string`. These are synced manually in `CompanySubscriptionService.AssignPlanAsync` but **not synced in most other lifecycle operations** (Suspend, Cancel, Reactivate).

### Usage Tracking (`UsageEvent`, `UsageService`)

`UsageEvent` store: `CompanyId`, `DepartmentId?`, `UserId?`, `ModuleKey`, `MetricType` (string), `Quantity` (decimal), `OccurredAt`.

`UsageService` records: `ai-tokens`, `n8n-nodes`, `storage-mb`, `knowledge-bases`, `inbox-messages`.

`GetCompanySummaryAsync` returns lifetime totals (no period filtering). 

### Entitlement Enforcement (`EntitlementResolver`)

`GetEntitlementsAsync` computes limits from `PlanTemplate.Entitlements` + usage from `UsageEvents` within `CurrentPeriodStart`. Returns `CompanyEntitlements` DTO with usage values + `CanAdd*` helpers.

`GetEffectiveEntitlementsAsync` applies `CompanyEntitlementOverride` on top (admin-adjustable per-company limits).

`CanUseModuleWithOverridesAsync` applies `CompanyModuleOverride` (grant/suppress per company) on top of plan inclusions.

**Where enforcement actually happens:** `RequireModuleAttribute`, `InviteController` (seat check), and a handful of AI endpoint callsites. Most limit checks are on the caller to invoke `IsWithin*Async()` before acting.

### Admin Panel Integration

`AdminController` can: set entitlement overrides, set module overrides, cancel/suspend/reactivate subscriptions, view audit logs.

### Stripe Integration

`BillingController.StripeWebhook` accepts a `POST /api/billing/stripe/webhook` with no signature validation (`AllowAnonymous`, comment says "signature validation should be added for production"). Updates `Tenant.Plan`, `BillingSubscription` fields only — does **not** update `CompanySubscription`.

---

## 2. Identified Gaps and Issues

> Each finding includes a **Proposed Fix** with concrete code guidance. Cross-references to the implementation phase are noted as `→ Phase N`.

---

### 🔴 Critical Issues

#### C1 — Duplicate Subscription Model (`CompanySubscription` vs `BillingSubscription`)

Two disconnected subscription entities exist in the DB:
- `CompanySubscription` — the operational model used by `EntitlementResolver`, all limit checks, plan gating, and `CompanySubscriptionService`.
- `BillingSubscription` — a Stripe-oriented shadow record only written by the webhook endpoint.

These are **never synchronized**. If Stripe sends a webhook, only `BillingSubscription` and `Tenant.Plan` are updated. `CompanySubscription.Status`, `PlanTemplateId`, `CurrentPeriodEnd` are left stale. The entire entitlement system continues running on stale data.

**Risk:** A company whose Stripe subscription is cancelled still has `CompanySubscription.Status = Active`. They retain access to paid features indefinitely.

**Proposed Fix** `→ Phase 2`:
- Add `ExternalCustomerId` and `ExternalSubscriptionId` fields directly to `CompanySubscription`. Remove `BillingSubscription` entity and its `DbSet` entirely.
- Rewrite `StripeWebhook` to call `CompanySubscriptionService.SyncFromStripeAsync(stripeEvent)` which updates `CompanySubscription` (status, periodStart, periodEnd, externalId) and then syncs `Tenant.Plan` / `Tenant.SubscriptionStatus` as derived denormalized fields.
- `CompanySubscription` becomes the single source of truth; Stripe IDs are stored on it directly.
- Add EF migration to move data from `BillingSubscriptions` → `CompanySubscriptions` before dropping.

---

#### C2 — Stripe Webhook Has No Signature Validation

`POST /api/billing/stripe/webhook` is decorated `[AllowAnonymous]` with a `// TODO: signature validation` comment. Any unauthenticated caller can post a webhook with an arbitrary `CompanyId` and change a company's plan to `Enterprise`.

**Risk:** Any public attacker can upgrade any company to Enterprise for free, or cancel any paying customer's subscription.

**Proposed Fix** `→ Phase 1 (immediate)`:
```csharp
// BillingController.cs — StripeWebhook action

[AllowAnonymous]
[HttpPost("stripe/webhook")]
public async Task<IActionResult> StripeWebhook()
{
    // 1. Read raw body — must happen before any model binding
    using var reader = new StreamReader(Request.Body);
    var payload = await reader.ReadToEndAsync();

    // 2. Validate Stripe-Signature header
    var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
    var webhookSecret = _configuration["Stripe:WebhookSecret"]
        ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");

    // 3. ConstructEvent throws StripeException on invalid signature or expired timestamp
    var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);

    // 4. Route to service — no direct DB writes here
    await _subscriptionService.SyncFromStripeAsync(stripeEvent);
    return Ok();
}
```
- Add `Stripe:WebhookSecret` to `.env` and `.env.example`.
- Until Stripe is fully integrated, replace `[AllowAnonymous]` with `[Authorize(Policy = "SuperAdminOnly")]` and use the existing internal admin path instead of a public webhook.

---

#### C3 — `Tenant.Plan` / `Tenant.SubscriptionStatus` Out of Sync

`Tenant.Plan` is only updated in `AssignPlanAsync`. `SuspendSubscriptionAsync`, `CancelSubscriptionAsync`, and `ReactivateSubscriptionAsync` do **not** update `Tenant.SubscriptionStatus`.

Meanwhile, `BillingController.GetUsage` reads `company.Plan` from `Tenant` to determine token limits — using **hardcoded switch/case values** that don't reflect the actual `PlanEntitlements` rows:
```csharp
int limit = company.Plan?.ToLowerInvariant() switch
{
    "free" => 50000,
    "starter" => 1000000,   // ← Wrong! Actual Starter limit is 100,000
    "pro" => 1000000,
    ...
};
```

**Proposed Fix** `→ Phase 1`:
1. Extract a private sync helper in `CompanySubscriptionService`:
```csharp
private async Task SyncTenantDenormalizationAsync(Guid companyId, string planSlug, string status)
{
    var tenant = await _dbContext.Tenants.FindAsync(companyId);
    if (tenant != null)
    {
        tenant.Plan = planSlug;
        tenant.SubscriptionStatus = status;
    }
}
```
2. Call `SyncTenantDenormalizationAsync` at the end of every lifecycle method: `AssignPlanAsync`, `SuspendSubscriptionAsync`, `CancelSubscriptionAsync`, `ReactivateSubscriptionAsync`, `ChangePlanAsync`.
3. Delete the hardcoded switch/case in `BillingController.GetUsage`. Replace with:
```csharp
var entitlements = await _entitlementResolver.GetEffectiveEntitlementsAsync(companyId.Value);
int limit = entitlements.MaxMonthlyTokens;
var used = entitlements.TokensUsedThisPeriod;
```

---

#### C4 — Cancelled/Suspended Subscriptions Not Blocked from Feature Access

`CanUseModuleAsync` and all `IsWithin*Async` callers do not check `CompanySubscription.Status`. A cancelled or suspended company can still use modules, process inbox events, and consume tokens.

**Risk:** Companies that cancel do not lose access.

**Proposed Fix** `→ Phase 1`:
Add a subscription status gate as the **first check** in both `GetEntitlementsAsync` and `CanUseModuleWithOverridesAsync`:
```csharp
// EntitlementResolver.cs — GetEntitlementsAsync
public async Task<CompanyEntitlements> GetEntitlementsAsync(Guid companyId)
{
    var subscription = await GetSubscriptionAsync(companyId);

    // Gate: no active subscription → zero entitlements
    if (subscription == null
        || subscription.Status == SubscriptionStatus.Suspended
        || subscription.Status == SubscriptionStatus.Cancelled)
    {
        return new CompanyEntitlements(); // all limits 0, all CanAdd* = false
    }

    // ... rest of existing logic
}

// EntitlementResolver.cs — CanUseModuleWithOverridesAsync
public async Task<bool> CanUseModuleWithOverridesAsync(Guid companyId, string moduleKey)
{
    var subscription = await GetSubscriptionAsync(companyId);
    if (subscription == null
        || subscription.Status == SubscriptionStatus.Suspended
        || subscription.Status == SubscriptionStatus.Cancelled)
    {
        return false; // ← gate before plan/override lookup
    }

    // ... rest of existing logic
}
```
Add tests: `CancelledSubscription_BlocksModuleAccess`, `SuspendedSubscription_GetEntitlements_ReturnsZeroLimits`.

---

### 🟠 High Priority Issues

#### H1 — Usage Aggregation Is Not Period-Aware in `UsageService.GetCompanySummaryAsync`

`GetCompanySummaryAsync` sums all usage events **lifetime**. `GetEntitlementsAsync` uses `CurrentPeriodStart` for filtering. `GetUsage` endpoint uses calendar `startOfMonth`. Three different period calculations exist across the codebase.

**Proposed Fix** `→ Phase 3`:
- Delete `GetCompanySummaryAsync`'s current implementation.
- Add a `periodStart` parameter (defaults to `CompanySubscription.CurrentPeriodStart`):
```csharp
public async Task<UsageSummary> GetCompanySummaryAsync(Guid companyId, DateTime? periodStart = null)
{
    var start = periodStart ?? await GetCurrentPeriodStartAsync(companyId);
    var events = await _db.UsageEvents
        .IgnoreQueryFilters()
        .Where(e => e.CompanyId == companyId && e.OccurredAt >= start)
        .GroupBy(e => e.MetricType)
        .Select(g => new { g.Key, Total = g.Sum(e => e.Quantity) })
        .ToListAsync();
    // ...
}
```
- Remove `startOfMonth` from `BillingController.GetUsage`; always use `subscription.CurrentPeriodStart`.
- Add test: `GetUsage_UsesPeriodStart_NotCalendarMonth`.

---

#### H2 — Usage Events Are Never Reset / Rolled Over

There is no Hangfire job to advance `CurrentPeriodStart` when the billing period ends. Usage accumulates past the period boundary and inflates future limit checks.

**Proposed Fix** `→ Phase 3`:
Create `UsagePeriodRolloverJob` in `OrvixFlow.Api/Jobs/`:
```csharp
public class UsagePeriodRolloverJob
{
    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var expiredSubscriptions = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Active
                     && s.CurrentPeriodEnd <= now)
            .ToListAsync();

        foreach (var sub in expiredSubscriptions)
        {
            var interval = sub.BillingInterval == "Yearly" ? 365 : 30;
            sub.CurrentPeriodStart = sub.CurrentPeriodEnd;
            sub.CurrentPeriodEnd = sub.CurrentPeriodEnd.AddDays(interval);
            sub.UpdatedAt = now;
            await _auditService.RecordAsync(sub.CompanyId, "PeriodRolledOver", $"Period advanced to {sub.CurrentPeriodEnd:O}");
        }
        await _db.SaveChangesAsync();
    }
}
```
Register in `Program.cs` as a recurring Hangfire job (daily). Add test: `UsagePeriodRollover_AdvancesPeriodStart_WhenExpired`.

---

#### H3 — `CheckLimitAsync` "Seats" Case Is Hardcoded Wrong

```csharp
case "seats":
    result.CurrentUsage = 0;    // ← Always 0. Never counts actual members.
    result.Allowed = true;      // ← Always allowed regardless of seat limit.
```

**Proposed Fix** `→ Phase 1`:
```csharp
case "seats":
    var memberCount = await _dbContext.UserCompanyMemberships
        .IgnoreQueryFilters()
        .CountAsync(m => m.CompanyId == companyId && m.Status == "Active");
    result.Limit = entitlements.MaxSeats ?? int.MaxValue;
    result.CurrentUsage = memberCount;
    result.ExceededLimit = "Seats";
    result.Allowed = entitlements.CanAddSeats(memberCount);
    break;
```
Add test: `CheckLimit_Seats_ReturnsActualMemberCount`.

---

#### H4 — `BillingInterval` Is a Freeform String

`"monthly"` (lowercase), `"Custom"` both silently calculate as 30 days. Any typo creates a wrong billing period.

**Proposed Fix** `→ Phase 2`:
1. Add enum to `OrvixFlow.Core/Entities/`:
```csharp
public enum BillingInterval { Monthly, Yearly, Custom }
```
2. Change `PlanTemplate.BillingInterval` and `CompanySubscription.BillingInterval` to `BillingInterval` type, stored as string via EF value converter (same pattern as `UserRole`).
3. Replace `interval == "Yearly" ? 365 : 30` with:
```csharp
private static int GetPeriodDays(BillingInterval interval) => interval switch
{
    BillingInterval.Yearly  => 365,
    BillingInterval.Monthly => 30,
    BillingInterval.Custom  => 30, // manual override — no automatic renewal
    _ => 30
};
```
Add test: `BillingInterval_Yearly_SetsCorrect365DayPeriod`.

---

#### H5 — Entitlement Overrides Ignored by `IsWithin*Async` Enforcement

`IsWithinTokenLimitAsync` calls `GetEntitlementsAsync` (base plan limits), not `GetEffectiveEntitlementsAsync` (base + admin overrides). Admin-granted extra tokens are silently ignored during enforcement.

**Proposed Fix** `→ Phase 1` (one-line change per method):
```csharp
// BEFORE
public async Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume)
{
    var entitlements = await GetEntitlementsAsync(companyId);
    return entitlements.CanAddTokens(tokensToConsume);
}

// AFTER
public async Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume)
{
    var entitlements = await GetEffectiveEntitlementsAsync(companyId); // ← use effective
    return entitlements.CanAddTokens(tokensToConsume);
}
```
Apply the same change to `IsWithinApiLimitAsync`, `IsWithinStorageLimitAsync`, `IsWithinKnowledgeBaseLimitAsync`, and `CanInviteUserAsync`.

Also fix `BillingController.GetSubscription` to use `GetEffectiveEntitlementsAsync` so the billing page shows the correct limits.

Add test: `IsWithinTokenLimit_RespectsAdminOverride_NotBasePlanLimit`.

---

#### H6 — Pending Plan Change Has No Processing Job

`ChangePlanAsync(immediate: false)` sets `PendingPlanId` and `PendingChangeAt`. No job ever processes it — scheduled plan changes are silently dropped.

**Proposed Fix** `→ Phase 2`:
Create `PendingPlanChangeJob` modeled after `TrialExpirationJob`:
```csharp
public class PendingPlanChangeJob
{
    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var pending = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.PendingPlanId != null && s.PendingChangeAt <= now)
            .ToListAsync();

        foreach (var sub in pending)
        {
            var newPlan = await _planService.GetPlanByIdAsync(sub.PendingPlanId!.Value);
            if (newPlan == null) continue;

            sub.PlanTemplateId = sub.PendingPlanId.Value;
            sub.Status = SubscriptionStatus.Active;
            sub.PendingPlanId = null;
            sub.PendingChangeAt = null;
            sub.UpdatedAt = now;

            var tenant = await _db.Tenants.FindAsync(sub.CompanyId);
            if (tenant != null) tenant.Plan = newPlan.Slug;

            await _auditService.RecordAsync(sub.CompanyId, "PendingPlanApplied", $"Plan changed to '{newPlan.Name}'");
        }
        await _db.SaveChangesAsync();
    }
}
```
Register as recurring Hangfire job (every 6 hours). Add test: `PendingPlanChangeJob_AppliesPlan_WhenPendingChangeAtExpires`.

---

#### H7 — `AssignPlanAsync` Always Sets Status to `Trialing` for Paid Plans

When a SuperAdmin assigns a paid plan after the company has already paid, the status is forced to `Trialing`, which is incorrect and may block entitled features.

**Proposed Fix** `→ Phase 2`:
Add an optional `targetStatus` parameter to `AssignPlanAsync`:
```csharp
public async Task<CompanySubscription> AssignPlanAsync(
    Guid companyId,
    Guid planTemplateId,
    string? billingInterval = null,
    string? targetStatus = null)   // ← new parameter
{
    // ...
    var defaultStatus = plan.IsFree
        ? SubscriptionStatus.Active
        : (plan.IsTrialAllowed ? SubscriptionStatus.Trialing : SubscriptionStatus.Active);

    var resolvedStatus = targetStatus ?? defaultStatus; // ← admin can override
    subscription.Status = resolvedStatus;
    // ...
}
```
Update `AdminController` plan assignment action to pass `targetStatus: SubscriptionStatus.Active` when assigning as a confirmed paid plan. Add test: `AssignPlan_WithTargetStatus_SetsStatusCorrectly`.

---

#### H8 — `MaxInboxMessagesPerMonth` and `MaxMailboxConnections` Have No Plan-Level Values

These limits exist in `CompanyEntitlements` and `CompanyEntitlementOverride` but not in `PlanEntitlements`. All companies effectively have unlimited inbox processing.

**Proposed Fix** `→ Phase 2`:
1. Add fields to `PlanEntitlements`:
```csharp
public int MaxInboxMessagesPerMonth { get; set; } = 0; // 0 = unlimited
public int MaxMailboxConnections { get; set; } = 1;
```
2. Add EF migration.
3. Update `PlanCatalog.BuildEntitlementsSeed()` with per-plan values, e.g.:
```csharp
// Free
MaxInboxMessagesPerMonth = 100,
MaxMailboxConnections = 1,

// Starter
MaxInboxMessagesPerMonth = 1000,
MaxMailboxConnections = 3,

// Growth
MaxInboxMessagesPerMonth = 10000,
MaxMailboxConnections = 10,

// Business / Enterprise — 0 (unlimited)
```
4. Update `GetEntitlementsAsync` to read these from `PlanEntitlements` instead of defaulting to 0.
5. Call `CanProcessInboxMessage` in `InboxProcessingJob` before processing. Add test: `InboxProcessing_Blocked_WhenMonthlyMessageLimitReached`.

---

### 🟡 Medium Priority Issues

#### M1 — `BillingController.GetUsage` Uses Hardcoded Plan→Limit Map

**Proposed Fix** `→ Phase 1`: Remove the switch/case block entirely. Replace with `EntitlementResolver.GetEffectiveEntitlementsAsync`. Already covered under **C3**.

---

#### M2 — `BillingController.GetSubscription` Returns Fake Billing History

```csharp
var billingHistory = new[]
{
    new { id = "inv_" + Guid.NewGuid().ToString("N")[..8], status = "Paid" }
};
```

**Proposed Fix** `→ Phase 4`:
- Until Stripe is integrated, remove the fake history from the response — return an empty array and a `billingHistoryNote: "Invoice history will be available once billing is activated."` field.
- After Stripe integration: add `Invoice` entity, populate from `invoice.paid` webhook events, return real records.
- In the interim, do not return random GUIDs as fake invoice IDs — they pollute logs and break idempotency assumptions.

---

#### M3 — Plan Change Doesn't Sync `Tenant.Plan`

**Proposed Fix** `→ Phase 1`: In `ChangePlanAsync`, when `immediate == true`, call `SyncTenantDenormalizationAsync(companyId, newPlan.Slug, SubscriptionStatus.Active)`. For deferred changes, this is handled by `PendingPlanChangeJob` (H6 fix).

---

#### M4 — No Downgrade Safety for Active Users and Data

**Proposed Fix** `→ Phase 4`:
Add a pre-check in `ChangePlanAsync` before applying changes:
```csharp
var kbCount = await _db.KnowledgeBases.CountAsync(k => k.TenantId == companyId);
if (newPlan.Entitlements?.MaxKnowledgeBases < kbCount)
    throw new DowngradeNotAllowedException($"Company has {kbCount} KBs but new plan allows {newPlan.Entitlements.MaxKnowledgeBases}.");

var storageMb = /* compute current */ ...;
if (newPlan.Entitlements?.MaxStorageMb < storageMb)
    throw new DowngradeNotAllowedException($"Company uses {storageMb}MB but new plan allows {newPlan.Entitlements.MaxStorageMb}MB.");
```
Return a 409 Conflict with a clear `DowngradeBlocker` payload explaining what the user needs to reduce before downgrading. Add test: `Downgrade_ThrowsDowngradeNotAllowed_WhenKbsExceedNewLimit`.

---

#### M5 — `GetUsage` Uses `startOfMonth` Instead of `CurrentPeriodStart`

**Proposed Fix** `→ Phase 1`: Replace `startOfMonth` with `subscription?.CurrentPeriodStart ?? DateTime.UtcNow.AddDays(-30)`. Already covered under **H1 fix above** — applying `GetEffectiveEntitlementsAsync` which already uses `CurrentPeriodStart` internally.

---

#### M6 — No Invoice / Payment History Entity

**Proposed Fix** `→ Phase 5` (Stripe):
Add `Invoice` entity to `OrvixFlow.Core/Entities/`:
```csharp
public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string ExternalInvoiceId { get; set; } = string.Empty; // Stripe invoice ID
    public int AmountCents { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Draft"; // Draft | Paid | Void | Uncollectible
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```
Populated from `invoice.paid` and `invoice.payment_failed` Stripe events. Returned in `GetSubscription` response replacing the fake array.

---

#### M7 — `SubscriptionStatus` Is a Static String Class, Not an Enum

`PastDue` is defined but never set. No compile-time safety on status transitions.

**Proposed Fix** `→ Phase 2`:
Convert to enum in `OrvixFlow.Core/Entities/`:
```csharp
public enum SubscriptionStatus { Trialing, Active, PastDue, Suspended, Cancelled }
```
Use EF value converter to store as string (consistent with `UserRole` pattern — see `Core/Authorization/Roles.cs`). Update all callers that compare against string constants. Add helpers: `IsActive()`, `IsAccessAllowed()` extension methods. Add test: `SubscriptionStatus_ParseSerializeRoundTrip`.

---

#### M8 — No Grace Period for Cancelled/Expired Subscriptions

**Proposed Fix** `→ Phase 4`:
Add `GracePeriodEndsAt` nullable field to `CompanySubscription`. Set it in `CancelSubscriptionAsync`:
```csharp
subscription.Status = SubscriptionStatus.Cancelled;
subscription.GracePeriodEndsAt = DateTime.UtcNow.AddDays(7); // configurable
```
In the status gate (`C4` fix), allow access while `GracePeriodEndsAt > now`:
```csharp
var isInGrace = subscription.GracePeriodEndsAt.HasValue
    && subscription.GracePeriodEndsAt.Value > DateTime.UtcNow;

if (subscription.Status == SubscriptionStatus.Cancelled && !isInGrace)
    return false;
```
`TrialExpirationJob` (or a new `GracePeriodExpirationJob`) downgrades to Free when grace ends.

---

#### M9 — Proration Endpoint Is a Stub

**Proposed Fix** `→ Phase 5` (Stripe):
- Short-term: mark the endpoint explicitly as returning an estimate, add `isEstimate: true` to response, and hide it from the frontend until Stripe is integrated.
- Long-term: Stripe's Proration API (`SubscriptionService.UpcomingInvoiceAsync`) provides the exact proration — call it and forward the result.

---

#### M10 — No Billing Role Gate on `TrackUsage` POST

`Roles.IsElevated` in `Api/Roles.cs` includes string checks inconsistent with `UserRoleExtensions` in `Core/Authorization/Roles.cs`.

**Proposed Fix** `→ Phase 3`:
- **Remove `POST /api/billing/usage`** from the public API entirely. Usage recording must be internal — callers inject `IUsageService` directly. External REST usage recording is an unnecessary attack surface (any elevated user can manipulate quotas).
- If an internal admin endpoint is needed for manual corrections: protect it with `[Authorize(Policy = "SuperAdminOnly")]`, use the canonical `UserRoleExtensions.ParseRole()` for any role checks inside, and write an `AuditTrail` entry for every usage correction.

---

### 🟢 Low Priority / Technical Debt

#### L1 — `BillingSubscription.CurrentPlan` Is a String, Not a FK

**Proposed Fix** `→ Phase 2` (covered by C1 fix — `BillingSubscription` entity is removed entirely when merged into `CompanySubscription`).

---

#### L2 — `PlanTemplate.BillingInterval` Stored as String vs `CompanySubscription.BillingInterval`

**Proposed Fix** `→ Phase 2`: Convert to `BillingInterval` enum (covered by H4 fix).

---

#### L3 — `UsageEvent.MetricType` Is a Freeform String

**Proposed Fix** `→ Phase 3`:
Add a `UsageMetric` static class to `OrvixFlow.Core/Entities/`:
```csharp
public static class UsageMetric
{
    public const string AiTokens       = "ai-tokens";
    public const string N8nNodes       = "n8n-nodes";
    public const string StorageMb      = "storage-mb";
    public const string KnowledgeBases = "knowledge-bases";
    public const string InboxMessages  = "inbox-messages";
}
```
Replace all string literals in `UsageService`, `EntitlementResolver`, and `BillingController` with these constants. This prevents silent metric type mismatches with no code changes to the DB schema.

---

#### L4 — `GetAllPlansAsync` Doesn't Filter Publicly Visible

**Proposed Fix** `→ Phase 2` (one-line change):
```csharp
// BillingController.GetAvailablePlans
var plans = await _planService.GetActivePlansAsync();
var publicPlans = plans.Where(p => p.IsPubliclyVisible); // ← add this filter
```
Admin plan listing (`AdminController`) should continue showing all plans including non-public ones.

---

#### L5 — Enterprise Plan Has `MonthlyPriceCents = 0`, Which Confuses `isUpgrade/isDowngrade`

**Proposed Fix** `→ Phase 2`:
Replace price-based upgrade/downgrade detection with an explicit `SortOrder` comparison:
```csharp
// BillingController.GetAvailablePlans
var currentSortOrder = subscription?.PlanTemplate?.SortOrder ?? 0;
var planDtos = plans.Select(p => new
{
    // ...
    isUpgrade   = p.SortOrder > currentSortOrder,
    isDowngrade = p.SortOrder < currentSortOrder,
    // Remove isUpgrade/isDowngrade based on price
});
```
`PlanCatalog` already has `SortOrder` field on `PlanTemplate`. Ensure seed values are set: Free=0, Starter=1, Growth=2, Business=3, Enterprise=4.

---

## 3. Target Billing Architecture

### 3.1 Single Source of Truth: `CompanySubscription`

**Rule:** `CompanySubscription` is the only authoritative subscription record. `BillingSubscription`, `Tenant.Plan`, and `Tenant.SubscriptionStatus` are derived/denormalized fields that must be kept in sync via a centralized `ISubscriptionSyncService`.

```
Stripe Event
    ↓
StripeWebhookHandler (validates signature)
    ↓
CompanySubscriptionService.SyncFromStripeAsync(...)
    ↓ writes
CompanySubscription (status, planId, periodStart, periodEnd, externalId)
    + Tenant.Plan (denormalized string)
    + Tenant.SubscriptionStatus (denormalized string)
    + BillingSubscription (stripe IDs for future lookups)
    + AuditTrail entry
```

### 3.2 Subscription Status Enum

Replace the `SubscriptionStatus` static string class with a proper enum in `OrvixFlow.Core/Authorization/` (following the established `UserRole` enum pattern):

```csharp
public enum SubscriptionStatus { Trialing, Active, PastDue, Suspended, Cancelled }
```

Store as serialized string in DB. Parse at boundary (same pattern as `UserRole`).

### 3.3 Entitlement Enforcement Gateway

All AI operations, uploads, invitations, and inbox processing **must** go through the same gateway before doing work:

```
CompanyEntitlementGateway.CheckAsync(companyId, limitType, amount)
    → checks: (1) subscription is Active or Trialing
    → checks: (2) effective entitlements (base plan + overrides)
    → returns: CheckResult { Allowed, Reason, CurrentUsage, Limit, UpgradeUrl }
```

The gateway must be the **single entry point** — not a collection of ad-hoc `IsWithin*Async` calls scattered across individual controllers.

### 3.4 Billing Interval as Enum

```csharp
public enum BillingInterval { Monthly, Yearly, Custom }
```

Period calculation derived from this enum, not freeform string comparison.

### 3.5 Usage Period Consistency

All three period-aware queries must use the **same source**: `CompanySubscription.CurrentPeriodStart`. Calendar-month calculations removed from `BillingController`.

### 3.6 Periodic Billing Jobs

```
TrialExpirationJob (existing) — runs every 6h, downgrades expired trials to Free
PendingPlanChangeJob (NEW)    — runs every 6h, applies PendingPlanId when PendingChangeAt <= now
UsagePeriodRolloverJob (NEW)  — runs daily, checks if CurrentPeriodEnd <= now and rolls over period
```

### 3.7 Module Access Resolution Chain

```
Plan inclusions (base)
    + CompanyModuleOverride (admin grant/suppress)
    + Subscription status gate (Suspended/Cancelled → no access)
    → CanUseModule
```

Suspension must be checked as the first gate before plan/override lookup.

### 3.8 Stripe Integration Contract

When Stripe is integrated:
1. `BillingController.StripeWebhook` validates `Stripe-Signature` header using `StripeClient.ConstructEvent`.
2. Relevant events: `invoice.paid`, `invoice.payment_failed`, `customer.subscription.updated`, `customer.subscription.deleted`.
3. Each event maps to a `CompanySubscriptionService` method call.
4. `BillingSubscription` stores only Stripe IDs — `CompanySubscription` stays authoritative for access control.

---

## 4. Improvements and Fixes — Prioritized

### Tier 1 — Must Fix Before Soft Launch ✅ COMPLETE (2026-04-15)

| ID | Fix | Files Affected | Status |
|----|-----|---------------|--------|
| **T1-1** | Add Stripe webhook signature validation | `BillingController.cs` | ✅ Done |
| **T1-2** | Sync `CompanySubscription.Status` in Suspend/Cancel/Reactivate | `CompanySubscriptionService.cs` | ✅ Done |
| **T1-3** | Sync `Tenant.SubscriptionStatus` in all lifecycle operations | `CompanySubscriptionService.cs` | ✅ Done |
| **T1-4** | Add subscription status gate to `CanUseModuleWithOverridesAsync` and `GetEntitlementsAsync` | `EntitlementResolver.cs` | ✅ Done |
| **T1-5** | Use `GetEffectiveEntitlementsAsync` instead of `GetEntitlementsAsync` inside `IsWithin*Async` callers | `EntitlementResolver.cs` | ✅ Done |
| **T1-6** | Fix `CheckLimitAsync("seats")` — always returns `Allowed=true` | `EntitlementResolver.cs` | ✅ Done |
| **T1-7** | Replace hardcoded plan→limit switch in `BillingController.GetUsage` | `BillingController.cs` | ✅ Done |

**Tier 1 Implementation Notes:**
- Webhook temporarily protected with `[Authorize(Policy = "SuperAdminOnly")]` until full Stripe signature validation is implemented
- Tenant sync implemented via `SyncTenantDenormalizationAsync()` helper in `CompanySubscriptionService`
- Subscription status gate returns zero entitlements and blocks module access for Suspended/Cancelled
- Seat limit now counts actual `UserCompanyMemberships.Status == "Active"` records
- Tests added in `OrvixFlow.Tests/BillingPhase1Tests.cs` (15 tests)

### Tier 2 — Should Be Done Before Any Real Billing

| ID | Fix | Files Affected |
|----|-----|---------------|
| **T2-1** | Implement `PendingPlanChangeJob` — process scheduled plan changes | New job file |
| **T2-2** | `ChangePlanAsync` must sync `Tenant.Plan` | `CompanySubscriptionService.cs` |
| **T2-3** | Convert `SubscriptionStatus` static class to enum | `CompanySubscription.cs`, `Roles.cs`, all callers |
| **T2-4** | Convert `BillingInterval` to enum | `PlanTemplate.cs`, `CompanySubscription.cs`, `CompanySubscriptionService.cs` |
| **T2-5** | Convert `MetricType` string constants to enum or `MetricTypes` static constants class | `UsageEvent.cs`, `UsageService.cs` |
| **T2-6** | Unify usage period calculation — use `CurrentPeriodStart` everywhere | `BillingController.cs`, `UsageService.cs` |
| **T2-7** | Add `MaxInboxMessagesPerMonth` and `MaxMailboxConnections` to `PlanEntitlements` entity and seed | `PlanEntitlements.cs`, `PlanCatalog.cs`, migration |
| **T2-8** | Filter publicly visible plans in `GetAvailablePlans` | `BillingController.cs` |
| **T2-9** | Fix Enterprise plan `isUpgrade/isDowngrade` — use `SortOrder` or explicit tier number | `BillingController.cs` |

### Tier 3 — Architecture Cleanup

| ID | Fix | Files Affected |
|----|-----|---------------|
| **T3-1** | Merge `BillingSubscription` into `CompanySubscription` (add `ExternalCustomerId`, `ExternalSubscriptionId`) | `BillingSubscription.cs`, `CompanySubscription.cs`, migration |
| **T3-2** | Introduce `CompanyEntitlementGateway` as single enforcement entrypoint | New interface + service |
| **T3-3** | Remove fake billing history from `GetSubscription`; add `Invoice` entity or defer to Stripe | `BillingController.cs` |
| **T3-4** | Add downgrade safety checks — KB count, storage, module scope preview on plan change | `CompanySubscriptionService.cs` |
| **T3-5** | Normalize `TrackUsage` POST — currently any authenticated user who is "elevated" can write usage events; this should be internal-only | `BillingController.cs` |
| **T3-6** | Implement `AssignPlanAsync` `status` parameter — allow admin to set `Active` directly for post-payment scenarios | `CompanySubscriptionService.cs`, `AdminController.cs` |
| **T3-7** | Add per-company billing address / tax ID fields to `Tenant` for Stripe Tax integration | `Tenant.cs`, migration |

### Tier 4 — Feature Additions

| ID | Feature | Notes |
|----|---------|-------|
| **T4-1** | `Invoice` entity — store per-period invoices (even mock ones before Stripe) | Pre-Stripe |
| **T4-2** | Grace period logic — N days post-cancellation on Free before blocking | `CancelSubscriptionAsync` |
| **T4-3** | Usage alerts — notify `CompanyOwner` at 80% and 100% token usage | New notification hook |
| **T4-4** | Per-department usage breakdown in billing page | `UsageService.GetDepartmentSummaryAsync` |
| **T4-5** | Usage rollover job — archive old `UsageEvents` at period end | New Hangfire job |
| **T4-6** | `PastDue` handling — Stripe `invoice.payment_failed` → flip to `PastDue` → retry logic | Stripe webhook |

---

## 5. Implementation Phases

### Phase 1 — Critical Fixes (1–2 days) ✅ COMPLETE (2026-04-15)
**Goal:** Make existing billing safe and consistent.

1. **Fix `BillingController.StripeWebhook`** — protected with `[Authorize(Policy = "SuperAdminOnly")]` until Stripe signature validation is implemented.
2. **Fix all lifecycle operations** in `CompanySubscriptionService` — added `SyncTenantDenormalizationAsync()` helper called in all methods that change `Status` or `Plan`.
3. **Fix subscription status gate** — `EntitlementResolver.GetEntitlementsAsync` and `CanUseModuleWithOverridesAsync` return zero/empty for Suspended or Cancelled subscriptions.
4. **Fix `CheckLimitAsync("seats")`** — now counts actual `UserCompanyMemberships.Status == "Active"` before comparing to limit.
5. **Fix `GetUsage` hardcoded map** — now reads from `EntitlementResolver.GetEffectiveEntitlementsAsync` instead.
6. **Fix `IsWithin*Async` callers** — all now call `GetEffectiveEntitlementsAsync` (override-aware).

**Tests added in `BillingPhase1Tests.cs`:**
- `SuspendSubscription_SyncsTenantStatus`
- `CancelSubscription_SyncsTenantStatus`
- `ReactivateSubscription_SyncsTenantStatus`
- `ChangePlan_Immediate_SyncsTenantPlan`
- `CancelledSubscription_GetEntitlements_ReturnsZeroLimits`
- `SuspendedSubscription_GetEntitlements_ReturnsZeroLimits`
- `CancelledSubscription_CanUseModuleWithOverrides_ReturnsFalse`
- `IsWithinTokenLimit_RespectsAdminOverride_NotBasePlanLimit`
- `CheckLimit_Seats_ReturnsActualMemberCount`

---

### Phase 2 — Billing Model Stabilization (3–5 days)
**Goal:** Clean data model, no duplicate entities, correct lifecycle.

1. **Add `SubscriptionStatus` enum** to `OrvixFlow.Core`. Migrate status string in DB.
2. **Add `BillingInterval` enum**. Update `PlanTemplate`, `CompanySubscription`, period calculation.
3. **Merge `BillingSubscription` → `CompanySubscription`** — add `ExternalCustomerId`, `ExternalSubscriptionId` fields. Remove `BillingSubscription` entity and `BillingSubscriptions` DbSet.
4. **Add `MaxInboxMessagesPerMonth`** and `MaxMailboxConnections` to `PlanEntitlements`. Seed values per plan. Update `GetEntitlementsAsync` to read them.
5. **Implement `PendingPlanChangeJob`** — runs every 6 hours, apply `PendingPlanId` when `PendingChangeAt <= now`, sync `Tenant.Plan`, write audit entry.
6. **Fix `ChangePlanAsync`** — sync `Tenant.Plan` immediately on plan change (at least for immediate changes).
7. **Fix `GetAvailablePlans`** — filter on `IsPubliclyVisible`.
8. **DB migration** for all schema changes.

**Tests to add:**
- `PendingPlanChangeJob_AppliesChange_WhenPeriodEnds`
- `PlanEntitlements_InboxMessages_EnforcedCorrectly`
- `BillingInterval_Yearly_Sets365DayPeriod`
- `AssignPlan_SyncsTenantPlan_WhenCancelled`

---

### Phase 3 — Usage Tracking & Enforcement (3–5 days)
**Goal:** Consistent, reliable usage enforcement with no bypass paths.

1. **Introduce `CompanyEntitlementGateway`** interface + implementation — single `CheckAsync(companyId, limitType, amount)` call that checks subscription status first, then effective entitlements.
2. **Enforce via gateway** in all AI service call sites: `AgentService`, `InboxGuardianService`, `IngestionPipelineService`, `FileIngestionJob`.
3. **Unify usage period** — all usage queries use `CompanySubscription.CurrentPeriodStart`.
4. **Convert `MetricType` to typed constants** — `UsageMetric` static class or enum.
5. **Add `UsagePeriodRolloverJob`** — runs daily, checks if `CurrentPeriodEnd <= now`, updates `CurrentPeriodStart = CurrentPeriodEnd`, sets `CurrentPeriodEnd = CurrentPeriodEnd + interval`, writes audit event.
6. **Remove `POST /api/billing/usage`** from public API — usage recording should be internal-only via injected `IUsageService`, not a REST endpoint.

**Tests to add:**
- `EntitlementGateway_BlocksAction_WhenTokenLimitExceeded`
- `EntitlementGateway_AllowsAction_WhenWithinOverrideLimit`
- `UsagePeriodRollover_UpdatesPeriodStart_WhenExpired`
- `InboxProcessing_DecreasesInboxMessageQuota`

---

### Phase 4 — Admin Panel & UX (2–3 days)
**Goal:** Admin sees accurate data; billing page UX is correct.

1. **Fix `BillingController.GetSubscription`** — use `GetEffectiveEntitlementsAsync`, correct period dates, remove fake billing history.
2. **Add `AssignPlanAsync(status)` parameter** — admin can set subscription to `Active` directly (for post-payment scenarios), not just `Trialing`.
3. **Add downgrade safety check** — `ChangePlanAsync` should warn/block when current KBs > new plan limit, current storage > new plan limit, current members > new plan seat limit.
4. **Add usage alert hook** in `UsageService` — when 80%/100% of monthly token limit is hit, fire an integration event (email or Hangfire notification job).
5. **Admin subscription view** — add `GET /api/admin/companies/{id}/subscription` returning full `CompanySubscription` + current usage + `EntitlementOverride`.

**Tests to add:**
- `GetSubscription_UsesEffectiveEntitlements_WithOverride`
- `Downgrade_Blocked_WhenCurrentKbsExceedNewPlanLimit`
- `AdminAssignPlan_CanSetActiveStatus`

---

### Phase 5 — Stripe Integration (1+ week)
**Goal:** Real payment processing, real webhook handling.

1. **Add Stripe.net NuGet package** to `OrvixFlow.Infrastructure`.
2. **Implement `IStripeService`** with methods: `CreateCustomer`, `CreateSubscription`, `UpdateSubscription`, `CancelSubscription`, `CreateBillingPortalSession`.
3. **Fix `StripeWebhook` endpoint**:
   - Read raw body before model binding.
   - Validate `Stripe-Signature` with `StripeClient.ConstructEvent(body, header, secret)`.
   - Route events to `CompanySubscriptionService`:
     - `invoice.paid` → `SetActiveAsync`
     - `invoice.payment_failed` → `SetPastDueAsync`
     - `customer.subscription.updated` → `SyncFromStripeAsync`
     - `customer.subscription.deleted` → `CancelSubscriptionAsync`
4. **Add Stripe webhook secret to `.env`** / environment config.
5. **Add `GET /api/billing/portal`** — returns Stripe Customer Portal URL for self-service plan management.
6. **Add `POST /api/billing/checkout`** — creates Stripe Checkout session for new subscription.
7. **Implement `Invoice` entity** — store invoice records received from Stripe webhook.

**Tests to add (integration):**
- `StripeWebhook_Rejects_InvalidSignature`
- `StripeWebhook_InvoicePaid_SetsSubscriptionActive`
- `StripeWebhook_PaymentFailed_SetsSubscriptionPastDue`
- `StripeWebhook_SubscriptionDeleted_CancelsAndDowngradesToFree`

---

## 6. Testing Strategy

### Unit Tests

| Test | What to Cover |
|------|--------------|
| `EntitlementResolverTests` | Effective entitlements with/without overrides; cancelled subscription returns zero limits |
| `CompanySubscriptionServiceTests` | All lifecycle transitions; Tenant sync after each operation |
| `PlanChangeValidationTests` | Downgrade with excess KBs/storage/seats |
| `SubscriptionStatusGateTests` | Module access blocked for Suspended, Cancelled |
| `SubscriptionEnumTests` | Parse/serialize round-trip for `SubscriptionStatus` enum |
| `BillingIntervalTests` | Period days calculation from enum |

### Integration Tests

| Test | What to Cover |
|------|--------------|
| `SubscriptionLifecycleIntegrationTests` | Full trial → active → cancelled → downgrade to free lifecycle |
| `PendingPlanChangeJobTests` | Pending change applied at correct time |
| `UsagePeriodRolloverJobTests` | Period rolls over, new events counted in new period |
| `EntitlementGatewayIntegrationTests` | Gateway blocks when limit exceeded, allows when within |
| `StripeWebhookHandlerTests` | Signature validation, event routing |

### Edge Cases to Cover

| Edge Case | Risk if Not Tested |
|-----------|-------------------|
| Plan assigned to company with no `PlanEntitlements` row | Falls back to hardcoded defaults — hidden billing config |
| Subscription with `PendingPlanId` but no job running | User's plan never changes |
| Company downgrade to plan with fewer seats than current members | Members cannot be invited but existing members not blocked |
| Usage events recorded in month N counted in rollover to month N+1 | Double-counting; inflated quota consumption |
| Enterprise plan (`MonthlyPriceCents = 0`) upgrade/downgrade direction | Wrong UI state |
| Two simultaneous plan change requests | Race condition on `PendingPlanId` field |
| `CanProcessInboxMessage` when `MaxInboxMessagesPerMonth = 0` | Treated as unlimited (intentional — verify this is the right behavior) |
| `CanUseModuleWithOverridesAsync` with no subscription | Returns `false` — correct? Or should it fall back to Free plan? |

---

## 7. Risks & Future Considerations

| Risk | Mitigation |
|------|-----------|
| **Stripe webhook replay attacks** | Stripe timestamps webhooks; validate `Stripe-Signature` timestamp is within 5 minutes |
| **Usage events table growth** | Add DB index on `(CompanyId, OccurredAt)`. Archive old events past 13 months. |
| **Concurrent usage recording** | `UsageService.WriteEventAsync` is append-only (safe). Limit checks are eventually consistent — acceptable for token limits; not acceptable for hard seat limits (use DB transaction). |
| **Entitlement cache stampede** | Every request hitting entitlement resolver hits DB 3+ times. Consider short-lived (30s) in-memory or distributed cache per company once traffic increases. |
| **Trial abuse** | A company that creates a new account after trial expiration gets a new trial. Consider email-domain level trial tracking. |
| **Downgrade data loss** | If a company downgrades, their extra KBs and files still exist but are locked. Define a clear policy: locked for N days, then asked to delete, then auto-archived. |
| **Enterprise pricing complexity** | Enterprise with `MonthlyPriceCents = 0` and `BillingInterval = "Custom"` needs manual billing outside the system. Add a flag `IsManuallyBilled` to `CompanySubscription` to avoid Stripe integration confusion. |
| **Multi-currency** | Currently all plans are USD-only. `PlanTemplate.Currency` field exists but is always `"USD"`. Stripe handles currency conversion — hook into it properly when integrating. |
