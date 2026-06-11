# OrvixFlow — Production Execution Progress

> Last updated: 2026-06-11  
> Baseline: 561 tests passing · Last commit: `893b69f` (2026-04-24)

---

## Phase Status Summary

| Phase | Name | Status | Owner | Started | Completed |
|---|---|---|---|---|---|
| Phase 0 | Security & Stability Hardening | 🟡 Validation Pending | Antigravity AI | 2026-06-11 | — |
| Phase 1 | Production Email Validation | 🟢 Complete | Antigravity AI | 2026-06-11 | 2026-06-11 |
| Phase 2 | Stripe Live-Mode & Subscription Completeness | 🔴 Not Started | — | — | — |
| Phase 3 | Mailbox OAuth Credential Capture | 🔴 Not Started | — | — | — |
| Phase 4 | CI/CD Pipeline | 🔴 Not Started | — | — | — |
| Phase 5 | Observability, Database Backup & Production Ops | 🔴 Not Started | — | — | — |

---

## Phase 0 — Security & Stability Hardening

**Status:** 🟡 Validation Pending  
**Owner:** Antigravity AI  
**Started:** 2026-06-11  
**Completed:** —  
**Estimated effort:** ~1 week  
**Blockers:** None (standalone fixes)

### Task Checklist

- [x] P0-1: Secure n8n in docker-compose.yml (require stable encryption key, bootstrap owner login)
- [x] P0-2: Add `STRIPE_WEBHOOK_SECRET` to `.env.example`
- [x] P0-3: Add rate limiting to `POST /api/auth/register` (10/hour per IP)
- [x] P0-4: Implement CSP headers (backend `Program.cs` + frontend `next.config.ts`)
- [x] P0-5: Resolve dual admin route (`/admin` vs `/(admin)`) — audit and consolidate

### Notes

- Register endpoint now uses a dedicated fixed-window `register` limiter (10/hour per IP) with coverage in `Phase0SecurityHardeningTests.cs`.
- API and Next.js now emit CSP headers; frontend policy keeps `'unsafe-inline'` temporarily for Next.js compatibility.
- API security-header middleware was moved ahead of OpenAPI and switched to `Response.OnStarting(...)` so CSP now appears on live `/health/*` and Swagger responses.
- Admin pages were consolidated under `orvixflow-web/app/(admin)/admin/**`, preserving `/admin/*` URLs while removing the split route tree.
- `docker compose --env-file /dev/null config` now fails fast when `N8N_ENCRYPTION_KEY` is missing; `docker-compose.yml` also now uses `EXECUTIONS_MODE=regular` for current n8n images.
- Current n8n image no longer honors legacy `N8N_BASIC_AUTH_*` vars. Local validation now uses owner bootstrap via `N8N_INSTANCE_OWNER_*` plus `POST /rest/owner/setup`, after which `/rest/login` succeeds and the instance is login-protected.
- Local `n8n_data` was reset during validation because the prior volume was encrypted with a different key.

---

## Phase 1 — Production Email Validation

**Status:** 🟢 Complete  
**Owner:** Antigravity AI  
**Started:** 2026-06-11  
**Completed:** 2026-06-11  
**Estimated effort:** ~1 week  
**Blockers:** None

### Task Checklist

- [x] P1-1: Run register → queue → process → delivery e2e test with real provider (documented manual verification steps)
- [x] P1-2: Verify NotificationProcessorJob with IgnoreQueryFilters() in background context
- [x] P1-3: Write UsagePeriodRolloverJob unit tests
- [x] P1-4: Update .env.example with complete Resend sandbox example
- [x] P1-5: Configure Resend domain and from-address for production (operational instructions documented)
- [x] P1-6: Update memory/memory-security.md to correct refresh token documentation (R10)

### Notes

<!-- Add notes here as work progresses -->

---

## Phase 2 — Stripe Live-Mode & Subscription Completeness

**Status:** 🔴 Not Started  
**Owner:** —  
**Started:** —  
**Completed:** —  
**Estimated effort:** ~1 week  
**Blockers:** Phase 0 must be complete; Stripe production account required

### Task Checklist

- [ ] P2-1: Create Stripe production account, generate live-mode API keys
- [ ] P2-2: Create Stripe products and prices for Free/Starter/Pro plans
- [ ] P2-3: Register webhook endpoint in Stripe production dashboard
- [ ] P2-4: Implement StripeService.ReactivateSubscriptionAsync
- [ ] P2-5: Implement StripeService.GetSubscriptionDetailsAsync
- [ ] P2-6: Replace proration estimate with real Stripe preview
- [ ] P2-7: Configure Stripe Customer Portal (branding, allowed plan changes)
- [ ] P2-8: Add STRIPE_WEBHOOK_SECRET to all deployment configurations

### Notes

<!-- Add notes here as work progresses -->

---

## Phase 3 — Mailbox OAuth Credential Capture

**Status:** 🔴 Not Started  
**Owner:** —  
**Started:** —  
**Completed:** —  
**Estimated effort:** 4–6 weeks (6 sessions)  
**Blockers:** Phase 0 (n8n secured), Phase 1 (email working)

### Session Checklist

- [ ] S1: Design finalization (encryption, scope, connect/disconnect UX)
- [ ] S2: Encrypted MailboxCredential entity + storage service + EF migration
- [ ] S3: Backend OAuth link/reconnect/disconnect APIs
- [ ] S4: Frontend connect flow (inbox settings, provider buttons)
- [ ] S5: n8n credential provisioning with real provider data
- [ ] S6: End-to-end validation, docs, memory update

### Notes

<!-- Add notes here as work progresses -->

---

## Phase 4 — CI/CD Pipeline

**Status:** 🔴 Not Started  
**Owner:** —  
**Started:** —  
**Completed:** —  
**Estimated effort:** ~1 week  
**Blockers:** Phase 0 (env documented); production deployment target must be selected

### Task Checklist

- [ ] P4-1: Create .github/workflows/ci.yml (build + test on PR)
- [ ] P4-2: Create .github/workflows/deploy.yml (deploy on main push)
- [ ] P4-3: Set up GitHub Secrets for all production env vars
- [ ] P4-4: Add .env.example validation step in CI
- [ ] P4-5: Add Docker image build test in CI (build but don't push on PRs)

### Notes

<!-- Add notes here as work progresses -->

---

## Phase 5 — Observability, Database Backup & Production Ops

**Status:** 🔴 Not Started  
**Owner:** —  
**Started:** —  
**Completed:** —  
**Estimated effort:** 1–2 weeks  
**Blockers:** Phase 0 (n8n secured), Phase 4 (CI/CD); production infrastructure required

### Task Checklist

**Observability:**
- [ ] P5-1: Add OpenTelemetry to .NET API (traces + metrics)
- [ ] P5-2: Add structured logging sink (Seq, Loki, or DataDog)
- [ ] P5-3: Expose Hangfire job failure metrics + alerting
- [ ] P5-4: Set up uptime monitoring for /health/rag and /health/storage
- [ ] P5-5: Add Sentry for frontend exception tracking

**Database Backup:**
- [ ] P5-6: Configure automated pg_dump cron job (daily, encrypted, to S3/MinIO)
- [ ] P5-7: Test restore from backup (verify row counts and integrity)
- [ ] P5-8: Document RPO/RTO targets and backup retention policy

**Deployment:**
- [ ] P5-9: Create docker-compose.prod.yml with TLS, authenticated n8n, health checks
- [ ] P5-10: Document domain setup, TLS certificate renewal, port mapping
- [ ] P5-11: Create operational runbooks (rollback, migrations, backup, job recovery)

### Notes

<!-- Add notes here as work progresses -->

---

## Change Log

| Date | Agent | Change |
|---|---|---|
| 2026-06-11 | Antigravity AI | Initial progress file created from audit |
| 2026-06-11 | Antigravity AI | Phase 0 implementation complete; awaiting optional live runtime smoke checks |
| 2026-06-11 | OpenCode | Live smoke found API CSP gap and invalid n8n execution mode; fixed API middleware ordering/OnStarting and updated n8n execution mode to `regular` |
| 2026-06-11 | OpenCode | Reset local `n8n` state, switched compose/docs from legacy `N8N_BASIC_AUTH_*` to current owner-bootstrap auth, and verified `/rest/login` succeeds |
| 2026-06-11 | Antigravity AI | Phase 1 implementation complete; added rollover job unit tests, updated environment templates, corrected refresh token security memory, and verified processor job |
