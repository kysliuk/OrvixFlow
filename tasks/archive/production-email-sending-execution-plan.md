# Production Email Sending - Multi-Session AI Execution Plan

> **Historical Execution Plan**
> This document is useful for background context, but it is no longer the current source of truth for production-readiness status.
> Use current code and `tasks/production/current-state-audit.md` for current status and remaining validation work.

## 1. Executive Summary

This plan covers the productionization of OrvixFlow's outbound email delivery for transactional auth emails and queued notification emails.

This is not a greenfield email feature. The current architecture already has:

- `IEmailService` as the outbound abstraction
- `AuthService` queueing verification and invitation emails into `NotificationQueue`
- `NotificationProcessorJob` delivering queued notifications via `IEmailService`
- `MockEmailService` and `SmtpEmailService`
- Hangfire recurring scheduling for notification processing

The implementation must preserve that architecture and harden it for production instead of bypassing it with direct sends.

The highest-risk hidden issue is that `NotificationQueue` is tenant-filtered in EF Core, while `NotificationProcessorJob` runs in background context without request JWT tenant resolution. Production email delivery is therefore not complete until queue processing is explicitly verified and hardened.

This track includes:

- provider support for `Console`, `Smtp`, and `Resend`
- SMTP bug fix for unauthenticated/test SMTP
- queue delivery correctness in Hangfire/background execution
- retry/error metadata and duplicate-send protection
- operational validation and docs

This track does not include mailbox OAuth credential capture. That is handled in `tasks/mailbox-oauth-credential-capture-execution-plan.md`.

## 2. Project Context Relevant To This Work

### Current Architecture

- Backend stack: ASP.NET Core / EF Core / PostgreSQL / Hangfire
- Clean architecture: `Core` -> `Infrastructure` -> `Api`
- Email sending abstraction: `OrvixFlow.Core/Interfaces/IEmailService.cs`
- Existing providers:
  - `OrvixFlow.Infrastructure/Services/MockEmailService.cs`
  - `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs`
- Provider configuration object:
  - `OrvixFlow.Infrastructure/Services/EmailOptions.cs`
- DI wiring:
  - `OrvixFlow.Infrastructure/DependencyInjection.cs`
- Notification queue entity:
  - `OrvixFlow.Core/Entities/NotificationQueue.cs`
- Background sender job:
  - `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
- Recurring job registration:
  - `OrvixFlow.Api/Program.cs`

### Auth / Notification Behavior

- `AuthService.RegisterAsync()`:
  - hashes verification token at rest
  - creates verification link from `Frontend:BaseUrl`
  - queues a notification instead of sending directly
- `AuthService.InviteUserAsync()`:
  - hashes invitation token at rest
  - creates invite link from `Frontend:BaseUrl`
  - queues a notification instead of sending directly
- Usage alerts also queue notifications into `NotificationQueue`

### Tenant Model Constraints

- Tenant isolation is enforced by global query filters in `AppDbContext`
- `NotificationQueue` is filtered by `CompanyId == _tenantProvider.GetTenantId()`
- `TenantProvider` resolves tenant from JWT claims, not headers, except platform-admin impersonation path
- Background Hangfire jobs do not naturally run with JWT context

### Security And Operational Conventions

- Do not commit secrets
- Use env vars / `.env` for runtime secrets and `.env.example` for tracked documentation
- Do not weaken tenant isolation casually
- Do not log sensitive tokens, secrets, or full email bodies in production logs
- Preserve hashed verification/invite tokens at rest

### Existing Testing Patterns

- Unit/integration backend tests use xUnit + EF InMemory + FluentAssertions
- Existing auth tests validate queue creation and token hashing
- Existing end-to-end auth tests inspect queued notifications directly
- There is currently no strong evidence of dedicated `NotificationProcessorJob` coverage

## 3. Goals

1. Make OrvixFlow capable of real outbound email delivery in production.
2. Preserve `Provider=Console` local-dev behavior.
3. Add `Resend` as a first-class provider.
4. Fix SMTP behavior for both authenticated and unauthenticated SMTP.
5. Ensure queued auth and notification emails are actually deliverable from Hangfire/background context.
6. Add production-safe queue reliability mechanics:
   - retry/error metadata
   - duplicate-send protection
   - concurrency protection
7. Leave clear docs and session handoff points for future agents.

## 4. Defined Vs Underdefined

### Clearly Defined

- Providers required: `Console`, `Smtp`, `Resend`
- Need to fix SMTP credential handling
- Config must be env-driven
- Existing queue-driven architecture must remain the delivery path
- Reliability hardening is in scope

### Underdefined But Must Be Resolved During Execution

- Whether to keep `System.Net.Mail.SmtpClient` for now or switch to `MailKit`
- Exact shape of `NotificationQueue` retry/error fields
- Whether to mark failed notifications as permanent after threshold or keep retrying indefinitely
- Whether job-level duplicate-send protection is achieved by lock only, row-level state transition, or both
- Whether startup validation should fail hard for selected misconfigured providers or log only in Development

### Recommended Defaults For This Track

- Keep `SmtpClient` for minimal delta unless it blocks tests or correctness
- Add explicit queue metadata fields rather than burying errors in logs
- Add a max retry threshold and failed state to avoid infinite noisy retries
- Use both job-level concurrency protection and per-row defensive updates where practical
- Fail fast only when the selected provider is invalid or incomplete

## 5. Critical Risks

### Technical Risks

- `NotificationProcessorJob` may currently see zero pending notifications due to tenant filters in background context
- Sending may duplicate if overlapping Hangfire runs process the same rows
- Batch crash after provider send but before DB save can lead to resend
- Resend integration may be added correctly but still never used if the queue processor is ineffective
- SMTP configuration bugs can break dev/test and hide delivery issues

### Product / Operational Risks

- Gmail SMTP has sender restrictions and low limits compared with real production providers
- Resend sandbox/domain verification may block realistic testing if not configured early
- Lack of delivery status visibility will make support difficult

### Security Risks

- Logging bodies may expose verification/invite links
- Over-broad use of `IgnoreQueryFilters()` in the queue processor may create future leakage risk
- Provider secrets could leak through misconfigured diagnostics

## 6. No-Break Rules

1. Do not replace queued email delivery with direct inline sends from auth flows.
2. Do not break `Provider=Console` local behavior.
3. Do not weaken tenant isolation outside the narrow background-processing need.
4. Do not log verification tokens, invite tokens, SMTP passwords, or Resend API keys.
5. Do not remove hashed token behavior at rest.
6. Do not redesign the entire notification system during this track.
7. Do not mix mailbox OAuth token capture into this track.

## 7. Master Execution Strategy

### Session Order

1. Provider baseline and config surface
2. Queue processing correctness in background context
3. Queue reliability hardening
4. End-to-end validation and documentation

### Re-Read Checkpoints

- Before Session 2: re-read `memory/memory-risks.md`, `memory/memory-security.md`, `memory/auth.md`
- Before Session 3: re-read `NotificationProcessorJob`, `NotificationQueue`, `AppDbContext`
- Before Session 4: re-read `.env.example`, `docker-compose.yml`, auth tests, notification tests

## 8. Session-By-Session Execution Plan

---

## Session 1 - Provider Baseline And Config Surface

### Size

- Medium
- Fits the budget because it is bounded to provider implementation, DI, and config, with no queue-schema redesign yet.

### Goal

Introduce production-capable provider selection while preserving the current abstraction and local fallback behavior.

### Exact Scope

- Fix SMTP credential behavior
- Add `ResendEmailService`
- Extend `EmailOptions`
- Update DI provider selection
- Update config docs in `.env.example`
- Update runtime env wiring in `.env` if present locally and safe to edit
- Add provider-specific tests

### Why This Is Its Own Session

- Provider changes are conceptually independent from queue mechanics
- Easier to verify and revert if needed
- Keeps early session context small and implementation focused

### Prerequisites

- Re-read:
  - `tasks/production-email-sending.md`
  - `memory/memory-architecture.md`
  - `memory/memory-security.md`
  - `OrvixFlow.Infrastructure/DependencyInjection.cs`
  - `OrvixFlow.Infrastructure/Services/EmailOptions.cs`
  - `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs`
  - `OrvixFlow.Infrastructure/Services/MockEmailService.cs`

### Files / Components Likely Involved

- `OrvixFlow.Infrastructure/Services/EmailOptions.cs`
- `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs`
- `OrvixFlow.Infrastructure/Services/ResendEmailService.cs` (new)
- `OrvixFlow.Infrastructure/DependencyInjection.cs`
- `OrvixFlow.Api/appsettings.json`
- `.env.example`
- `.env`
- `docker-compose.yml`
- test files under `OrvixFlow.Tests/`

### Implementation Tasks

1. Inspect current `EmailOptions` shape and extend only with fields needed for provider selection and Resend.
2. Fix SMTP credentials assignment so credentials are only set when username/password are provided.
3. Decide whether SMTP sender address comes from existing options or needs stricter validation.
4. Implement `ResendEmailService` using `HttpClient`.
5. Add clear provider selection logic in DI:
   - `Console` -> `MockEmailService`
   - `Smtp` -> `SmtpEmailService`
   - `Resend` -> `ResendEmailService`
6. Add misconfiguration validation for the selected provider only.
7. Add/update config defaults in `appsettings.json` if appropriate without secrets.
8. Update `.env.example` with:
   - `EMAIL_PROVIDER`
   - SMTP variables
   - Resend variables
9. Update local `.env` only if the repo currently uses it for developer runtime and only with placeholders/non-sensitive examples.
10. Add tests for provider behavior and DI selection.

### Architecture Constraints

- `IEmailService` remains the single outbound contract
- Do not change auth flows in this session
- Do not change queue entity or queue job in this session

### Security Concerns

- No secret values in tracked files
- No provider secrets in logs or exception messages
- Avoid logging subject/body at info level in provider implementations

### Tests To Add / Update

- `SmtpEmailServiceTests`
  - authenticated SMTP path
  - unauthenticated SMTP path
- `ResendEmailServiceTests`
  - success path
  - non-success HTTP path
  - malformed config path
- DI/config test verifying provider selection

### Validation Checklist

- `Provider=Console` still works with no extra config
- `Provider=Smtp` resolves when SMTP config is present
- `Provider=Resend` resolves when API key/from address config is present
- Unsupported provider value fails clearly
- `dotnet test` passes for touched tests

### Definition Of Done

- Code compiles
- Provider selection works by config
- SMTP bug is fixed
- Resend provider exists and is test-covered
- Config docs are updated

### Handoff Notes For Next Session

- Do not claim production delivery is complete yet
- Next session must verify background queue processing correctness

---

## Session 2 - Queue Processing Correctness In Background Context

### Size

- Medium
- Fits the budget because it is concentrated on one job, one entity, and related tests, but requires careful security reasoning.

### Goal

Ensure queued auth and notification emails can actually be processed in Hangfire/background execution despite tenant filtering.

### Exact Scope

- Audit `NotificationProcessorJob` query/filter behavior
- Fix background visibility of pending notifications
- Validate processing of auth emails and usage alerts
- Add explicit tests for background delivery path

### Why This Is Its Own Session

- This is the highest-risk architecture gap
- It touches tenant isolation and must be reviewed separately from provider work

### Prerequisites

- Session 1 complete
- Re-read:
  - `memory/memory-security.md`
  - `memory/memory-risks.md`
  - `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
  - `OrvixFlow.Infrastructure/Data/AppDbContext.cs`
  - `OrvixFlow.Api/Services/TenantProvider.cs`

### Files / Components Likely Involved

- `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
- possibly `OrvixFlow.Core/Entities/NotificationQueue.cs`
- possibly helper service(s) if refactoring is necessary
- tests under `OrvixFlow.Tests/`

### Implementation Tasks

1. Inspect how the job queries pending notifications today.
2. Confirm whether it is currently blocked by EF query filters in background context.
3. Implement the narrowest safe fix.
4. If using `IgnoreQueryFilters()`, constrain the query tightly to pending queue rows only.
5. Preserve per-notification company identity in processing logic and logs.
6. Validate that auth and usage-alert notifications both still use the same processing path correctly.
7. Add job-focused tests that do not rely on request JWT context.

### Architecture Constraints

- Do not weaken request-path tenant isolation
- Keep background access narrowly scoped to queue processing
- Do not redesign all Hangfire tenant patterns in this session

### Security Concerns

- Any query-filter bypass must stay local to the job and be justified
- Do not expose cross-tenant queue data to controllers or general services

### Tests To Add / Update

- `NotificationProcessorJobTests`
  - processes queued verification email
  - processes queued invite email
  - processes queued usage alert
  - failure path leaves item available for retry handling later
  - works without HTTP/JWT tenant context

### Validation Checklist

- Background job can see pending notifications
- Auth queue rows are processed in tests
- Usage-alert rows are processed in tests
- Existing auth queue-creation tests still pass

### Definition Of Done

- Queue-driven delivery works in background context
- Tenant isolation for normal requests remains unchanged

### Handoff Notes For Next Session

- Queue correctness exists, but duplicate-send and retry metadata hardening still remain

---

## Session 3 - Queue Reliability Hardening

### Size

- Heavy
- Fits the budget because it is still bounded to notification delivery, but it may require entity changes, a migration, job changes, and broader test coverage.

### Goal

Make queue processing production-safe with retry/error metadata and duplicate-send protection.

### Exact Scope

- Extend `NotificationQueue` state model if needed
- Add retry/error metadata
- Add duplicate-send protection
- Add concurrency protection for job scheduling/execution
- Improve per-item persistence semantics

### Why This Is Its Own Session

- This is operational hardening, not base correctness
- It likely requires a migration and careful treatment of existing rows

### Prerequisites

- Session 2 complete
- Re-read:
  - `OrvixFlow.Core/Entities/NotificationQueue.cs`
  - `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
  - existing notification-related migrations

### Files / Components Likely Involved

- `OrvixFlow.Core/Entities/NotificationQueue.cs`
- `OrvixFlow.Infrastructure/Data/AppDbContext.cs`
- new EF migration under `OrvixFlow.Infrastructure/Migrations/`
- `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
- `OrvixFlow.Api/Program.cs` if scheduling/attributes need adjustment
- tests under `OrvixFlow.Tests/`

### Implementation Tasks

1. Define minimal queue metadata additions, likely including some of:
   - attempt count
   - last attempted at
   - last error
   - processing status beyond just `Processed`
   - failed/dead-letter marker
2. Add migration if entity changes are required.
3. Harden job flow so item state is persisted safely.
4. Prevent overlapping runs from double-processing the same rows.
5. Decide and implement retry threshold behavior.
6. Ensure successful delivery is marked exactly once.
7. Ensure failed rows remain diagnosable without exposing sensitive content.
8. Add tests covering retries, duplicates, and failure state transitions.

### Architecture Constraints

- Keep `NotificationQueue` generic for both auth emails and usage alerts
- Avoid full notification-framework redesign
- Minimize schema changes to only what the reliability requirements need

### Security Concerns

- `LastError` or similar must not store provider secrets or full email body
- Duplicate-send protection must not rely on insecure heuristics

### Tests To Add / Update

- retry increment tests
- permanent failure threshold tests
- concurrency / duplicate-send protection tests
- migration compatibility tests if needed

### Validation Checklist

- overlapping job execution does not double-send the same notification
- failed items store actionable metadata
- successful items are not retried
- repeated failures stop at defined threshold or move into failed state
- no sensitive values are persisted in cleartext error metadata

### Definition Of Done

- Queue has explicit reliability semantics
- Duplicate-send risk is materially reduced
- Failure diagnosis is possible from app state/logs

### Handoff Notes For Next Session

- Next session should focus on end-to-end validation and docs, not additional architecture changes unless defects are found

---

## Session 4 - End-To-End Validation, Docs, And Memory Update

### Size

- Medium
- Fits the budget because this is mostly validation, targeted fixes, and documentation updates.

### Goal

Prove that the production email path works end-to-end and leave the repo with clear operational guidance.

### Exact Scope

- Validate auth and notification flows with the real queue processor
- Validate all provider modes
- Update docs/memory if architecture or operational behavior materially changed

### Why This Is Its Own Session

- Keeps verification explicit
- Prevents endless implementation churn after production-readiness is already achieved

### Prerequisites

- Sessions 1-3 complete
- Re-read:
  - `.env.example`
  - `docker-compose.yml`
  - `AuthService`
  - `NotificationProcessorJob`
  - relevant updated tests

### Files / Components Likely Involved

- test files
- `.env.example`
- `docker-compose.yml`
- `memory/` docs if major architecture/operations changed

### Implementation Tasks

1. Run targeted tests, then `dotnet test`.
2. Validate `Provider=Console` behavior still works.
3. Validate SMTP with a safe test setup.
4. Validate Resend with sandbox/test domain if available.
5. Run a register -> queue -> process -> verify -> login flow.
6. Run an invite -> queue -> process -> accept flow.
7. Update memory docs if this track materially changed architecture or operational patterns.
8. Verify `.env.example` and local `.env` remain aligned with implemented options.

### Architecture Constraints

- Prefer fixing only discovered defects
- Avoid introducing new feature scope here

### Security Concerns

- Use non-production secrets/accounts for verification
- Avoid saving real secrets into tracked files while testing

### Tests To Add / Update

- Any missing end-to-end delivery tests discovered during validation
- Existing auth tests if queue metadata changes required assertion updates

### Validation Checklist

- register/verification email delivered through queue processor
- invite email delivered through queue processor
- usage alert queue still works
- all three provider modes behave as expected
- docs reflect final config surface
- `dotnet test` passes

### Definition Of Done

- Outbound production email track is implementation-complete and verified
- Docs/config examples are aligned with code
- Memory is updated if architectural behavior changed materially

### Handoff Notes

- Stop after this session unless mailbox OAuth track is explicitly being executed next

## 9. Stop Conditions For The Execution Agent

The agent must stop and ask for clarification if any of the following occur:

- Mailbox OAuth concerns start leaking into outbound email implementation
- The intended SMTP library migration becomes larger than a minimal provider fix
- Queue reliability requires a broader messaging redesign
- Tenant-filter handling appears to require a platform-wide background-job tenancy refactor
- Deliverability requirements expand into bounce/complaint webhook handling in this track

## 10. Final Acceptance Criteria

- `IEmailService` supports `Console`, `Smtp`, and `Resend`
- SMTP works with and without credentials
- Queue-driven auth email delivery works in Hangfire/background execution
- Queue has retry/error metadata and duplicate-send protection
- Existing auth token hashing remains intact
- Local/dev mode remains easy to run
- `.env.example` and local `.env` reflect the implemented configuration surface
- Relevant tests pass and validation has been performed
