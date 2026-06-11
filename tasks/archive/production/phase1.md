# Phase 1 — Production Email Validation

> **Obsolete / Historical Plan**
> Superseded by `tasks/production/current-state-audit.md`, `tasks/production/overview.md`, and `tasks/production/progress.md` on 2026-06-11.
> This file reflects an older execution plan and stale status assumptions. Do not use it as the current source of truth.

> **Status:** Not Started  
> **Estimated effort:** 1 week  
> **Dependencies:** Phase 0 complete; Resend account with verified sending domain  
> **Blocks:** Phase 3 (mailbox OAuth needs email for credential notifications)

---

## Goal

Prove that OrvixFlow's email delivery pipeline works end-to-end in production conditions. This is NOT about adding new email features — it is about validating that the existing correct code actually delivers real emails through a real provider.

---

## Why

The `NotificationProcessorJob` code is correct and well-hardened (retry logic, `IgnoreQueryFilters`, `DisableConcurrentExecution`, processing lease). The `ResendEmailService` exists and is tested in isolation. However:

- No evidence exists that a real registration email has ever been received by a real inbox
- The `NotificationProcessorJob` runs in a Hangfire background context with no JWT — it uses `IgnoreQueryFilters()` to bypass tenant filters. This critical path has never been validated in a staging environment
- `UsagePeriodRolloverJob` (daily billing period rollover) has zero test coverage

This phase closes all three gaps.

---

## Scope

- Validate end-to-end email delivery through Resend in a staging environment
- Verify `NotificationProcessorJob` background execution (no JWT context)
- Add `UsagePeriodRolloverJob` unit tests
- Update `.env.example` with complete Resend configuration
- Correct the outdated `memory-security.md` documentation (refresh token system is implemented but documented as absent)

---

## Out of Scope

- No new email providers
- No changes to `NotificationProcessorJob` logic (code is already correct)
- No changes to email templates or content
- No mailbox OAuth (that is Phase 3)
- No Stripe configuration (that is Phase 2)

---

## Dependencies

- **Phase 0 complete** — environment must be documented and secured before testing production delivery
- **Resend account** with a verified sending domain (e.g., `orvixflow.com`)
- **Staging environment** — a running `docker compose up` with `EMAIL_PROVIDER=Resend` and real `EMAIL_RESEND_API_KEY`

---

## Files / Components Likely Involved

| File | Task |
|---|---|
| `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs` | P1-2: verify; read before testing |
| `OrvixFlow.Api/Jobs/UsagePeriodRolloverJob.cs` | P1-3: add tests |
| `OrvixFlow.Infrastructure/Services/ResendEmailService.cs` | P1-1: used in validation |
| `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs` | P1-1: used in SMTP validation |
| `OrvixFlow.Core/Entities/NotificationQueue.cs` | P1-2: understand retry fields |
| `OrvixFlow.Tests/` | P1-3: new `UsagePeriodRolloverJobTests.cs` |
| `.env.example` | P1-4: update Resend example |
| `memory/memory-security.md` | P1-6: correct refresh token documentation |

---

## Implementation Tasks

### P1-1 — End-to-End Email Delivery Validation

This is a manual validation task, not a code change.

**Prerequisite:** Start the application with:
```
EMAIL_PROVIDER=Resend
EMAIL_RESEND_API_KEY=re_<your_test_api_key>
EMAIL_FROM_EMAIL=noreply@<your-verified-domain>
EMAIL_FROM_NAME=OrvixFlow
```

**Flow A — Registration email:**
1. Call `POST /api/auth/register` with a test email address you control
2. Confirm a `NotificationQueue` row is created in the DB with `Processed=false`
3. Wait for `NotificationProcessorJob` to run (every 5 minutes) OR manually trigger it via Hangfire dashboard at `http://localhost:5000/hangfire` (requires SuperAdmin JWT)
4. Confirm the `NotificationQueue` row is now `Processed=true`
5. Confirm a verification email was received at the test address
6. Click the verification link — confirm it works

**Flow B — Invite email:**
1. Call `POST /api/invite` with a test email address
2. Confirm queue row created
3. Trigger job
4. Confirm invite email received
5. Accept invite — confirm account created correctly

**Flow C — Usage alert email:**
1. Create a `UsageEvent` that exceeds the tenant's entitlement limit
2. Confirm `UsageAlertService` queues a notification
3. Trigger job
4. Confirm alert email received

**Flow D — SMTP validation:**
1. Switch `EMAIL_PROVIDER=Smtp` with a test SMTP server (e.g., Mailhog in Docker, or Mailtrap)
2. Repeat Flow A
3. Confirm email arrives in SMTP test inbox

> ⚠️ **Known Risk (R9):** `InboxProcessingJob` and `FeedbackEnrichmentJob` use `BackgroundTenantProvider` which resolves tenant from the job parameter. While architecturally correct, this has never been stress-tested under concurrency. Note any unexpected behavior during this phase but do not fix it here — document it in this file and create a separate task if needed.

### P1-2 — Verify NotificationProcessorJob Background Execution

**Read first:** `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs` (160 lines)

The job correctly:
- Uses `IgnoreQueryFilters()` to bypass EF tenant filters (line 44) — required because background context has no JWT
- Uses `[DisableConcurrentExecution(timeoutInSeconds: 300)]` (line 39) — prevents parallel runs
- Has a 15-minute processing lease for crash recovery
- Tracks `AttemptCount`, `LastError`, `Failed` for reliability

**Verification steps:**
- [ ] Confirm that Hangfire's DI scope provides `AppDbContext` without a JWT-based `TenantId` when the job runs
- [ ] Confirm `IgnoreQueryFilters()` causes cross-tenant rows to be visible (this is correct — the job must process ALL tenants' notifications)
- [ ] Confirm `DisableConcurrentExecution` prevents a second job run from starting while one is in progress
- [ ] If any issue is found: document it here. Do NOT modify the job without understanding the full retry/lease model.

### P1-3 — Add UsagePeriodRolloverJob Unit Tests

**File to create:** `OrvixFlow.Tests/UsagePeriodRolloverJobTests.cs`

**Read first:** `OrvixFlow.Api/Jobs/UsagePeriodRolloverJob.cs`

The job runs daily and advances billing periods for companies whose current period has expired. Tests needed:

```csharp
// Test structure — follow existing job test patterns (e.g., TrialExpirationTests.cs)

public class UsagePeriodRolloverJobTests : IDisposable
{
    // Setup: InMemory DB with unique name per test

    [Fact]
    public async Task ExecuteAsync_WhenPeriodExpired_AdvancesToNextPeriod()
    {
        // Arrange: company with subscription where CurrentPeriodEnd = yesterday
        // Act: job.ExecuteAsync()
        // Assert: CurrentPeriodStart = old CurrentPeriodEnd, CurrentPeriodEnd = advanced
    }

    [Fact]
    public async Task ExecuteAsync_WhenPeriodNotExpired_DoesNotAdvance()
    {
        // Arrange: company with subscription where CurrentPeriodEnd = tomorrow
        // Act: job.ExecuteAsync()
        // Assert: period unchanged
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrialingSubscription_AdvancesPeriod()
    {
        // Arrange: trialing company
        // Act + Assert: period advances correctly
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAffectOtherCompanies()
    {
        // Arrange: two companies, only one has expired period
        // Act + Assert: only the expired one is updated
    }
}
```

**Pattern reference:** `OrvixFlow.Tests/TrialExpirationTests.cs` — follow the same DB setup, `MockTenantProvider`, and assertion style.

### P1-4 — Update .env.example with Complete Resend Configuration

**File:** `.env.example`

Current state (partial):
```
EMAIL_RESEND_API_KEY=
EMAIL_RESEND_BASE_URL=https://api.resend.com
```

Replace with a complete example:
```
# Email Delivery — Resend (production recommended)
# Get API key from https://resend.com/api-keys
# EMAIL_PROVIDER=Resend uses this key to send via the Resend API
EMAIL_RESEND_API_KEY=re_your_resend_api_key_here
EMAIL_RESEND_BASE_URL=https://api.resend.com

# Resend requires a verified sending domain.
# Set EMAIL_FROM_EMAIL to an address at your verified domain.
# Example: noreply@yourdomain.com
# EMAIL_PROVIDER=Resend will fail with 403 if the domain is not verified.
```

### P1-5 — Configure Resend Domain and From-Address for Production

This is an operational task (no code change):

- [ ] Log in to Resend dashboard
- [ ] Add your production domain (e.g., `orvixflow.com`)
- [ ] Add the required DNS records (SPF, DKIM, DMARC) to your domain
- [ ] Verify domain in Resend dashboard
- [ ] Create a production API key with `Sending access` only (not full access)
- [ ] Store API key in production `.env` (not committed)
- [ ] Confirm `EMAIL_FROM_EMAIL` is set to an address at the verified domain

### P1-6 — Update memory-security.md (Fix Stale Documentation)

**File:** `memory/memory-security.md`

**Problem (Risk R10):** Line 57 says: _"No refresh token — users must re-login after expiry. This is intentional for now; future work may add refresh tokens."_

This is wrong. Refresh tokens are fully implemented:
- `RefreshToken` entity (in `OrvixFlow.Core/Entities/`)
- `CreateRefreshTokenAsync` in `AuthService`
- `RefreshSessionAsync` in `AuthService`
- 7-day TTL, opaque `lookupKey.secret`, family-based revocation
- Tested in `AuthServiceTests`

**Fix:** Update the relevant section of `memory-security.md` to document the actual refresh token model:
- 7-day refresh token TTL
- Opaque token format (lookupKey.secret)
- Family-based revocation (if one token in a family is compromised, all are revoked)
- Refresh token is rotated on each use
- Stored in `RefreshToken` table, separate from JWT

---

## Architecture Rules

- Do not modify `NotificationProcessorJob.cs` unless a genuine bug is found
- Do not modify `ResendEmailService.cs` unless a genuine bug is found
- `IgnoreQueryFilters()` in `NotificationProcessorJob` is intentional and correct — do not remove it
- All new tests must use unique InMemory DB names (`Guid.NewGuid().ToString()`)
- Test the job through its `ExecuteAsync()` public method — do not make private methods public for testing
- Do not add logging inside existing methods without consulting the existing logging patterns in `NotificationProcessorJob`

---

## Tests Required

### New Unit Tests

- `UsagePeriodRolloverJobTests.cs` (4 test cases — see P1-3 above)

### Existing Tests to Re-Run

```bash
dotnet test --filter "FullyQualifiedName~NotificationProcessor"
dotnet test --filter "FullyQualifiedName~EmailProvider"
dotnet test --filter "FullyQualifiedName~AuthEndToEnd"
dotnet test --filter "FullyQualifiedName~TrialExpiration"
dotnet test --filter "FullyQualifiedName~UsagePeriodRollover"
# Full suite
dotnet test
```

### Manual / E2E Tests

See P1-1 above (Flows A, B, C, D).

---

## Validation Checklist

- [ ] Registration email received at a real inbox (Resend dashboard shows delivery)
- [ ] Invite email received at a real inbox
- [ ] Usage alert email generated and delivered
- [ ] `NotificationProcessorJob` processes cross-tenant rows correctly (using `IgnoreQueryFilters`)
- [ ] `NotificationProcessorJob` marks rows as `Processed=true` after successful delivery
- [ ] Failed delivery increments `AttemptCount` and stores sanitized error in `LastError`
- [ ] After 3 failures, `Failed=true` and row is not retried
- [ ] `UsagePeriodRolloverJob` tests pass (4 new tests)
- [ ] `memory/memory-security.md` correctly describes the refresh token system
- [ ] `.env.example` has complete Resend documentation
- [ ] `dotnet test` — 565 passing (561 existing + 4 new), 0 failing
- [ ] `npm run build && npm run lint && npm run test` — all pass

---

## Definition of Done

1. All four email flows (register, invite, usage alert, SMTP test) delivered real emails to real inboxes
2. `UsagePeriodRolloverJob` has tests and they pass
3. `memory-security.md` is corrected
4. Full test suite passes

---

## Common Mistakes

1. **Trying to fix the queue processor during validation** — if a bug is found, document it. Do not fix during this phase unless the fix is a trivial one-liner. Complex fixes go to a new task.
2. **Using the Console provider and claiming email works** — validation must use Resend or SMTP, not Console (which only logs to stdout)
3. **Forgetting that Hangfire jobs use a scoped DI context** — `NotificationProcessorJob` gets its own `AppDbContext` scope. The `IgnoreQueryFilters()` call must be on the `DbContext` within that scope, not a shared static context.
4. **Skipping the SMTP validation flow** — SMTP must also be verified; Resend alone is not sufficient
5. **Not updating memory-security.md** — stale memory docs mislead future agents. The correction takes 5 minutes.

---

## Handoff to Phase 2

Before Phase 2 starts, confirm:

1. At least one real email has been received via Resend
2. `UsagePeriodRolloverJob` tests pass
3. `memory-security.md` is updated
4. `.env.example` is complete

Phase 2 requires a Stripe production account. Prepare those credentials before starting Phase 2.
