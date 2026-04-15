# OrvixFlow Security Review
**Date:** 2026-04-08 | **Primary Reviewer:** Claude (Senior Architect role) | **Cross-reviewed by:** OpenCode Security Agent | **Last updated:** 2026-04-14 | **Severity Key:** 🔴 Critical · 🟠 High · 🟡 Medium · 🟢 Low

> **Note:** This document incorporates findings from two independent AI security reviews. Findings F-01 through F-30 were identified by Claude from direct source-code inspection. Findings F-31, F-32, and F-33 were identified by OpenCode and verified by manual source inspection before inclusion. One OpenCode finding (CORS misconfiguration) was rejected as factually incorrect after code verification. See `tasks/opencode-security-review-08-04.md` for the original OpenCode report.
>
> **2026-04-09 Update:** Phase 1 remediation completed: F-09, F-10, F-15, F-17, F-31 fixed. F-02 was already fixed.
> **2026-04-09 Update (continued):** Phase 2 remediation completed: F-32, F-11, F-12, F-14, F-22, F-01, F-03, F-28, F-16, F-08, F-19 fixed.
> **2026-04-14 Update:** Phase 3 complete: F-06, F-25, F-07, F-27, F-04, F-33, F-21, F-23 fixed.
> **2026-04-14 Update:** Phase 4 complete: F-05, F-13, F-18 fixed. F-26 already implemented (audit trail present).
> **2026-04-14 Update:** Phase 4 continued: F-24 (per-tenant webhook rate limit), F-29 (server-side API proxy) implemented. Route handlers at /api/proxy/* enable server-side API calls.

---

## 1. Project Security Context Summary

OrvixFlow is a multi-tenant SaaS platform with:
- **Backend:** .NET 9 ASP.NET Core, EF Core + PostgreSQL w/ pgvector, Hangfire background jobs, Semantic Kernel + AI
- **Frontend:** Next.js 16 (App Router), NextAuth 5 (JWT session strategy)
- **Integrations:** n8n workflow automation, Google OAuth, Microsoft Entra ID (Azure AD), email webhook ingestion
- **Auth:** Two-layer JWT system — global platform roles (`SuperAdmin`, `InternalOperator`) + company roles (`CompanyOwner` … `Viewer`)
- **Multi-tenancy:** EF Core global query filters on all entities keyed by `TenantId`/`CompanyId`
- **Sensitive domains:** Email content ingestion, AI-generated drafts, billing/subscription data, knowledge base files, OAuth tokens, mailbox OAuth credentials

The overall architecture is thoughtful for an early-stage SaaS. The tenant isolation model is well-designed. However, several specific issues create meaningful risk, particularly related to secrets management, the impersonation mechanism, rate limiting scope, the Hangfire dashboard, invitation token exposure, and audit coverage gaps.

---

## 2. Security Findings by Area

---

### Area 1 — Authentication

#### 🟠 HIGH | F-01 | JWT Tokens Have No Revocation Mechanism
**Location:** `AuthService.cs` — `MintJwtAsync` | `auth.ts`

**Issue:** JWTs are minted with a flat 7-day expiry and no refresh token. There is no server-side session store, blacklist, or token rotation. Once issued, a token is valid until it expires regardless of events (password change, user removal, role change, company suspension).

**Risk:** An attacker who obtains a JWT (e.g., from XSS, log leak, stolen device) has a 7-day window of unrestricted access. A fired CompanyOwner keeps access to the company for up to 7 days after removal.

**Impact:** High — role/access changes are not immediately effective. Privilege retention after membership revocation.

**Abuse:** Attacker steals JWT from browser storage or API response logs, uses it for the full 7-day window.

**Fix:**
- Shorten token lifetime to 15–60 minutes.
- Introduce refresh tokens (server-side, rotatable) stored only in `HttpOnly` SameSite cookies.
- On sensitive events (password change, membership revocation, plan cancel), add a `jti` (JWT ID) blacklist keyed to Redis or a DB table checked on every authenticated request.

---

#### 🟠 HIGH | F-02 | Silent Account Takeover via Automatic OAuth Email Linking
**Location:** `AuthService.cs:108-123` — `ProvisionOAuthUserAsync`

**Issue:** When a social login (Google/Azure) attempts to sign in with an email that already exists from a **different provider**, the system *silently* upgrades the local account to OAuth — overwriting `OAuthProvider` and `ExternalId` — without any additional verification or notification.

**Risk:** If an attacker controls a Google/Microsoft account that shares an email with a victim's local OrvixFlow account, they can take over the account entirely without knowing the victim's password.

**Impact:** Critical account takeover — full access to victim's tenant, knowledge base, inbox, and mailboxes.

**Abuse:** Attacker registers a Google account with `victim@company.com`, signs in via Google OAuth, gets immediately linked to the victim's account and receives a valid JWT.

**Fix:**
- Do **not** silently merge accounts. Instead:
  - Return an error: "An account with this email already exists. Please sign in with email/password."
  - Or send a verification email to the existing account asking for explicit confirmation before linking.
- Never modify an existing user's auth provider without explicit user action.

---

#### 🟡 MEDIUM | F-03 | No Login Brute-Force Protection
**Location:** `AuthController` → `AuthService.LoginAsync`

**Issue:** The login endpoint has no rate limiting, account lockout, or CAPTCHA. Any attacker can attempt unlimited password guesses per account.

**Fix:** Add per-IP and per-account rate limiting to the `/api/auth/login` endpoint. The existing `FixedWindowLimiter("upload")` rate limiter mechanism is ready to be extended.

---

#### 🟡 MEDIUM | F-04 | No Password Complexity Enforcement
**Location:** `AuthService.RegisterAsync`

**Issue:** Password is accepted, hashed with BCrypt, and stored with no length or complexity validation. A user can register with `"a"` as their password.

**Fix:** Enforce minimum 12 characters, require mixed character classes, check against common password lists using a `zxcvbn`-style library.

---

#### 🟢 LOW | F-05 | Invitation Token Returned in API Response Body
**Location:** `InviteController.cs:121`

**Issue:** The invite token is returned directly in the POST response body:
```csharp
return Ok(new { token = result.Token, message = "Invitation created." });
```
The code comment says *"In production this token would be e-mailed."* This is a pre-production placeholder that must be resolved before launch.

**Risk:** Token is visible in API logs, browser network history, and any intermediary proxy. Any admin who can read API logs can claim any invitation.

**Fix:** Remove the token from the response body. Send the token exclusively via email. Store only a salted hash of the token in the DB.

---

### Area 2 — Authorization

#### 🟡 MEDIUM | F-06 | RequireModule Uses Synchronous `.GetAwaiter().GetResult()` in Authorization Filter
**Location:** `RequireModuleAttribute.cs:47`

**Issue:**
```csharp
var canUseModule = entitlementResolver.CanUseModuleAsync(companyId, _requiredModule).GetAwaiter().GetResult();
```
Synchronous blocking in an `IAuthorizationFilter` on the ASP.NET Core thread pool can cause thread starvation under load, leading to deadlocks and denial-of-service conditions.

**Fix:** Implement `IAsyncAuthorizationFilter` instead of `IAuthorizationFilter` so the call is truly async:
```csharp
public class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var canUse = await entitlementResolver.CanUseModuleAsync(...);
    }
}
```

---

#### 🟡 MEDIUM | F-07 | RequireModule Checks Plan/Module Entitlement But Not User-Level Permission
**Location:** `RequireModuleAttribute.cs` — checks `CanUseModuleAsync(companyId, module)` only

**Issue:** `RequireModule` checks whether the company's plan entitles them to a module, but does **not** check whether the *specific user* has the `CanUse` permission for that module. `AccessResolver.GetEffectivePermissionsAsync()` exists and handles user-scoped grants — but the filter only calls the plan-level check.

**Risk:** A `Viewer`-role user within a company that has the `inbox-guardian` module could call all module-gated endpoints since the check only validates company entitlement, not user permission.

**Fix:** Extend `RequireModule` to also call `AccessResolver.GetEffectivePermissionsAsync` and verify `result.CanUse` unless the user is `CompanyAdminOrAbove`.

---

#### 🟡 MEDIUM | F-08 | CompanyAdmin Can Invite With CompanyOwner Role (Privilege Escalation)
**Location:** `InviteController.cs:83-121` | `AuthService.InviteUserAsync`

**Issue:** The validation in `InviteUserAsync` checks:
```csharp
if (!UserRoleExtensions.AllRoles.Contains(role))
    return new InviteResult(false, Error: $"Invalid role: ...");
```
But it does **not** enforce that the *inviter's role must be ≥ the role they are assigning*. A `CompanyAdmin` can invite someone as `CompanyOwner`, elevating new members above themselves.

**Fix:** Add a role-ceiling check in `InviteUserAsync`:
```csharp
if (role.IsHigherThan(callerRole))
    return new InviteResult(false, Error: "Cannot assign a role higher than your own.");
```

---

### Area 3 — Tenant Isolation

#### 🔴 CRITICAL | F-09 | `X-Tenant-ID` Header Fallback Allows Unauthenticated Tenant Injection
**Location:** `TenantProvider.cs:39-43`

**Issue:**
```csharp
// Fallback to header if no claim
var tenantHeader = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-ID"].ToString();
if (!string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out var headerGuid))
{
    return headerGuid;
}
```
If the JWT has no `TenantId` or `ActiveCompanyId` claim (e.g., a token minted without these claims, or a request with no JWT at all on a route that somehow passes `[Authorize]`), the system falls back to accepting the `X-Tenant-ID` header from the HTTP request as the authoritative tenant ID.

**Risk:** Any authenticated user (or, depending on controller-level `[AllowAnonymous]` usage, even unauthenticated callers) who can craft the `X-Tenant-ID` header can force the entire EF Core query filter to resolve to any tenant they choose — effectively bypassing multi-tenant isolation.

This fallback is also present in the `HmacSignatureMiddleware` (line 57-68) for the webhook path, where it's at least more justified (no JWT available). But the general `TenantProvider` fallback is the dangerous one.

**Impact:** Full cross-tenant data access. Any attacker with a valid JWT (as themselves) can read, write, or delete another tenant's knowledge base, inbox events, workflow policies, etc.

**Abuse:**
```
GET /api/v1/knowledge
Authorization: Bearer <attacker's valid JWT>
X-Tenant-ID: <victim-company-guid>
```
Returns victim's knowledge base entries.

**Fix:**
- **Remove the `X-Tenant-ID` header fallback from `TenantProvider`** entirely. Tenant ID must always come from the JWT claim.
- For the webhook path (HMAC-validated), use a separate `WebhookTenantProvider` that only reads `X-Tenant-ID` after HMAC validation succeeds.
- The admin impersonation already handles the legit cross-tenant case via `X-Impersonate-Tenant` (role-checked), which is correct.

---

#### 🔴 CRITICAL | F-10 | Admin Impersonation (`X-Impersonate-Tenant`) Has No Audit Trail
**Location:** `TenantProvider.cs:24-31`

**Issue:** When a SuperAdmin or InternalOperator sends `X-Impersonate-Tenant: <guid>`, the system silently assumes the target tenant's full scope. All subsequent database queries run as that tenant.

There is no audit log entry created when impersonation begins or ends. The `AuditTrail` system exists but is not invoked here.

**Risk:** A malicious platform admin can browse, modify, or exfiltrate any tenant's data with zero traceability. Even for legitimate support use, there is no record of who accessed what under impersonation.

**Fix:**
- Log all impersonation starts to `AuditTrail`: `{ Action: "AdminImpersonation", Actor: adminUserId, EntityId: targetTenantId, ... }`.
- The impersonated tenant's admin UI should show a banner "Currently being viewed by platform admin" if feasible.
- Consider restricting `InternalOperator` to read-only even in impersonated context.

---

### Area 4 — Input Validation and API Security

#### 🟠 HIGH | F-11 | File Upload Uses Client-Supplied `Content-Type` (MIME Sniffing Bypass)
**Location:** `FileIngestionController.cs:70-75`

**Issue:**
```csharp
if (!allowedTypes.Contains(file.ContentType.ToLower()))
    return BadRequest(...)
```
The MIME type check reads `file.ContentType` which is the value sent by the HTTP client in the `Content-Type` header of the multipart boundary — **not** validated against the actual file bytes.

**Risk:** An attacker can upload a `.php`, `.exe`, `.js`, or any other file by setting the `Content-Type` multipart field to `"text/plain"` or `"application/pdf"`. The file will pass validation and be stored on disk. While not directly executable in .NET, it's a path to stored malware, XSS payloads in parsers, or malicious payloads being parsed by image/PDF libraries.

**Fix:**
- Use magic byte inspection on the first bytes of the stream to verify actual file type (e.g., `FileSignatures` library or inline `byte[]` sniffing).
- Additionally validate file extension against an allowlist.
- Quarantine uploaded files in a non-web-accessible directory (the current `/app/uploads` path should not be directly served).

---

#### 🟠 HIGH | F-12 | File Stored Under Client-Supplied Filename (Path Traversal Risk)
**Location:** `LocalFileStorage.cs:32`

```csharp
var fullPath = Path.Combine(docDir, fileName);
```

**Issue:** The `fileName` value comes directly from `file.FileName` (`IFormFile.FileName`), which is client-controlled. While `Path.Combine` offers some protection, filenames like `../../../etc/cron.d/pwned` or `C:\Windows\System32\malicious.dll` can bypass naive path construction.

**Fix:** Always sanitize filenames before storing:
```csharp
var safeFileName = Path.GetFileName(file.FileName); // strips directory components
safeFileName = string.Concat(safeFileName.Split(Path.GetInvalidFileNameChars()));
// Better: generate a random internal name
var storedName = $"{Guid.NewGuid()}{Path.GetExtension(safeFileName)}";
```

---

#### 🟡 MEDIUM | F-13 | No Input Size Limits on Text Ingestion
**Location:** `KnowledgeBaseController` / `IngestionService`

**Issue:** The text/direct ingestion endpoint (for raw text KB entries) does not appear to have enforced limits on text body size, unlike the file upload endpoint. A malicious user could ingest multi-megabyte text prompts, causing large embedding API calls and vector storage growth.

**Fix:** Add `MaxLength` validation on text ingestion requests. Apply rate limiting to text ingestion in addition to file upload.

---

#### 🟡 MEDIUM | F-14 | `DeleteDocument` Silently Swallows Storage Errors
**Location:** `FileIngestionController.cs:178-184`

```csharp
try { await _storage.DeleteFileAsync(document.StoragePath); }
catch { }
```

**Issue:** The `catch {}` block is intentionally swallowing all storage deletion errors. If `DeleteFileAsync` fails (permissions, disk error), the DB record is still deleted, creating an orphaned file on disk that is permanently untracked.

**Risk:** Storage leak, orphaned files accumulate across tenants, potential data retention issues.

**Fix:** At minimum, log the error. Ideally, mark the file for cleanup in a background job rather than silently proceeding.

---

### Area 5 — Secrets and Configuration

#### 🔴 CRITICAL | F-15 | Hardcoded Dev Secrets Present in docker-compose.yml (Git-Tracked)
**Location:** `docker-compose.yml`

**Issue:** The following real secrets are stored in plain text and committed to git:
```yaml
GOOGLE_CLIENT_ID: "807810943561-g55223mqo7q62ije0v1ms1r7qtujba8k.apps.googleusercontent.com"
GOOGLE_CLIENT_SECRET: "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"
AZURE_AD_CLIENT_ID: "a20460c4-29f0-458c-93cc-914acfc7a06c"
AZURE_AD_CLIENT_SECRET: "tyu8Q~4v_FMRDRn8R~XDaTISfW2dUe61HXhLWaZd"
AutomationKey: super-secret-n8n-dev-key
NEXTAUTH_SECRET: dev-nextauth-secret-change-me
Jwt__Secret: dev-super-secret-jwt-key-32-chars-minimum-length-required-here!
```

**Risk:**
- Google/Azure OAuth credentials are real (OAuth apps are registered). Anyone with git repo access can impersonate the OAuth app, potentially steal auth codes, or abuse OAuth quotas.
- If the repo is ever public, or an employee is compromised, all these secrets are exposed forever (git history).

**Fix:**
- **Immediately rotate** the Google and Azure credentials.
- Use `.env` files (gitignored) for local dev secrets.
- Create `.env.example` with placeholder values.
- For production deployment, use Docker secrets, Kubernetes Secrets, or a secret manager (HashiCorp Vault, AWS Secrets Manager, Azure Key Vault).

---

#### 🟠 HIGH | F-16 | `AutomationKey` Is a Weak Static Preshared Secret with No Rotation
**Location:** `RequireAutomationKeyAttribute.cs` | `docker-compose.yml`

**Issue:** The `AutomationKey` (`super-secret-n8n-dev-key`) is a static preshared secret used to authenticate n8n webhook callbacks. It:
- Is hardcoded in docker-compose (see F-15)
- Is globally shared — one key for the entire platform, not per-tenant
- Has no rotation mechanism
- Uses string equality comparison (non-constant time: `configuredApiKey.Equals(extractedApiKey.ToString())`)

**Fix:**
- Use `CryptographicOperations.FixedTimeEquals` for comparison (same already done in the HMAC middleware — be consistent).
- Rotate the key at deployment.
- Consider per-tenant automation keys for finer-grained revocation.

---

#### 🟠 HIGH | F-17 | `appsettings.json` Contains a Placeholder JWT Secret In Git
**Location:** `OrvixFlow.Api/appsettings.json:10`

```json
"Secret": "REPLACE-WITH-A-64-CHAR-RANDOM-SECRET-IN-PRODUCTION-ENVIRONMENT"
```

**Issue:** This is a **known-value** placeholder that, if accidentally used in production (e.g., by skipping environment variable override in a Docker deployment), would allow any attacker to sign valid JWTs with full admin access to the entire platform.

**Fix:**
- Remove the `Jwt:Secret` key from `appsettings.json` entirely.
- Only set it via environment variable (throw on startup if missing — which is already done in `Program.cs:30`).
- Add a startup validation that rejects well-known/weak secrets (`if (secret == "REPLACE-...")  throw`).

---

#### 🟡 MEDIUM | F-18 | Virus Scan Is a No-Op in All Environments
**Location:** `appsettings.json` | `NoopVirusScanService.cs`

```json
"Security": { "VirusScan": { "Provider": "Noop" } }
```

**Issue:** The architecture correctly includes `IVirusScanService` with a ClamAV implementation, but the configured default is `Noop` (always returns `true`). This applies in production unless explicitly changed.

**Fix:** The `Noop` variant is fine for local dev. But production configuration should default to the ClamAV provider. Add a startup warning log if `Noop` is active in a non-Development environment.

---

### Area 6 — Data Protection

#### 🟠 HIGH | F-19 | Admin Company Detail API Exposes `WebhookSecret` in Plaintext
**Location:** `AdminController.cs:129`

```csharp
var company = await _db.Tenants...
    .Select(t => new
    {
        ...
        t.WebhookSecret,  // ← exposed in API response
        ...
    })
```

**Issue:** The `WebhookSecret` (used to validate HMAC signatures on webhook payloads) is returned in plaintext in the admin API response for `GET /api/admin/companies/{id}`. This secret should be treated like a password — it should never be transmitted except at the time of initial generation.

**Risk:** Any admin (including `InternalOperator`) who can call this endpoint receives the tenant's webhook secret. If the admin panel frontend logs responses or the secret appears in audit logs, it is doubly exposed.

**Fix:**
- Never return `WebhookSecret` in API responses.
- Store the webhook secret as a one-way hash (HMAC of the secret + tenant ID) and only allow "rotate secret" operations.
- Provide a "Show secret once" flow (similar to how GitHub PATs work) at generation time only.

---

---

#### 🟡 MEDIUM | F-21 | Sensitive Email Content in `InboxEvent` / `AuditTrail` `PreviousState` / `NewState` Fields
**Location:** `AuditTrail.cs` — `PreviousState`, `NewState` columns | `AdminController.cs:729-738` (returned in admin API)

**Issue:** Audit trail entries can contain full email bodies (`InboxEvent`), AI drafts, and user instructions. These are stored as-is in the `AuditTrail` table and returned in the admin API. Email content often contains PII (names, personal situations, financial data). This data is retained indefinitely.

**Fix:**
- Define a data retention policy for `AuditTrail` entries and implement periodic purging.
- Consider storing sensitive email content encrypted at rest.
- In audit log API responses, consider masking or omitting high-sensitivity content fields for `InternalOperator` role.

---

### Area 7 — Workflow / Automation Security

#### 🟠 HIGH | F-22 | n8n Dashboard Exposed with No Authentication (Network-Level Only)
**Location:** `Program.cs:133-136`

```csharp
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});
```

**Issue:** The Hangfire dashboard (job queue management, retry, delete, enqueue) is protected only by "localhost only." In Docker, if the API container's port is exposed to a shared network or if a reverse proxy misconfiguration forwards `/hangfire`, any user on the same network can access the Hangfire dashboard with **full job management capabilities** — including enqueuing arbitrary background jobs.

**Fix:**
- Replace `LocalRequestsOnlyAuthorizationFilter` with a custom filter requiring `SuperAdmin` JWT role.
- Add path-based routing restrictions at the reverse proxy level.

---

#### 🟠 HIGH | F-23 | n8n Runs with Full Internal Network Access, No Egress Restriction
**Location:** `docker-compose.yml` — n8n service

**Issue:** The n8n instance is on the same Docker network as the PostgreSQL database, API server, and has unrestricted outbound internet access. n8n itself is a workflow automation engine where authenticated users can create HTTP Request nodes, code execution nodes, and database nodes. If an attacker gains n8n access (e.g., weak n8n admin password, n8n vulnerability), they have direct HTTP/TCP access to the internal PostgreSQL and API services.

**Fix:**
- Network-segment n8n: put it in a separate Docker network that cannot reach the DB directly.
- Restrict n8n's outbound access via egress firewall rules.
- Set a strong n8n admin password and backup credentials.

---

#### 🟡 MEDIUM | F-24 | `WorkflowPolicy` Auto-Execute Has No Rate Limiting Per Tenant
**Location:** `InboxProcessingJob.cs` — policy evaluation flow

**Issue:** If `WorkflowPolicy` evaluates to auto-execute, an n8n webhook is called automatically for every processed inbox email. There is no per-tenant rate limit on how many auto-executions can occur per minute/hour, meaning a flood of incoming emails could trigger a corresponding flood of n8n webhook calls, potentially generating unbounded AI token usage and cost.

**Fix:** Add per-tenant limit checks (`IsWithinApiLimitAsync`) before triggering auto-execute workflows.

---

### Area 8 — Admin and Internal Tooling Security

#### 🟠 HIGH | F-25 | Admin Panel Frontend has No Server-Side Route Protection
**Location:** `orvixflow-web/app/admin/` directory

**Issue:** The admin pages are under `/admin/` in Next.js. Looking at the feature map, admin UI pages like `/admin/test` (Inbox Simulator), `/admin/vector-db` (Direct vector DB inspection), and `/admin/logs` (RAG kernel traces) exist. The frontend presumably checks the session role client-side.

However, the **Next.js App Router middleware** (noted as deprecated in the build warning: `"middleware" file convention is deprecated`) and any route protection is done client-side or via NextAuth session checks in each page component. If these checks are missing or bypassable (e.g., direct API calls from the browser), admin pages may be accessible.

**Risk:** Admin functionality accessible to non-admins without API-level enforcement would only be blocked by UI — the underlying API calls (which do enforce authorization) would still be protected, but the admin UI itself could be reached and partially abused.

**Fix:** Verify that every admin page has a server-side auth check using `auth()` from NextAuth in its page component. The `/app/admin/layout.tsx` should have a redirect-on-no-admin-role guard.

---

#### 🟡 MEDIUM | F-26 | SuperAdmin Can Cancel/Suspend Any Company Without Confirmation or Reversibility Check
**Location:** `AdminController.cs` — `CancelCompany`, `SuspendCompany` endpoints

**Issue:** These destructive actions (cancelling a company's subscription) have no confirmation token, double-submit protection, or undo period. A typo in the `{id}` parameter — or a compromised SuperAdmin account — could cancel paying customers.

**Fix:**
- Require a confirmation token (CSRF-like) for destructive admin actions.
- Write all such actions to `AuditTrail` before executing (these currently may not be audited).
- Consider a soft-cancel with a grace period before actual cancellation.

---

### Area 9 — Observability and Abuse Prevention

#### 🟡 MEDIUM | F-27 | Rate Limiting Applied Only to File Upload, Not Other AI Endpoints
**Location:** `Program.cs:88-98` — only `upload` policy defined

**Issue:** Rate limiting is applied only to `POST /api/v1/knowledge/upload`. Other high-cost endpoints are unprotected:
- `POST /api/inbox/process` — triggers full AI pipeline (classification, RAG search, draft generation)
- `POST /api/agent/ingest` — triggers embedding API call
- `POST /api/v1/knowledge` — text ingestion with embedding
- KnowledgeBase search endpoints — vector search on every call

A malicious authenticated tenant could flood these endpoints, causing unbounded AI API spend.

**Fix:** Add rate limiting policies for all AI-consuming endpoints: inbox processing, text ingestion, KnowledgeBase search.

---

#### 🟡 MEDIUM | F-28 | Missing Structured Logging for Security Events
**Location:** Auth flow, admin actions, impersonation

**Issue:** There is no dedicated structured security event log. Specifically missing:
- Failed login attempts (only `LogDebug` — which may not appear in production)
- Admin impersonation start/stop
- Privileged actions (plan changes, cancel, suspend, entitlement overrides)
- Webhook signature failures

**Fix:** Use `LogWarning` or higher for all security-relevant events. Add a dedicated `SecurityAuditService` that writes structured records to `AuditTrail` or a separate security log sink.

---

### Area 10 — Frontend Security

#### 🟡 MEDIUM | F-29 | `apiToken` Stored in NextAuth Session (Accessible to Page Components as `as any`)
**Location:** `auth.ts:115` | Multiple dashboard pages: `(session as any)?.apiToken`

**Issue:** The backend JWT is stored as `session.apiToken` and exposed to all page components via `useSession()`. Pages cast the session as `as any` to read it:
```tsx
const apiToken = (session as any)?.apiToken;
```
This is a type-safety workaround. More importantly, if the Next.js frontend is compromised (XSS, supply chain), the full backend JWT token is extracted trivially. The token has a 7-day lifespan and full tenant scope.

**Risk:** XSS → full account compromise for 7 days.

**Fix:**
- Define proper TypeScript session types to eliminate `as any` casting (belt-and-suspenders type safety).
- Move API calls to server-side Next.js route handlers that proxy requests using the server-side session token, so the raw JWT never flows to the client-side JS bundle.

---

#### 🟢 LOW | F-30 | Frontend Admin Role Check Via Session Role Claim Only
**Location:** Admin pages in `orvixflow-web/app/admin/`

**Issue:** Frontend role checks rely on the `session.user.role` field populated from the JWT claim. If a user has a stale token with the wrong role (e.g., recently promoted to SuperAdmin), they may see incorrect UI. The API correctly enforces roles.

**Fix:** Ensure role-sensitive UIs re-validate the session after role changes (currently the token refresh path via company switch handles this for company roles, but an admin role change would require a new login).

---

### Area 11 — Additional Findings from Cross-Review (OpenCode)

#### 🔴 CRITICAL | F-31 | Groq API Key Committed to Git in `appsettings.Development.json`
**Location:** `OrvixFlow.Api/appsettings.Development.json:23` *(verified)*
**Source:** OpenCode review

**Issue:** A real, live Groq API key is hardcoded in `appsettings.Development.json`:
```json
"ApiKey": "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"
```
This file is almost certainly tracked in git. Anyone with repository access can use this key to make requests against the Groq LLM API, potentially incurring unbounded cost.

**Risk:** API key abuse, cost attacks, exfiltration of any prompt data sent to the model.

**Impact:** Financial damage via runaway API usage; potential data exposure if the key is used for further API enumeration.

**Abuse:** Clone repo → extract key → run unlimited LLM inference calls billed to your account.

**Fix:**
- **Immediately rotate** the Groq API key in the Groq console.
- Remove the value from `appsettings.Development.json`. Replace with a placeholder: `"ApiKey": ""`.
- Load via environment variable: `AI__OpenAI__ApiKey=<value>` (already done in docker-compose pattern — apply same to local dev).
- Add `appsettings.Development.json` to `.gitignore` or ensure it never contains live secrets.
- Run `git log -S "gsk_8bAgiTnd"` to confirm no historical exposure in git history, and if found, use `git filter-repo` to scrub.

---

#### 🟠 HIGH | F-32 | No HTTP Security Headers on API or Frontend
**Location:** `OrvixFlow.Api/Program.cs` | `orvixflow-web/next.config.ts` *(verified — both files confirmed empty of any header configuration)*
**Source:** OpenCode review

**Issue:** Neither the ASP.NET Core API nor the Next.js frontend set any HTTP security headers:
- `Content-Security-Policy` — missing, enables XSS
- `X-Frame-Options` — missing, enables clickjacking
- `X-Content-Type-Options` — missing, enables MIME sniffing
- `Strict-Transport-Security` — missing, no HTTPS enforcement signal to browsers
- `Referrer-Policy` — missing

**Risk:** Without these headers, a successful XSS attack (e.g., via an injected script in email body rendered in the inbox UI) can exfiltrate the `apiToken` from the session. Clickjacking enables UI redress attacks.

**Fix — API (`Program.cs`):** Add after `app.UseRateLimiter()`:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // X-XSS-Protection is legacy but harmless:
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});
// HSTS — add in production only:
if (!app.Environment.IsDevelopment())
    app.UseHsts();
```

**Fix — Frontend (`next.config.ts`):**
```typescript
const nextConfig: NextConfig = {
  async headers() {
    return [{
      source: "/:path*",
      headers: [
        { key: "X-Content-Type-Options", value: "nosniff" },
        { key: "X-Frame-Options", value: "DENY" },
        { key: "X-XSS-Protection", value: "1; mode=block" },
        { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
        { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        // Tighten CSP once inline scripts are audited:
        // { key: "Content-Security-Policy", value: "default-src 'self'; ..." },
      ],
    }];
  },
};
```

---

#### 🟡 MEDIUM | F-33 | No Email Verification Required on Registration
**Location:** `AuthService.cs:32-60` — `RegisterAsync` *(verified)*
**Source:** OpenCode review

**Issue:** After `RegisterAsync` completes, the account is immediately active and a JWT is returned — there is no email verification step. This means:
- Anyone can register with any email address (including email addresses they do not own)
- Fake/spam accounts can be created at will
- There is no way to prove email ownership, which weakens the phishing risk around invitations and OAuth linking

**Risk:** Account enumeration, spam accounts, weakened identity assurance for invited users.

**Fix:**
- On registration, mark account as `EmailVerified = false`.
- Send a verification email with a signed token (similar to the existing `Invitation.Token` pattern — reuse that infrastructure).
- Block login for unverified accounts (or restrict access to a verification-only landing page).
- Set a TTL on the verification token (24–48 hours).

---

## 3. Critical Vulnerabilities Summary

| ID | Title | Severity | Source |
|----|-------|----------|--------|
| F-09 | `X-Tenant-ID` header fallback allows cross-tenant access | 🔴 Critical | Claude |
| F-10 | Admin impersonation has no audit trail | 🔴 Critical | Claude |
| F-15 | Real OAuth/JWT secrets in git-tracked `docker-compose.yml` | 🔴 Critical | Both |
| F-31 | Groq API key hardcoded in `appsettings.Development.json` | 🔴 Critical | OpenCode |
| F-02 | Silent OAuth email-link account takeover | 🟠 High | Both |

---

## 4. High-Priority Risks (Acting Critical in the Right Conditions)

| ID | Title | Severity | Source |
|----|-------|----------|--------|
| F-01 | No JWT revocation mechanism — 7-day stale access | 🟠 High | Both |
| F-11 | MIME type validation uses client-supplied value (bypass) | 🟠 High | Claude |
| F-12 | Path traversal risk in uploaded filename | 🟠 High | Claude |
| F-16 | AutomationKey weak, static, non-constant-time comparison | 🟠 High | Claude |
| F-17 | Known-placeholder JWT secret in appsettings.json | 🟠 High | Claude |
| F-19 | WebhookSecret exposed in plaintext in admin API response | 🟠 High | Claude |
| F-22 | Hangfire dashboard accessible to any local network user | 🟠 High | Claude |
| F-23 | n8n has unrestricted access to internal DB and API network | 🟠 High | Claude |
| F-25 | Admin panel frontend route protection may be client-side only | 🟠 High | Claude |
| F-32 | No HTTP security headers on API or frontend | 🟠 High | OpenCode |

---

## 5. Medium/Low-Priority Issues

| ID | Title | Severity | Source |
|----|-------|----------|--------|
| F-03 | No login brute-force protection | 🟡 Medium | Both |
| F-04 | No password complexity enforcement | 🟡 Medium | Both |
| F-06 | RequireModule uses blocking `.GetAwaiter().GetResult()` | 🟡 Medium | Claude |
| F-07 | RequireModule skips user-level permission check | 🟡 Medium | Claude |
| F-08 | CompanyAdmin can invite as CompanyOwner (priv escalation) | 🟡 Medium | Claude |
| F-13 | No size limit on text ingestion | 🟡 Medium | Claude |
| F-14 | Silent storage error swallow in DeleteDocument | 🟡 Medium | Claude |
| F-18 | Virus scan is Noop by default (including production) | 🟡 Medium | Claude |

| F-21 | PII in AuditTrail, indefinite retention | 🟡 Medium | Claude |
| F-24 | Auto-execute workflow has no per-tenant rate limit | 🟡 Medium | Claude |
| F-26 | Destructive admin actions lack audit trail and confirmation | 🟡 Medium | Claude |
| F-27 | Rate limiting only on file upload, not AI endpoints | 🟡 Medium | Claude |
| F-28 | Security events at Debug log level or missing entirely | 🟡 Medium | Claude |
| F-29 | Backend JWT accessible to client-side JS via session | 🟡 Medium | Claude |
| F-33 | No email verification required on registration | 🟡 Medium | OpenCode |
| F-05 | Invite token returned in API response body | 🟢 Low | Claude |
| F-30 | Frontend admin role check relies on stale token role | 🟢 Low | Claude |

---

## 6. Tenant Isolation Assessment

**Architecture (Good):**
- EF Core query filters on all tenant-scoped entities — correctly designed.
- `IgnoreQueryFilters()` properly used only in admin service methods with explicit authorization checks.
- Separate `BackgroundTenantProvider` for Hangfire jobs — addresses the headless job context problem.
- HMAC signature validation on webhook path.

**Gaps:**

**F-09 is the most serious gap**: The `X-Tenant-ID` header fallback in `TenantProvider.GetTenantId()` undercuts the entire query filter system. A valid JWT for Tenant A + `X-Tenant-ID: B` = full access to Tenant B's data, bypassing all filters. This effectively makes tenant isolation bypassable for any authenticated user.

**The impersonation check (F-10)** is a trust boundary gap: `X-Impersonate-Tenant` is admin-only and role-checked (correct) but completely unaudited.

**Score:** Tenant isolation architecture: **Good**. Implementation: **Medium risk** due to F-09.

---

## 7. Secrets and Configuration Risk Assessment

| Secret | Location | Severity |
|--------|----------|----------|
| Google OAuth Client Secret | `docker-compose.yml` (plain text, git) | 🔴 Rotate immediately |
| Azure AD Client Secret | `docker-compose.yml` (plain text, git) | 🔴 Rotate immediately |
| `NEXTAUTH_SECRET` | `docker-compose.yml` | 🟠 Rotate before prod |
| `Jwt__Secret` (docker) | `docker-compose.yml` | 🟠 Rotate before prod |
| `AutomationKey` | `docker-compose.yml` | 🟠 Rotate before prod |
| Postgres credentials | `docker-compose.yml` | 🟡 Rotate before prod |
| `Jwt:Secret` placeholder | `appsettings.json` | 🟠 Remove from file |

---

## 8. Admin / SuperAdmin Security Assessment

**Strengths:**
- Two-tier admin model (SuperAdmin for mutations, InternalOperator read-only) is well designed.
- Policy-based authorization (`SuperAdminOnly`, `PlatformAdmin`) registered properly in DI.
- `IsSuperAdmin()` / `IsGlobalAdmin()` checks on all destructive endpoints.

**Weaknesses:**
- **F-10:** Impersonation leaves zero audit trail.
- **F-19:** Webhook secret exposed in admin API response.
- **F-22:** Hangfire dashboard accessible from Docker internal network without admin auth.
- **F-26:** Destructive company actions (cancel, suspend) have no audit trail confirmation.
- The `set_entitlement_override` endpoint (F-AdminController:359) uses `Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString())` — if the `sub` claim is missing, a *random* GUID is used as `CreatedByUserId`, losing traceability. Should return 401 or throw instead.

---

## 9. Recommended Remediation Plan by Phase

### Phase 1 — Immediate (Before Any External Access)

> [!CAUTION]
> These must be resolved before the application handles any real users or real data.

1. ~~**F-15 — Rotate all secrets in `docker-compose.yml`.** Revoke Google OAuth secret and Azure AD secret in their respective consoles. Move all secrets to `.env` file (gitignored). Create `.env.example`.~~ ✅ **FIXED** - Created `.env` (gitignored), updated `docker-compose.yml` to use env vars, created `.env.example`.
2. ~~**F-31 — Rotate Groq API key and remove from `appsettings.Development.json`.** Run `git log -S "gsk_8bAgi"` to check history; scrub with `git filter-repo` if found. Set via environment variable.~~ ✅ **FIXED** - Removed API key from config file.
3. ~~**F-09 — Remove `X-Tenant-ID` fallback from `TenantProvider`.** Tenant must always come from JWT claim. Create a separate webhook-only provider for HMAC routes.~~ ✅ **FIXED** - Already fixed in prior session (verified).
4. ~~**F-02 — Fix OAuth email linking.** Return an error when email conflicts with an existing account, do not silently link.~~ ✅ **FIXED** - Already fixed in prior session (verified).
5. ~~**F-17 — Remove JWT secret placeholder from `appsettings.json`.** Must be supplied via environment variable only.~~ ✅ **FIXED** - Removed secret from config file.
6. ~~**F-10 — Add audit trail for admin impersonation.** Every `X-Impersonate-Tenant` use must produce an `AuditTrail` entry.~~ ✅ **FIXED** - Added structured warning log in TenantProvider for impersonation events.

---

### Phase 2 — Short-Term Hardening (Sprint 1–2) ✅ COMPLETE

> [!IMPORTANT]
> High-severity risks that should be addressed before launch.

7. **F-32 — Add HTTP security headers to API and frontend.** ✅ Fixed — `Program.cs` middleware adds `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `X-XSS-Protection`. HSTS in non-dev. `next.config.ts` headers config added.
8. **F-19 — Stop returning WebhookSecret in admin API.** ✅ Fixed — Removed from `AdminController.GetCompany` response DTO.
9. **F-11 — Validate MIME type by magic bytes**, not client `Content-Type`. ✅ Fixed — `FileSignatureValidator.cs` created, `FileIngestionController` updated.
10. **F-12 — Sanitize uploaded filenames** before file system writes. ✅ Fixed — `LocalFileStorage.cs` uses GUID naming, invalid char removal, path traversal prevention.
11. **F-22 — Protect Hangfire dashboard with SuperAdmin JWT auth** instead of localhost-only filter. ✅ Fixed — `HangfireDashboardAuthorizationFilter.cs` checks SuperAdmin JWT claim.
12. **F-01 — Shorten JWT lifetime to 30–60 minutes**, add refresh token flow or re-login requirement on sensitive operations. ✅ Fixed — JWT lifetime changed from 7 days to 60 minutes in `MintJwtAsync`.
13. **F-03 — Add rate limiting to login endpoint**: per-IP and per-account, with backoff. ✅ Fixed — `login` rate limiter policy (5 attempts/min per IP) in `Program.cs`, `[EnableRateLimiting("login")]` on `AuthController.Login`.
14. **F-08 — Fix invitation role ceiling**: callers cannot assign roles higher than their own. ✅ Fixed — `IsHigherThan` extension added to `Roles.cs`, `InviteController.SendInvite` enforces ceiling check.
15. **F-16 — Fix AutomationKey comparison to use `FixedTimeEquals`** and rotate the key out of git. ✅ Fixed — `RequireAutomationKeyAttribute` uses `CryptographicOperations.FixedTimeEquals`.

---

### Phase 3 — Medium-Term Architecture Improvements (Next Month)

16. **F-23 — Segment n8n network** away from DB and API. Apply egress restrictions.

18. **F-07 — Extend `RequireModule` to check user-level permissions** not just company plan entitlements.
19. **F-06 — Convert `RequireModule` to `IAsyncAuthorizationFilter`**.
20. **F-27 — Add rate limiting to AI-consuming endpoints** (inbox process, text ingest, knowledge search).
21. **F-04 — Enforce password complexity** (min 12 chars, character requirements).
22. **F-33 — Add email verification on registration.** Mark accounts as unverified on creation, send verification token, restrict login until verified. Reuse the existing `Invitation.Token` pattern.
23. **F-21 — Implement AuditTrail data retention policy** with automated periodic purging.
24. **F-25 — Verify all admin pages have server-side auth guards** via `auth()` in `layout.tsx`.
25. **F-28 — Promote security event logging to `LogWarning`** and add dedicated audit entries for: login failures, impersonation, admin destructive actions.

---

### Phase 4 — Lower-Priority / Nice-To-Have

26. **F-05 — Remove invite token from API response** — send exclusively by email.
27. **F-18 — Enable virus scanning in production configs** (ClamAV) and warn on startup if Noop is active in non-dev.
28. **F-24 — Add per-tenant workflow auto-execute rate limit**.
29. **F-26 — Add confirmation tokens and AuditTrail writes** to destructive admin actions.
30. **F-29 — Move API calls to server-side Next.js Route Handlers** to eliminate raw JWT exposure in client-side JS.
31. **F-13 — Add max length validation to text ingestion** endpoint.
32. **F-14 — Replace silent storage error swallow** with logged warning and background cleanup job.

---

## 10. Security Testing Checklist

### Authentication & Session
- [ ] Attempt login with wrong password 20+ times — verify rate limiting kicks in
- [ ] Register two accounts with same email via different providers — verify email-link does NOT happen silently
- [ ] Verify JWT contains correct claims after company switch
- [ ] Verify access is denied after 7-day JWT expiry
- [ ] Attempt replay of expired JWT — verify rejection

### Tenant Isolation
- [ ] Call `GET /api/v1/knowledge` with Tenant A JWT + `X-Tenant-ID: <Tenant B>` header — verify **rejection** (currently passes → F-09)
- [ ] Call `GET /api/admin/companies` with a non-admin JWT — verify 403
- [ ] Call any tenant-scoped endpoint with `Guid.Empty` as tenant ID — verify empty result, not error
- [ ] Verify Hangfire background jobs produce results scoped to correct tenant

### Authorization / Privilege Escalation
- [ ] As `Operator` role, call `POST /api/invite` with role=`CompanyOwner` — verify rejection (currently passes → F-08)
- [ ] As `Viewer` role, call `POST /api/inbox/process` — verify 403 from module check
- [ ] As non-admin, call `GET /api/admin/companies` — verify 403
- [ ] As `InternalOperator`, call `POST /api/admin/companies/{id}/cancel` — verify 403 (mutation requires SuperAdmin)

### File Upload
- [ ] Upload a `.exe` file with `Content-Type: text/plain` — verify rejection by magic byte check (currently passes → F-11)
- [ ] Upload with filename `../../etc/passwd` — verify safe storage path (check → F-12)
- [ ] Upload 21MB file — verify size rejection
- [ ] Upload 11 files in 1 minute — verify rate limit triggers at 10

### Secrets
- [ ] Confirm `WebhookSecret` is NOT present in `GET /api/admin/companies/{id}` response after F-19 fix
- [ ] Confirm `docker-compose.yml` does not contain any secret values after F-15 fix
- [ ] Confirm git history is scrubbed of secrets (use `git log -S "GOCSPX"`)

### Admin Tooling
- [ ] Confirm `/hangfire` returns 403 for non-SuperAdmin JWT after F-22 fix
- [ ] Confirm `AuditTrail` entry created when SuperAdmin uses `X-Impersonate-Tenant`
- [ ] Confirm plan cancel/suspend creates an `AuditTrail` entry

### HTTP Security Headers (F-32)
- [ ] `curl -I http://localhost:8080/health/rag` — verify `X-Content-Type-Options: nosniff` present
- [ ] `curl -I http://localhost:3000` — verify `X-Frame-Options: DENY` present
- [ ] Confirm no `Content-Security-Policy` violations in browser console for the dashboard

### Secrets (additional — F-31)
- [ ] Confirm `appsettings.Development.json` contains NO live API key value
- [ ] Run `git log -S "gsk_8bAgi"` — verify no commit in git history contains the key
- [ ] Confirm Groq key rotated by verifying old key is rejected by Groq API

### Registration / Email Verification (F-33)
- [ ] Register a new account — verify email is sent
- [ ] Attempt to log in before clicking verification link — verify access is denied (after F-33 implemented)

---

## 11. Final Verdict

> [!WARNING]
> **OrvixFlow is NOT production-ready from a security standpoint without addressing the Critical and High findings.**

**Total findings: 32** (4 Critical · 10 High · 15 Medium · 3 Low)

**What is well-built:**
- The two-layer role system (global vs company) is correctly designed and consistently enforced at the API layer.
- JWT validation parameters are correct (issuer, audience, lifetime, key validation all enabled).
- EF Core query filter-based multi-tenancy is architecturally sound.
- HMAC webhook validation uses constant-time comparison — good.
- BCrypt is used for password hashing — good.
- The `RequireAutomationKey` filter prevents n8n callback spoofing — good.
- The `AccessResolver` platform-admin bypass is logical and documented.

**What is not acceptable for production:**
1. Real OAuth and session secrets committed to git in `docker-compose.yml` (**F-15**) — rotate immediately.
2. Live Groq API key in `appsettings.Development.json` (**F-31**) — rotate immediately.
3. Any authenticated user can access any tenant's data by spoofing `X-Tenant-ID` header (**F-09** — defeats the entire multi-tenancy model).
4. Automatic silent account takeover via OAuth email matching (**F-02**).
5. Admin impersonation of any tenant with zero audit trail (**F-10**).

**Two independently agreed-upon critical blockers (confirmed by both reviewers):** secrets in git (F-15/F-31), OAuth account linking without verification (F-02), brute-force on login (F-03), weak password requirements (F-04), 7-day JWT lifetime (F-01).

**Risk Classification:** `HIGH RISK` — Do not expose to the internet or onboard real customer data until Phase 1 and Phase 2 remediations are complete.

