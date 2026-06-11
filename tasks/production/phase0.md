# Phase 0 — Security & Stability Hardening

> **Status:** Validation Pending  
> **Estimated effort:** 1 week  
> **Dependencies:** None — all tasks are standalone  
> **Blocks:** All subsequent phases  

---

## Goal

Close all critical and high-severity security gaps before any production traffic or further development. This phase has no external dependencies and produces zero new features — it only removes production blockers.

---

## Why

Three of the five most dangerous production gaps exist in infrastructure configuration, not in application code:

1. **n8n admin UI is unauthenticated** — any network-accessible n8n instance exposes full workflow creation/editing to anyone who reaches port 5678. This is a critical exposure.
2. **`N8N_ENCRYPTION_KEY` is the dev placeholder** — all n8n credentials are encrypted with a publicly known key. Trivially reversible in production.
3. **`STRIPE_WEBHOOK_SECRET` is missing from `.env.example`** — any operator deploying from the template silently omits this, causing Stripe to reject all webhooks and subscriptions to never activate.

The remaining two tasks (register rate limiting, CSP, admin route consolidation) close medium-severity gaps that are cheap to fix now and expensive to retrofit later.

---

## Scope

- Secure n8n in `docker-compose.yml`
- Document `STRIPE_WEBHOOK_SECRET` in `.env.example`
- Add rate limiting to `POST /api/auth/register`
- Implement Content Security Policy headers (backend + frontend)
- Audit and consolidate the dual admin route structure

---

## Out of Scope

- No new features
- No database migrations
- No Stripe live-mode configuration (that is Phase 2)
- No email delivery validation (that is Phase 1)
- No CI/CD setup (that is Phase 4)
- Do not change any auth logic, RBAC, or billing code

---

## Dependencies

None. All five tasks are independent of each other and of other phases. They may be executed in any order or in parallel.

---

## Files / Components Likely Involved

| File | Task |
|---|---|
| `docker-compose.yml` | P0-1: n8n auth + encryption key |
| `.env.example` | P0-1 (N8N vars), P0-2 (STRIPE_WEBHOOK_SECRET) |
| `OrvixFlow.Api/Program.cs` | P0-3: register rate limit; P0-4: CSP header middleware |
| `orvixflow-web/next.config.ts` | P0-4: CSP headers |
| `orvixflow-web/app/admin/` | P0-5: dual route audit |
| `orvixflow-web/app/(admin)/` | P0-5: dual route audit |
| `orvixflow-web/middleware.ts` | P0-5: route protection verification |
| `OrvixFlow.Tests/` | P0-3: register rate limit test |

---

## Implementation Tasks

### P0-1 — Secure n8n in docker-compose.yml

**Files:** `docker-compose.yml`, `.env.example`

- [x] Configure a managed instance owner for current n8n builds:
  ```yaml
  - N8N_INSTANCE_OWNER_MANAGED_BY_ENV=true
  - N8N_INSTANCE_OWNER_EMAIL=${N8N_OWNER_EMAIL}
  - N8N_INSTANCE_OWNER_FIRST_NAME=${N8N_OWNER_FIRST_NAME}
  - N8N_INSTANCE_OWNER_LAST_NAME=${N8N_OWNER_LAST_NAME}
  - N8N_INSTANCE_OWNER_PASSWORD_HASH=${N8N_OWNER_PASSWORD_HASH}
  ```
- [x] Change `N8N_ENCRYPTION_KEY` default from `dev-encryption-key-change-me` to require an env var with no fallback:
  ```yaml
  - N8N_ENCRYPTION_KEY=${N8N_ENCRYPTION_KEY:?N8N_ENCRYPTION_KEY must be set}
  ```
  (The `:?` syntax causes `docker compose up` to fail if the var is not set — safe by design)
- [x] Add to `.env.example`:
  ```
  # n8n Instance Owner Authentication (REQUIRED in production)
  N8N_OWNER_EMAIL=admin@orvixflow.local
  N8N_OWNER_FIRST_NAME=Orvix
  N8N_OWNER_LAST_NAME=Admin
  N8N_OWNER_PASSWORD_HASH=REPLACE-WITH-A-BCRYPT-HASH
  N8N_ENCRYPTION_KEY=REPLACE-WITH-A-64-CHAR-RANDOM-KEY
  ```
- [x] Complete the initial owner bootstrap once via `POST /rest/owner/setup` so `showSetupOnFirstLoad` becomes `false`
- [x] Generate a real `N8N_ENCRYPTION_KEY` value (64-char hex) and store in local `.env` (gitignored)

> ⚠️ **IMPORTANT:** If n8n already has workflows created with the dev encryption key, those workflows' credentials are encrypted with that key. Rotating the key without migrating credentials will break existing workflows. Before rotating in any non-fresh environment, export all n8n workflows, rotate the key, and re-import to force re-encryption.

### P0-2 — Add STRIPE_WEBHOOK_SECRET to .env.example

**File:** `.env.example`

- [x] Add the following to the Stripe section in `.env.example`:
  ```
  # Stripe Webhook Secret (REQUIRED — get from Stripe dashboard → Webhooks → signing secret)
  STRIPE_WEBHOOK_SECRET=whsec_your_webhook_signing_secret_here
  ```
- [x] Verify `docker-compose.yml` maps it correctly (it already does: `Stripe__WebhookSecret: ${STRIPE_WEBHOOK_SECRET}`)
- [x] Verify `Program.cs` startup warning fires when it's missing (already implemented at line ~240)

### P0-3 — Add Rate Limiting to POST /api/auth/register

**Files:** `OrvixFlow.Api/Program.cs`, `OrvixFlow.Api/Controllers/AuthController.cs`, `OrvixFlow.Tests/`

- [x] In `Program.cs`, add a new rate limiting policy in the existing `AddRateLimiter` block:
  ```csharp
  // P0-3: Rate limiting on registration endpoint to prevent bulk account creation and email flooding
  // 10 attempts per hour per IP address.
  options.AddPolicy("register", context =>
      RateLimitPartition.GetFixedWindowLimiter(
          partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          factory: _ => new FixedWindowRateLimiterOptions
          {
              PermitLimit = 10,
              Window = TimeSpan.FromHours(1),
              QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
              QueueLimit = 0
          }));
  ```
- [x] In `AuthController.cs`, add `[EnableRateLimiting("register")]` to the `Register` action (same pattern as login's `[EnableRateLimiting("login")]`)
- [x] Add unit test in `OrvixFlow.Tests/AuthControllerTests.cs` (or a new `RegisterRateLimitTests.cs`):
  - Test: `Register_ExceedsRateLimit_Returns429` — mock the rate limiter or test the policy configuration
  - Pattern: follow the existing `AuthControllerTests` setup (InMemory DB, `MockTenantProvider`)

**Code reference:** `Program.cs` lines 84–95 show the existing `login` rate limiter pattern. Follow that exactly.

### P0-4 — Implement CSP Headers

**Files:** `OrvixFlow.Api/Program.cs`, `orvixflow-web/next.config.ts`

#### Step 1: Audit inline scripts

Before writing the CSP policy, audit all inline scripts:

- In `orvixflow-web/app/`, grep for `<script>` tags with inline JS:
  ```bash
  grep -r "<script" orvixflow-web/app/ --include="*.tsx" --include="*.ts" | grep -v "src="
  ```
- In `OrvixFlow.Api/`, check if any razor/HTML responses use inline scripts (unlikely — API only)

#### Step 2: Backend CSP

In `Program.cs`, in the existing security headers middleware (lines ~181–189), add:
```csharp
context.Response.Headers.Append("Content-Security-Policy",
    "default-src 'self'; " +
    "script-src 'self'; " +        // tighten if no inline scripts found in audit
    "style-src 'self' 'unsafe-inline'; " +  // most CSS frameworks need this
    "img-src 'self' data: https:; " +
    "font-src 'self' https:; " +
    "connect-src 'self'; " +
    "frame-ancestors 'none';");
```

If inline scripts exist and cannot be immediately moved: use `'unsafe-inline'` as a temporary measure and document it as a known gap to close. Do NOT leave CSP disabled entirely.

#### Step 3: Frontend CSP

In `orvixflow-web/next.config.ts`, uncomment and configure the CSP header in the `headers()` array. Use the same policy as the backend, adjusting for Next.js's needs:
- `connect-src` must include the API base URL (from env)
- If Google/Microsoft OAuth is used: `connect-src` must include `https://accounts.google.com`, `https://login.microsoftonline.com`

#### Step 4: Verify

```bash
# Verify header is present on API response
curl -I http://localhost:5000/health/rag
# Verify header is present on frontend
curl -I http://localhost:3000
```

### P0-5 — Audit and Consolidate Dual Admin Route Structure

**Files:** `orvixflow-web/app/admin/`, `orvixflow-web/app/(admin)/`, `orvixflow-web/middleware.ts`

- [x] Enumerate all pages in both route trees:
  ```
  app/admin/companies/[id]/audit/page.tsx
  app/admin/companies/[id]/page.tsx
  app/admin/modules/page.tsx
  app/admin/page.tsx
  app/admin/plans/[id]/page.tsx
  app/admin/plans/page.tsx
  app/admin/tenants/page.tsx
  app/admin/test/page.tsx
  
  app/(admin)/companies/[id]/inbox/page.tsx
  app/(admin)/inbox-metrics/page.tsx
  app/(admin)/layout.tsx
  app/(admin)/page.tsx
  ```
- [x] Check `orvixflow-web/middleware.ts` to determine which routes are protected by which middleware matchers
- [x] Verify both route trees enforce `SuperAdmin` or `InternalOperator` role via session check
- [x] Decision: consolidate all admin pages under `app/(admin)/` (the newer group layout pattern)
  - Move any remaining `app/admin/` pages into `app/(admin)/`
  - Update navigation links if any reference `/admin/...` paths
  - Remove the `app/admin/` directory once all pages are migrated
- [x] If `app/admin/` and `app/(admin)/` serve different purposes (e.g., legacy vs new): document this explicitly in `app/(admin)/layout.tsx` with a comment

---

## Architecture Rules

- Rate limiting must use the existing `AddRateLimiter` / `[EnableRateLimiting]` pattern from `Program.cs` — do not introduce a new rate limiting library
- CSP implementation must be in the existing security headers middleware (do not add a new middleware for it)
- All frontend route protection must go through `middleware.ts` and NextAuth session — do not add per-page auth checks in `layout.tsx` as the primary gate
- Do not modify any entity, migration, or service during this phase

---

## Tests Required

### Unit Tests

- `Register_ExceedsRateLimit_Returns429` — verify the `register` rate limit policy is applied

### Manual Validation

- `docker compose up` must fail with a clear error if `N8N_ENCRYPTION_KEY` is not set
- n8n at `http://localhost:5678` must require authenticated owner login after initial bootstrap
- `curl -X POST http://localhost:5000/api/auth/register` 11 times in under an hour — 11th must return 429
- `curl -I http://localhost:5000/health/rag` — must include `Content-Security-Policy` header
- `curl -I http://localhost:3000` — must include `Content-Security-Policy` header
- Navigate to `/admin` — must require SuperAdmin or InternalOperator session

---

## Validation Checklist

- [ ] `docker compose up` fails with clear error if `N8N_ENCRYPTION_KEY` env var is missing
- [x] n8n owner login is required after initial bootstrap
- [ ] `N8N_ADMIN_USER`, `N8N_ADMIN_PASSWORD`, `N8N_ENCRYPTION_KEY` are documented in `.env.example`
- [ ] `STRIPE_WEBHOOK_SECRET` is documented in `.env.example`
- [ ] `POST /api/auth/register` returns 429 after 10 attempts per hour
- [ ] Register rate limit test passes: `dotnet test --filter "FullyQualifiedName~Register"`
- [ ] `Content-Security-Policy` header present on API responses
- [ ] `Content-Security-Policy` header present on frontend responses
- [ ] All admin pages enforce SuperAdmin/InternalOperator role
- [ ] No admin pages are accessible to CompanyOwner, CompanyAdmin, or CompanyMember roles
- [ ] `dotnet test` — still 561 passing, 0 failing
- [ ] `npm run build && npm run lint && npm run test` — all pass

---

## Definition of Done

All five items are complete AND:
- 0 test failures in `dotnet test`
- 0 TypeScript errors in `npm run build`
- CSP header present on both API and frontend
- n8n requires owner login
- `.env.example` documents all required secrets
- Register endpoint has rate limiting

---

## Common Mistakes

1. **Forgetting to update `.env.example` after adding compose vars** — always update the template alongside the compose file
2. **Writing CSP as `'unsafe-inline' 'unsafe-eval'`** — this defeats the purpose. Audit first, move inlines to files, then write strict policy
3. **Only protecting one admin route tree** — both `/admin` and `/(admin)` must be protected until one is removed
4. **Using `SlideWindow` rate limiter instead of `FixedWindow`** — the project already uses `FixedWindowRateLimiter`. Be consistent.
5. **Not testing the rate limit with actual HTTP calls** — unit tests on the policy configuration are insufficient; verify with curl

---

## Handoff to Phase 1

Before Phase 1 starts, confirm:

1. `docker compose up` works with updated `.env` (all new required vars filled in)
2. 0 test failures
3. CSP header is live
4. n8n is authenticated
5. Register rate limiting is active

Phase 1 requires a Resend account and verified sending domain. Prepare those credentials before starting Phase 1.
