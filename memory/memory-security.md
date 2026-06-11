# OrvixFlow — Security Memory

> **Last updated:** 2026-04-15
> **Based on:** `tasks/claude-security-review-08-04.md` (dual AI review, Phase 1–4 remediation complete)
> **Remediation status:** All Critical + High issues fixed as of Phase 2. Phase 3 complete (2026-04-14). Phase 4 complete (2026-04-14). One deferred item: F-20 (MinIO) is a separate implementation track.

This document is the single authoritative reference for the security model of OrvixFlow. Future agents and engineers must read this **before touching auth, authorization, tenant isolation, secrets, or billing code.**

---

## 1. Security Overview

OrvixFlow is a multi-tenant SaaS with a two-layer role model, EF Core query-filter-based tenant isolation, JWT bearer auth, and n8n workflow integration. Security is primarily enforced at the **ASP.NET Core API layer** (not the frontend). The frontend enforces UI-only guards as defense-in-depth; they are **not the security boundary**.

### Core Security Principles

1. **Tenant isolation is the most critical invariant.** Every EF Core query on tenant-scoped data goes through global query filters. Any bypass (`IgnoreQueryFilters()`) requires explicit justification and admin-level authorization enforcement.
2. **Authorization happens at the API, not the frontend.** Next.js middleware and client-side role checks are defense-in-depth only.
3. **JWT claims are the single source of truth for identity.** No header-based tenant/identity override is accepted for regular users.
4. **Secrets never live in tracked files.** All secrets load from `.env` (gitignored) or environment variables.
5. **Admin access is audited.** All impersonation and destructive admin actions must produce structured log/audit entries.

---

## 2. Authentication

### How Login/Session/JWT Works

| Step | What happens | Key file |
|------|-------------|----------|
| Registration | `AuthService.RegisterAsync` — bcrypt hash, `EmailVerified=false`, verification email sent | `AuthService.cs` |
| Email verification | Token (TTL 24–48h) validated, `EmailVerified=true` set, login allowed | `AuthController.cs`, `AuthService.cs` |
| Login | Credentials validated, JWT minted via `MintJwtAsync` (60-min lifetime) | `AuthService.cs` |
| OAuth login | `ProvisionOAuthUserAsync` — if email exists with different provider, **returns error** (no silent merge) | `AuthService.cs:107-119` |
| Company switch | New JWT minted with updated `TenantId` / `ActiveCompanyId` / `Role` | `AuthController.cs` |
| Frontend session | NextAuth 5 JWT strategy — token stored in HttpOnly session cookie | `orvixflow-web/auth.ts` |

### JWT Claims (exact names matter)

```
sub            → UserId (Guid)
email          → user@company.com
TenantId       → Guid (same as CompanyId)
ActiveCompanyId→ Guid (same as TenantId; legacy alias — both resolved)
Role           → company role OR platform role (see §3)
Plan           → "Free" | "Trialing" | "Pro" | ...
DisplayName    → display string
```

> **GOTCHA:** `TenantProvider` reads BOTH `TenantId` and `ActiveCompanyId` claims. If you add a new claim affecting tenant resolution, update **both** `MintJwtAsync` AND `BuildProfileAsync` in `AuthService.cs`.

### Key Security Properties of Current Auth

- **JWT lifetime:** 60 minutes (down from 7 days — F-01 fixed).
- **Refresh Token System:** Fully implemented to support continuous session renewal:
  - **Lifetime:** 7-day TTL.
  - **Opaque Format:** Tokens are formatted as `lookupKey.secret` (opaque token format).
  - **Hash-based Storage:** Tokens are hashed using SHA-256 before storing in the `RefreshToken` database table (separate from JWTs).
  - **Rotation on Use:** On each refresh request (`RefreshSessionAsync`), the old refresh token is revoked (its `RevokedAt` timestamp is set) and a new one is returned.
  - **Family-based Revocation:** Tokens in the same family are linked via `FamilyId`. If explicit logout is performed (`LogoutAsync`), all active tokens in that family are revoked. If token reuse/theft is suspected or membership is inactive, session creation falls back to the default active company or fails.
- **No server-side blacklist** — a revoked membership is only effective after the current JWT expires (max 60 min).
- **Password requirements:** min 12 chars, lower + upper + digit + special char (F-04 fixed via `ValidatePasswordComplexity()`).
- **Rate limiting on login:** 5 attempts/min per IP (sliding window) via `[EnableRateLimiting("login")]` (F-03 fixed).
- **Email verification required:** Registration produces an unverified account; login blocked until email confirmed (F-33 fixed).
- **bcrypt** is used for password hashing — correct, do not change.

### Known Pitfalls

- **Do not use `Include(u => u.Tenant)` in existence-check queries.** EF Core InMemory returns `null` for the entire result when the related entity doesn't exist, silently bypassing security checks. See `memory-risks.md` for the safe pattern.
- **OAuth linking is intentionally strict.** `ProvisionOAuthUserAsync` rejects any OAuth login whose email matches an existing account using a different provider, with a clear error. Do not "improve" this by silently merging — it was a critical account-takeover vector (F-02).

---

## 3. Authorization

### Two-Layer Role System

> **This is the most common source of bugs.** Always keep these two layers completely separate.

**Layer 1 — Global (platform-level) — `User.Role`**

| Value | Meaning |
|-------|---------|
| `SuperAdmin` | Full platform, all mutations |
| `InternalOperator` | Read-only platform admin |
| `""` (empty string) | Normal user — no global role |

Stored in `User.Role`. Only populated for platform staff. **Never set `User.Role` to a company role value.**

**Layer 2 — Company (org-level) — `UserCompanyMembership.CompanyRole`**

| Value | Meaning |
|-------|---------|
| `CompanyOwner` | Full control within their company |
| `CompanyAdmin` | Delegated company management |
| `CompanyMember` | Belongs to the company but relies on department memberships for scoped access |

Stored in `UserCompanyMembership.CompanyRole`. One row per user-company pair. Legacy values `DepartmentManager`, `Operator`, and `Viewer` are migration-era aliases only during the RBAC redesign rollout and must not be reintroduced in new logic.

**Layer 3 — Department (org-subscope) — `UserDepartmentMembership.DepartmentRole`**

| Value | Meaning |
|-------|---------|
| `DepartmentManager` | Can manage invites, assignments, and department-scoped data in that department |
| `DepartmentOperator` | Can work within that department's modules/data without management authority |

Stored per `UserDepartmentMembership` row. The same user may be `DepartmentManager` in one department and `DepartmentOperator` in another.

**JWT `Role` claim** — resolved in `MintJwtAsync`:
- If `User.Role` is a platform admin role → JWT `Role` = global role.
- Otherwise → JWT `Role` = `UserCompanyMembership.CompanyRole` (`CompanyOwner`, `CompanyAdmin`, or `CompanyMember`).
- Department role is **never** trusted from JWT; it must be resolved from `UserDepartmentMembership`.

**Key files:**
- `OrvixFlow.Core/Authorization/Roles.cs` — canonical enum + extension methods (`IsHigherThan`, `IsPlatformAdmin`, `IsCompanyAdmin`, `ParseRole`, department-role helpers).
- `OrvixFlow.Infrastructure/Auth/AccessResolver.cs` — uses active department memberships for fallback permission resolution.
- `OrvixFlow.Infrastructure/Auth/ScopeContext.cs` — uses JWT role only for company-wide vs scoped decisions.
- `OrvixFlow.Api/Controllers/InviteController.cs` and `OrvixFlow.Api/Controllers/TeamController.cs` — enforce department-manager authority via DB membership checks.

### Permission Resolution

1. `RequireModuleAttribute` — checks company plan entitlement (`CanUseModuleAsync`) **AND** user-level `CanUse` permission via `AccessResolver.GetEffectivePermissionsAsync()` (F-07 fixed). `CompanyAdmin` and above bypass user-level check.
2. `AccessResolver.GetEffectivePermissionsAsync()` — resolves Company → Department → User scope chain and uses active department memberships for `CompanyMember` fallback access.
3. `ModulePermissionGrant` — specific permissions: `CanView`, `CanUse`, `CanTest`, `CanConfigure`, `CanManageIntegrations`, `CanManagePrompts`, `CanViewLogs`, `IsAdmin`.

### Admin Panel Authorization

- **SuperAdmin:** Full read/write on all `/api/admin/*` endpoints.
- **InternalOperator:** Read-only on GET endpoints only. Mutation attempts return 403.
- **Policy names registered in `Program.cs`:** `SuperAdminOnly` (mutations), `PlatformAdmin` (reads).
- **Privilege escalation protection:** Invitation role ceiling enforced — callers cannot assign a role higher than their own (`IsHigherThan` check in `InviteController`, F-08 fixed).

### Critical Rules

- Never compare `User.Role` against company role strings (e.g., `CompanyOwner`).
- `AccessResolver` must not fall back to legacy company-role checks for `Operator`/`Viewer`; it should use active department memberships for scoped access.
- `ScopeContext` may use the JWT `Role` claim only to distinguish company-wide access (`CompanyOwner`/`CompanyAdmin`) from department-scoped `CompanyMember` access.
- When adding a role: add to **both** `Core/Authorization/Roles.cs` (enum/extensions) **and** `Api/Roles.cs` (impersonation gating) if applicable.

---

## 4. Tenant Isolation

### Invariant

Every entity has a `TenantId` or `CompanyId` field. `AppDbContext` applies global EF Core query filters. These filters are **always active by default**. This is the primary data isolation mechanism.

**Location:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs:168-185`

### Tenant Resolution (priority order)

1. JWT claim `TenantId` (preferred)
2. JWT claim `ActiveCompanyId` (legacy alias — same concept)
3. For platform admins: `X-Impersonate-Tenant` header (role-checked + logged)
4. **No other fallback** — `X-Tenant-ID` as a general fallback was REMOVED (F-09 fixed).

> For webhook paths (`/api/webhook/inbox`), tenant is resolved via HMAC-validated `X-Tenant-ID` in a **separate webhook-only provider**, not the general `TenantProvider`.

### Safe Bypass Pattern (admin only)

```csharp
// ONLY used in admin-facing service methods that already enforce IsSuperAdmin()/IsGlobalAdmin()
_db.Entity.IgnoreQueryFilters()
```

**Methods that MUST use `.IgnoreQueryFilters()`** (and already do — maintain this):
- `CompanySubscriptionService.GetSubscriptionAsync()`
- `CompanySubscriptionService.AssignPlanAsync()` (existing subscription lookup)
- `EntitlementResolver.GetSubscriptionAsync()`
- `EntitlementResolver.GetEntitlementOverrideAsync()`
- `EntitlementResolver.GetModuleOverridesAsync()`

> **When adding new admin queries:** Always check if the target entity has a query filter in `AppDbContext`. If yes, add `.IgnoreQueryFilters()` AND verify the endpoint has `IsSuperAdmin()` / `IsGlobalAdmin()` enforcement.

### Admin Impersonation

Path: `X-Impersonate-Tenant: <guid>` header.
- Only accepted if `UserRoleExtensions.ParseRole(roleClaim).IsPlatformAdmin()`.
- Produces a structured `LogWarning` on every use: `"SECURITY: Admin impersonation started"` with `AdminUserId`, `ImpersonatedTenantId`, `RemoteIp`.
- **Limitation:** Logging only (no `AuditTrail` DB write yet). Future improvement: write to `AuditTrail` for DB-level traceability.

### Cross-Tenant Restrictions

- Background jobs: use `BackgroundTenantProvider` / `ITenantProviderFactory` to inject the correct tenant context without a JWT.
- Image resolver vector fallback in tests: **must manually filter by `TenantId`** since EF InMemory doesn't support pgvector operations. The filter must not be skipped even in test code.
- Admin queries: always use `IgnoreQueryFilters()` — the tenant filter will otherwise return only the admin's own company data, causing silent data loss.

---

## 5. API and Backend Security Patterns

### Where Validation Happens

| Layer | What it validates |
|-------|------------------|
| `RequireModuleAttribute` (filter) | Company plan entitlement + user-level `CanUse` permission |
| `HmacSignatureMiddleware` (middleware) | Webhook HMAC-SHA256 signature |
| `FileSignatureValidator` | File magic bytes (MIME type validation, overrides client Content-Type header) |
| `ValidatePasswordComplexity()` | Password strength at registration |
| Controller actions | Model validation (`[Required]`, `ModelState.IsValid`) |
| `EntitlementResolver` | Seat limits, token limits, storage limits, KB limits |

### Where Authorization Happens

```
Every protected endpoint → [Authorize] (JWT validated by ASP.NET Core)
                         → Role policy ("SuperAdminOnly", "PlatformAdmin", or JWT role check)
                         → [RequireModule("module-key")] (entitlement + user permission)
                         → Controller action (business-level checks)
```

### Patterns Every Future Controller Must Follow

1. **`[Authorize]` on every non-public endpoint.** No exceptions.
2. **Tenant ID comes from `ITenantProvider`**, never from request parameters for scoped data.
3. **Admin endpoints:** Use `[Authorize(Policy = "SuperAdminOnly")]` for mutations, `[Authorize(Policy = "PlatformAdmin")]` for reads.
4. **Module-gated endpoints:** Use `[RequireModule("module-key")]` attribute.
5. **Rate limiting:** Use `[EnableRateLimiting("policy-name")]` on AI-consuming endpoints. Policies defined in `Program.cs`.
6. **Automation callbacks from n8n:** Must use `[RequireAutomationKey]` filter; comparison uses `CryptographicOperations.FixedTimeEquals`.
7. **Secrets returned in API responses:** Never return `WebhookSecret`, tokens, or keys in response DTOs. The invite token fix (F-05) is an example — tokens go via email only.

### HTTP Security Headers

Present on both API and frontend (F-32 fixed):
- API middleware (`Program.cs`): `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-XSS-Protection: 1; mode=block`, `Content-Security-Policy`, HSTS in non-dev.
- Frontend (`next.config.ts`): above + `Permissions-Policy` and route-wide `Content-Security-Policy`.

### Rate Limiting Policies (defined in `Program.cs`)

| Policy | Endpoint | Limit |
|--------|----------|-------|
| `login` | `POST /api/auth/login` | 5/min per IP (fixed window) |
| `register` | `POST /api/auth/register` | 10/hour per IP (fixed window) |
| `upload` | `POST /api/v1/knowledge/upload` | 10/min per tenant+IP |
| `ai-process` | `POST /api/inbox/process` | 30/min per tenant+IP |
| `ai-ingest` | `POST /api/agent/ingest` | 20/min per tenant+IP |
| `ai-search` | `GET /api/v1/knowledge` | 60/min per tenant+IP |

> AI rate limits use `{tenantId}:{ip}` as the partition key to limit per tenant while allowing multiple IPs.

---

## 6. Security-Sensitive Flows

### Organization / Membership Assignment

- Company membership stored in `UserCompanyMembership.CompanyRole`.
- Invite flow: `InviteController.SendInvite` → `AuthService.InviteUserAsync` → role ceiling enforced → token sent via email only (not in response body).
- Accept invite: `AuthController.AcceptInvite` validates token → creates membership → mints JWT.
- **Risk:** Claims in the current JWT are stale after a role change. Max staleness = 60 minutes (JWT lifetime).

### Admin Panel Access

- **Backend:** `[Authorize(Policy = "SuperAdminOnly")]` or `[Authorize(Policy = "PlatformAdmin")]` on all `/api/admin/*` routes.
- **Frontend:** Next.js middleware checks `req.auth?.user?.role` for `/admin` routes and redirects non-platform-admins to `/`. Runs server-side before any render (F-25 fixed).
- Admin layout (`app/(admin)/admin/layout.tsx`) also checks — this is defense-in-depth only.
- **Hangfire dashboard (`/hangfire`):** Protected by `HangfireDashboardAuthorizationFilter` requiring SuperAdmin JWT (F-22 fixed).

### Plan / Subscription Assignment

- Admin only (`SuperAdminOnly`).
- `CompanySubscriptionService.AssignPlanAsync` must use `.IgnoreQueryFilters()` when querying existing subscription (F-AdminQueryFilter pattern).
- Syncs `Tenant.Plan` string on every assignment.
- All plan changes write to `AuditTrail`.

### Webhook / n8n Integration

- Webhook endpoint (`/api/webhook/inbox`): protected by `HmacSignatureMiddleware` (HMAC-SHA256, constant-time compare).
- n8n callbacks: protected by `[RequireAutomationKey]` (constant-time compare, F-16 fixed).
- `AutomationKey` loaded from environment (`Automation:AutomationKey`) — not hardcoded.
- **n8n network segmentation (F-23 fixed):** n8n runs on the `external` Docker network only. It cannot reach PostgreSQL directly (`orvix-db` is `internal` only). Attack surface from n8n compromise is limited to the API's public webhook endpoints.

### File / Storage Access

- Files stored under `/app/uploads/{tenantId}/{documentId}/` — tenant-namespaced paths.
- GUID-named files on disk (no user-supplied filenames — F-12 fixed).
- Magic byte MIME validation via `FileSignatureValidator` (F-11 fixed).
- No direct HTTP serving of files from the uploads directory.
- **F-20 (MinIO) is deferred** — the current volume-mounted local storage is a placeholder. When MinIO is implemented, tenant-namespated bucket policies must be enforced.

### Background Jobs (Hangfire)

- Jobs run without a JWT. Tenant context is provided by `BackgroundTenantProvider` or `ITenantProviderFactory`.
- `InboxProcessingJob` — fetches tenant from `InboxEvent.TenantId`.
- `FileIngestionJob` — receives `tenantId` as a job parameter.
- `TrialExpirationJob` — uses `IgnoreQueryFilters()` to scan all companies; safe because there's no user context.
- **Risk:** If a job parameter for `tenantId` is tampered, tenant isolation breaks in the job context. Job enqueueing must always validate the tenantId comes from the current authenticated user's scope.

---

## 7. Current Known Security Gaps

As of 2026-04-15, all Critical and High findings from the security review are fixed. The remaining open items are:

| ID | Description | Severity | Status |
|----|-------------|----------|--------|
| **F-29** | Backend JWT accessible to client-side JS via `useSession()` | Medium | Open — server-side Route Handlers partially implemented (proxy routes added), but full migration not done |
| **F-30** | Frontend role checks rely on potentially stale token role | Low | Open — acceptable given 60-min JWT lifetime |
| **F-20** | Local file storage (volume mount) instead of MinIO/S3 | — | Deferred — separate implementation track |

### Gap: No JWT Revocation

**Current state:** JWTs are stateless with 60-min lifetime. There is no server-side blacklist. A revoked membership (e.g., user removed from company) is only effective after the current JWT expires.

**Impact:** Low-medium (max 60-min window). Acceptable for MVP. Future mitigation: `jti` blacklist in Redis or DB on sensitive events (password change, membership revocation, plan cancel).

### Gap: AuditTrail Impersonation (Partial)

**Current state:** Admin impersonation via `X-Impersonate-Tenant` is logged at `LogWarning` level. It does **not** yet write a record to the `AuditTrail` DB table.

**Impact:** If structured log sink is not configured, impersonation events may be lost. Future: write to `AuditTrail` to ensure DB-level traceability.

### Gap: Virus Scanning is Noop by Default

**Current state:** `IVirusScanService` defaults to `NoopVirusScanService`. Production environments will print a startup warning if Noop is active and environment is not Development (F-18 fixed).

**Impact:** Uploaded files are not scanned for malware unless ClamAV provider is configured.

### Gap: Content-Security-Policy Uses Compatibility Mode For Next.js

**Current state:** CSP is now enabled on both API and frontend responses. The frontend policy still allows `'unsafe-inline'` for scripts/styles to remain compatible with Next.js runtime output and existing inline style usage.

**Impact:** Stronger protection than no CSP, but not the strict nonce/hash-based policy desired for long-term hardening. Future tightening should remove `'unsafe-inline'` where feasible.

---

## 8. Guidance for Future Changes

### Before Touching Auth or Roles

1. Read `memory-risks.md` — the "Two Roles Classes" and "Global vs Company Roles" sections are critical.
2. Check `MintJwtAsync` in `AuthService.cs` — any new claim must be added here.
3. Check `TenantProvider.cs` — tenant resolution must always come from JWT claims.
4. Check `ScopeContext.cs` — reads the `Role` claim for permission logic.
5. Run `TenantProviderTests.cs` and `AuthControllerTests.cs` after any auth change.

### Before Adding a New Admin Endpoint

1. Apply `[Authorize(Policy = "SuperAdminOnly")]` for mutations, `[Authorize(Policy = "PlatformAdmin")]` for reads.
2. If querying tenant-scoped entities by `companyId`, add `.IgnoreQueryFilters()`.
3. Write to `AuditTrail` for destructive actions (cancel, suspend, plan change, entitlement override).
4. Never return `WebhookSecret` or any token in the response DTO.

### Before Adding a New AI Endpoint

1. Add a rate limiting policy in `Program.cs` using `{tenantId}:{ip}` as partition key.
2. Apply `[EnableRateLimiting("policy-name")]` to the controller action.
3. Check if the operation should also gate on entitlement limits (`IsWithinTokenLimitAsync`, `IsWithinApiLimitAsync`).

### Before Adding a New File Upload Endpoint

1. Use `FileSignatureValidator` for magic byte validation — do not trust `IFormFile.ContentType`.
2. Use GUID-based filenames generated by `LocalFileStorage` — never use client-supplied filenames.
3. Store files under `{tenantId}/` namespaced paths.
4. Apply file size limits (currently 10MB) and rate limiting.

### Before Adding a New Webhook / Integration

1. HMAC-SHA256 validation using `HmacSignatureMiddleware` pattern (or add the endpoint to the middleware path list).
2. Constant-time comparison via `CryptographicOperations.FixedTimeEquals` — never use `==` or `.Equals()` for secret comparison.
3. Resolve tenant from validated signature payload, not from a plain header.

### When Changing Tenant Isolation Logic

1. Run `TenantIsolationTests.cs` immediately before and after.
2. Verify the EF Core query filter in `AppDbContext` is still active for all affected entities.
3. Any new entity that stores per-tenant data must have a query filter added to `AppDbContext.OnModelCreating`.
4. Admin queries that bypass filters must explicitly enforce admin authorization before returning results.

### Anti-Patterns to Avoid

| Anti-pattern | Why it's dangerous |
|-------------|-------------------|
| Resolving tenant from `X-Tenant-ID` header in `TenantProvider` | Was the critical F-09 bug — full cross-tenant access bypass |
| Silently merging OAuth identity with existing email | Was the F-02 account takeover vector |
| Returning tokens/secrets in API responses | Invite token (F-05), WebhookSecret (F-19) — both fixed, don't regress |
| Using `string.Equals()` for secret comparison | Timing side-channel — always use `CryptographicOperations.FixedTimeEquals` |
| Setting `User.Role` to a company role value | Breaks the two-layer role model, causes authorization failures |
| Calling async code with `.GetAwaiter().GetResult()` in auth filters | Thread starvation under load — was F-06, which is why RequireModuleAttribute uses `IAsyncAuthorizationFilter` |
| Adding new admin entities without `.IgnoreQueryFilters()` | Silent data loss — admin sees only their own company's data |
| Committing secrets to any tracked file | F-15, F-31 — rotate immediately if discovered |

---

## 9. Security Implementation Tips for Agents

### How to Investigate Before Editing

1. **For auth changes:** Check `AuthService.cs` (MintJwtAsync, RegisterAsync, LoginAsync), `TenantProvider.cs`, `ScopeContext.cs`.
2. **For authorization changes:** Check `RequireModuleAttribute.cs`, `AccessResolver.cs`, `Roles.cs` (both files).
3. **For tenant isolation changes:** Check `AppDbContext.OnModelCreating`, `TenantProvider.cs`, `BackgroundTenantProvider.cs`.
4. **For admin panel changes:** Check `AdminController.cs` + `Program.cs` (policy registrations) + `middleware.ts` (frontend guard).
5. **For n8n/webhook changes:** Check `HmacSignatureMiddleware.cs`, `RequireAutomationKeyAttribute.cs`, `docker-compose.yml` networks.

### High-Impact Files (Touch With Extra Care)

| File | Why it's high-impact |
|------|---------------------|
| `OrvixFlow.Infrastructure/Auth/AuthService.cs` | All auth flows — login, register, token minting, OAuth, invites |
| `OrvixFlow.Api/Services/TenantProvider.cs` | Tenant resolution — if broken, cross-tenant data access is trivial |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | All query filters — if broken, all tenant isolation collapses |
| `OrvixFlow.Core/Authorization/Roles.cs` | Role enum and all extension methods used across the system |
| `OrvixFlow.Api/Filters/RequireModuleAttribute.cs` | Module access gating — if broken, all premium features exposed |
| `OrvixFlow.Api/Program.cs` | Auth middleware order, rate limit policies, admin policies, HSTS |
| `orvixflow-web/middleware.ts` | Frontend admin route guard — must check server-side before rendering |
| `orvixflow-web/auth.ts` | NextAuth config — JWT session strategy and token population |

### Always Regression-Test After Security-Touching Changes

```bash
dotnet test                                          # All 314 tests must pass
dotnet test --filter "FullyQualifiedName~TenantIsolation"
dotnet test --filter "FullyQualifiedName~AuthController"
dotnet test --filter "FullyQualifiedName~AccessResolver"
dotnet test --filter "FullyQualifiedName~HmacSignature"
dotnet test --filter "FullyQualifiedName~RequireModule"
```

### Assumptions That Must Never Be Made

1. **"The frontend role check is enough."** — It is not. The API must enforce role/policy independently.
2. **"The tenant comes from the request header."** — It must come from the JWT claim. Headers are attacker-controlled.
3. **"This admin query will just return the right company's data."** — EF query filters return the admin's own company. Always use `.IgnoreQueryFilters()` for cross-company admin lookups.
4. **"OAuth email matching means it's the same user."** — Email is not a proof of identity across providers. Explicit linking with user consent is required.
5. **"Secrets in dev configs are fine since it's local."** — Development configs are git-tracked. Rotate any real key that appears there.
6. **"I can use `.Equals()` for constant-time comparison."** — Use `CryptographicOperations.FixedTimeEquals` for any secret comparison.
7. **"The tenant isolation tests still pass, so isolation is fine."** — Also manually verify `.IgnoreQueryFilters()` is not accidentally on a non-admin code path.
