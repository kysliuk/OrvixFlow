# OrvixFlow - Risks & Fragile Areas

## Critical: Tenant Isolation

**Location:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs:168-185`

**Risk:** Data leakage between tenants

**Pattern:** All queries use global query filters for tenant isolation. **NEVER** bypass these filters without explicit justification and testing.

**Safe bypass:**
```csharp
_db.Entity.IgnoreQueryFilters() // Admin operations only
```

**When changing:**
- Run `TenantIsolationTests.cs`
- Verify no tenant ID in query results

---

## Critical: Module Permission Gating

**Location:** `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`

**Risk:** Unauthorized access to premium features

**Pattern:** `[RequireModule("module-key")]` attribute gates access. Must check both CanView AND CanUse.

**When changing:**
- Test with different role types (CompanyOwner, Manager, Member)
- Test at Company/Department/User scopes

---

## Critical: OAuth Account Linking

**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs:107-119`

**Risk:** Account takeover via email matching

**Pattern:** FIXED - If email exists with different OAuth provider, returns error: "An account with this email already exists. Please sign in with your original authentication method."

**Note:** EF Core InMemory does not support `Include` on missing related entities. The `ProvisionOAuthUserAsync` email check uses `IgnoreQueryFilters()` without `Include` — the Tenant is not needed for the email-existence check.

**When changing:**
- Verify existing test `ProvisionOAuthUserAsync`

---

## Critical: Tenant Isolation (X-Tenant-ID Header)

**Location:** `OrvixFlow.Api/Services/TenantProvider.cs:44-47`

**Risk:** Cross-tenant data access via HTTP header

**Pattern:** FIXED - Removed `X-Tenant-ID` header fallback. Tenant ID must ALWAYS come from JWT claims (`ActiveCompanyId` or `TenantId`). For webhook paths that need X-Tenant-ID, use a separate WebhookTenantProvider that validates HMAC signature.

**When changing:**
- Run `TenantProviderTests.cs`
- Verify no tenant header bypass is possible

---

## Critical: Admin Impersonation Audit Trail

**Location:** `OrvixFlow.Api/Services/TenantProvider.cs:32-38`

**Risk:** Platform admin can impersonate any tenant without audit trail

**Pattern:** FIXED - Admin impersonation via `X-Impersonate-Tenant` header is now logged at Warning level with structured data: AdminUserId, ImpersonatedTenantId, RemoteIp. Log entry includes "SECURITY: Admin impersonation started."

**When changing:**
- Check logs for impersonation events
- Ensure logging pipeline captures Warning level for TenantProvider

---

## High: Secrets Management

**Location:** `docker-compose.yml`, `.env`, `appsettings.json`, `appsettings.Development.json`

**Risk:** Secrets committed to git, exposure via logs

**Pattern:** FIXED - All secrets moved to `.env` file (gitignored). docker-compose.yml uses `${VAR}` syntax with `env_file: .env`. JWT secret and API keys removed from appsettings files.

**Required:**
- `.env` must exist and contain all required secrets before running
- Use `.env.example` as template for new deployments
- Rotate any secrets that were previously committed (Google, Azure, Groq)

**When changing:**
- Never commit secrets to git
- Use environment variables in all environments

---

## High: JWT Claims Parsing

**Location:** `OrvixFlow.Api/Services/TenantProvider.cs`, `OrvixFlow.Infrastructure/Auth/ScopeContext.cs`

**Risk:** Missing claims cause runtime errors

**Pattern:** Claims extracted: `TenantId`, `ActiveCompanyId`, `Role`, `Plan`, `sub` (userId)

**When changing:**
- Add new claims to `AuthService.MintJwtAsync` AND `BuildProfileAsync`
- Test token refresh scenarios

---

## High: Background Jobs

**Location:** `OrvixFlow.Api/Jobs/InboxProcessingJob.cs`

**Risk:** Jobs run outside request context (no JWT)

**Pattern:** Uses `BackgroundTenantProvider` for tenant context

**When changing:**
- Verify job executes with correct tenant
- Check Hangfire dashboard at `/hangfire`

---

## High: Webhook HMAC Validation

**Location:** `OrvixFlow.Api/Middleware/HmacSignatureMiddleware.cs`

**Risk:** Unvalidated webhooks could inject data

**Pattern:** Only `/api/webhook/inbox` is protected. Tenant secret must exist.

**When changing:**
- Run `HmacSignatureMiddlewareTests.cs`
- Test missing/invalid signatures

---

## Medium: Query Filter with Navigation Properties

**Location:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs:181`

**Risk:** Null reference if navigation property not loaded

```csharp
g.ModuleAssignment != null && g.ModuleAssignment.CompanyId ...
```

**Pattern:** Null check required due to global filter interaction

---

## Medium: InboxEvent Idempotency

**Location:** `OrvixFlow.Infrastructure/Data/InboxEventRepository.cs`

**Risk:** Duplicate processing of same email

**Pattern:** Uses `MessageId` for deduplication

---

## Critical: Global Roles vs Company Roles — Two Separate Layers

**Location:** `OrvixFlow.Core/Authorization/Roles.cs`, `OrvixFlow.Infrastructure/Auth/AuthService.cs:MintJwtAsync`, `OrvixFlow.Core/Entities/User.cs`

**Risk:** Conflating global roles with company roles causes authorization failures

**Two distinct role layers:**

### Global (platform-level) — stored in `User.Role`
- `SuperAdmin` — full platform control
- `InternalOperator` — platform support, read-only admin access
- Empty (`""`) — normal users (no global role)

### Company (organization-level) — stored in `UserCompanyMembership.CompanyRole`
- `CompanyOwner` — full control within their company
- `CompanyAdmin` — delegated company management
- `DepartmentManager` — manages within assigned department(s)
- `Operator` — performs work within assigned modules
- `Viewer` — read-only within assigned modules

**JWT `Role` claim:**
- For users with a global role (`SuperAdmin`, `InternalOperator`): JWT contains the global role
- For normal users: JWT contains their `UserCompanyMembership.CompanyRole`
- Determined in `MintJwtAsync`: checks `User.Role` first, if it's a platform admin role, uses it; otherwise falls back to `CompanyRole`

**Critical rule:** `User.Role` should ONLY be set for platform admins. Normal users should have `User.Role = ""`. The company role is stored in `UserCompanyMembership.CompanyRole`, NOT in `User.Role`.

**When changing role logic:**
- Never set `User.Role` to a company role (e.g., `CompanyOwner`, `Operator`)
- Never compare `User.Role` against company roles
- `AccessResolver` reads from `UserCompanyMembership.CompanyRole` — this is correct
- `ScopeContext` reads from JWT `Role` claim — works because platform roles pass `IsCompanyAdminOrAbove()`

---

## Medium: Role Parsing

**Location:** `OrvixFlow.Core/Authorization/Roles.cs`

**Risk:** Inconsistent role strings across system

**Pattern:** Canonical roles stored as claim values. Use `UserRoleExtensions.ParseRole()` for all comparisons.

---

## Low: Soft Delete via Query Filters

**Location:** Various entities use soft delete pattern

**Pattern:** Deleted items filtered out via global query filters

**When changing:**
- Use `IgnoreQueryFilters()` for admin deletes

---

## Low: InMemory Vector Search Fallbacks

**Location:** `OrvixFlow.Infrastructure/Ai/ImageResolver.cs`

**Risk:** Unit tests skip vector arithmetic translation because `InMemoryDatabase` doesn't support pgvector `CosineDistance`.

**Pattern:** Catch `Exception` in LINQ queries using vector arithmetic and provide clear client-side evaluation fallback for testing.

**Critical:** Fallback MUST filter by `TenantId` manually to maintain isolation in tests.

---

## High: Query Filter Bypass Required for Admin Operations

**Location:** `OrvixFlow.Infrastructure/Services/CompanySubscriptionService.cs`, `OrvixFlow.Infrastructure/Services/EntitlementResolver.cs`

**Risk:** Admin queries for other companies' subscriptions, entitlement overrides, and module overrides are silently blocked by tenant query filters. The filter `s.CompanyId == _tenantProvider.GetTenantId()` returns the admin's own company ID (or `Guid.Empty`), not the target company's ID.

**Symptoms:**
- `GetSubscriptionAsync()` returns `null` for any company the admin doesn't belong to → "No Subscription" shown in UI
- `AssignPlanAsync()` fails to find existing subscription → attempts INSERT → **unique constraint violation** → 500 error

**Pattern:** All admin-facing service methods that query `CompanySubscription`, `CompanyEntitlementOverride`, or `CompanyModuleOverride` by `companyId` must use `.IgnoreQueryFilters()`.

**Fixed methods (must maintain this pattern):**
- `CompanySubscriptionService.GetSubscriptionAsync()`
- `CompanySubscriptionService.AssignPlanAsync()` (existing subscription check)
- `EntitlementResolver.GetSubscriptionAsync()`
- `EntitlementResolver.GetEntitlementOverrideAsync()`
- `EntitlementResolver.GetModuleOverridesAsync()`

**When adding new admin queries:** Always check if the entity has a query filter in `AppDbContext`. If yes, use `.IgnoreQueryFilters()` — safe because admin endpoints enforce their own authorization (`IsSuperAdmin()`, `IsGlobalAdmin()`).

---

## High: RAG Ingestion (DoS)

**Location:** `OrvixFlow.Api/Controllers/FileIngestionController.cs`

**Risk:** Large file uploads or high frequency can exhaust resources or incur costs.

**Mitigation:** 
- Native .NET rate limiting on `/api/v1/knowledge/upload` (fixed window).
- Max file size (10MB) and allowed MIME types enforced.
- Virus scanning hook (`IVirusScanService`) integrated.

**When changing:**
- Test with oversized files.
- Verify rate limiter triggers after 10 requests/min.

---

## High: AI Service Connectivity

**Location:** `OrvixFlow.Api/Health/RagHealthCheck.cs`

**Risk:** RAG features fail silently if AI providers or pgvector are down.

**Mitigation:**
- Dedicated `/health/rag` endpoint checks DB vector ops and Embedding API.

**When changing:**
- Run `RagHealthCheck` manually.

---

## Architecture Boundaries

### DO NOT CROSS

1. **Api → Core**: Only interfaces and entities
2. **Api → Infrastructure**: No direct service calls (use DI)
3. **Core → Infrastructure**: No dependencies
4. **Frontend → Backend**: Only via fetchApi() or auth.ts

---

---

## Critical: Two Roles Classes

**Locations:**
1. `OrvixFlow.Core/Authorization/Roles.cs` — enum + extension methods (`IsHigherThan`, `IsPlatformAdmin`, etc.)  
2. `OrvixFlow.Api/Roles.cs` — static class with string constants (`"Admin"`, `"Owner"`, `"SuperAdmin"`)

**TenantProvider impersonation:** `TenantProvider.GetTenantId()` uses `Roles.IsAdmin(roleClaim)` (from `Api/Roles.cs`) which only recognizes `"Admin"`, `"Owner"`, `"SuperAdmin"` — NOT `"CompanyAdmin"` or `"CompanyOwner"`. This means company-level admins cannot use the `X-Impersonate-Tenant` header for impersonation; only platform admins (SuperAdmin) can.

**When adding roles:** Add to BOTH files if applicable. `Core/Authorization/Roles.cs` for enum/extension logic, `Api/Roles.cs` for impersonation.

---

## Critical: EF Core InMemory + Include with Missing Related Entities

**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs:ProvisionOAuthUserAsync`

**Issue:** `Include(u => u.Tenant)` in EF Core InMemory causes the entire query to return `null` when the related `Tenant` record doesn't exist, even when the `User` record does exist. This silently bypasses security checks.

**Pattern:** For existence checks (finding if a User with given email/provider exists), do NOT use `Include`. The navigation property data is not needed for the check.

**Safe pattern:**
```csharp
// For existence checks — NO Include
var user = await _db.Users.IgnoreQueryFilters()
    .FirstOrDefaultAsync(u => u.Email == email);

// For loading related data — Include AFTER existence check
var userWithTenant = await _db.Users.IgnoreQueryFilters()
    .Include(u => u.Tenant)
    .FirstOrDefaultAsync(u => u.Id == userId);
```

---

## Adding New Features

1. Create entity in `OrvixFlow.Core/Entities/`
2. Add interface in `OrvixFlow.Core/Interfaces/`
3. Implement service in `OrvixFlow.Infrastructure/`
4. Register DI in `OrvixFlow.Infrastructure/DependencyInjection.cs`
5. Add controller in `OrvixFlow.Api/Controllers/`
6. Add tests in `OrvixFlow.Tests/`
7. Add frontend in `orvixflow-web/app/`

---

## Required Test Coverage

When modifying these areas, tests MUST pass:

| Area | Test File |
|------|-----------|
| Tenant isolation | `TenantIsolationTests.cs` |
| Auth flow | `AuthControllerTests.cs` |
| Permissions | `AccessResolverTests.cs` |
| Webhooks | `HmacSignatureMiddlewareTests.cs` |
| Background jobs | `InboxProcessingJob` (manual) |

---

## Security Remediation Status (2026-04-14)

### Phase 1 Complete ✅
| Finding | Description | Status |
|---------|-------------|--------|
| F-09 | X-Tenant-ID header fallback removed | ✅ Fixed |
| F-10 | Admin impersonation audit logging | ✅ Fixed |
| F-15 | Secrets moved to .env file | ✅ Fixed |
| F-17 | JWT secret removed from appsettings.json | ✅ Fixed |
| F-31 | Groq API key removed from config | ✅ Fixed |
| F-02 | OAuth email linking returns error | ✅ Fixed |

### Phase 2 Complete ✅
| Finding | Description | Status |
|---------|-------------|--------|
| F-32 | HTTP security headers (API middleware + Next.js headers) | ✅ Fixed |
| F-11 | File MIME type validation (magic bytes in FileSignatureValidator) | ✅ Fixed |
| F-12 | Filename sanitization (GUID naming + path traversal prevention) | ✅ Fixed |
| F-14 | Silent error swallowing in file deletion (logged now) | ✅ Fixed |
| F-22 | Hangfire dashboard auth (SuperAdmin-only via authorization filter) | ✅ Fixed |
| F-01 | JWT lifetime shortened from 7 days to 60 minutes | ✅ Fixed |
| F-03 | Login rate limiting (5 attempts/min per IP via sliding window) | ✅ Fixed |
| F-28 | Failed login logging elevated to Warning level | ✅ Fixed |
| F-16 | AutomationKey comparison uses FixedTimeEquals | ✅ Fixed |
| F-08 | Invitation role ceiling check (IsHigherThan extension method) | ✅ Fixed |
| F-19 | WebhookSecret removed from AdminController.GetCompany response | ✅ Fixed |

### Phase 3 Complete ✅ (2026-04-14)
| Finding | Description | Status |
|---------|-------------|--------|
| F-06 | RequireModule uses async (IAsyncAuthorizationFilter) | ✅ Fixed |
| F-25 | Admin pages have server-side auth guards | ✅ Fixed |
| F-07 | RequireModule checks user-level permissions | ✅ Fixed |
| F-27 | Rate limiting on AI endpoints | ✅ Fixed |
| F-04 | Password complexity enforcement | ✅ Fixed |
| F-33 | Email verification on registration | ✅ Fixed |
| F-21 | AuditTrail data retention policy | ✅ Fixed |
| F-23 | n8n network segmentation | ✅ Fixed |

**Deferred:** F-20 (MinIO) — separate implementation

**Note:** F-20 (MinIO storage) deferred to separate implementation.

### Volume Mounts Added ✅
| Volume | Service | Path | Purpose |
|--------|---------|------|---------|
| pgdata | orvix-db | /var/lib/postgresql/data | PostgreSQL data persistence |
| n8n_data | n8n | /home/node/.n8n | n8n workflow data |
| uploads_data | orvix-api | /app/uploads | File uploads persistence |

**Note:** uploads_data volume is temporary. Future F-20 will replace with MinIO (S3-compatible) storage.

---

## Recent Security Fixes (2026-04-14)

### F-06: RequireModule Async Conversion
**Location:** `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`

**Fix:** Converted from synchronous `IAuthorizationFilter` to `IAsyncAuthorizationFilter`. Now uses proper `await` instead of `.GetAwaiter().GetResult()`:
- Changed `OnAuthorization(AuthorizationFilterContext)` → `OnAuthorizationAsync(AuthorizationFilterContext)`
- Uses `await entitlementResolver.CanUseModuleAsync(...)` instead of blocking call
- Prevents thread pool starvation under load

**Tests:** `RequireModuleAttributeTests.cs` added

### F-25: Admin Pages Server-Side Auth Guards
**Location:** `orvixflow-web/middleware.ts`

**Fix:** Added server-side role check in Next.js middleware:
- Now checks `req.auth?.user?.role` for all `/admin` routes
- Redirects non-SuperAdmin/InternalOperator users to homepage
- Runs before any UI renders — no flash of content

**Code added:**
```typescript
// Server-side role check for admin routes (F-25)
if (isLoggedIn && pathname.startsWith("/admin")) {
  const role = req.auth?.user?.role;
  const isSuperAdmin = role === "SuperAdmin" || role === "InternalOperator";
  if (!isSuperAdmin) {
    return Response.redirect(new URL("/", req.nextUrl));
  }
}
```

**Note:** The client-side check in `admin/layout.tsx` remains as defense-in-depth.

### F-07: RequireModule User-Level Permission Check
**Location:** `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`

**Fix:** Extended `RequireModuleAttribute` to check user-level permissions via `IAccessResolver.GetEffectivePermissionsAsync`:
- After verifying company entitlement (`CanUseModuleAsync`), also checks user's `CanUse` permission
- CompanyAdmins (`CompanyOwner`, `CompanyAdmin`) bypass user-level check via `UserRoleExtensions.ParseRole().IsCompanyAdmin()`
- Returns 403 "Access Denied" if user lacks permission, even when company has the module

**Code added:**
```csharp
// FIX F-07: Also check user-level permissions (unless already admin via Roles.IsAdmin above)
var userIdClaim = user.FindFirst("sub")?.Value;
if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
{
    var accessResolver = context.HttpContext.RequestServices.GetService(typeof(IAccessResolver)) as IAccessResolver;
    if (accessResolver != null)
    {
        var permissions = await accessResolver.GetEffectivePermissionsAsync(userId, companyId, _requiredModule);
        if (!permissions.CanUse)
        {
            context.Result = new ObjectResult(new { error = "Access Denied", message = $"You do not have permission to use the '{_requiredModule}' module." }) { StatusCode = 403 };
            return;
        }
    }
}
```

**Tested:** All 314 tests pass

### F-27: Rate Limiting on AI Endpoints
**Location:** `OrvixFlow.Api/Program.cs`, `InboxController.cs`, `AgentController.cs`, `KnowledgeBaseController.cs`

**Fix:** Added per-tenant+IP rate limiting policies to prevent AI API cost abuse:
- `ai-process` — 30 requests/minute (for `/api/inbox/process`)
- `ai-ingest` — 20 requests/minute (for `/api/agent/ingest`)
- `ai-search` — 60 requests/minute (for `/api/v1/knowledge` GET)

All policies use tenant ID + IP as partition key to limit per tenant while still allowing multiple IPs.

**Code added in Program.cs:**
```csharp
options.AddPolicy("ai-process", context =>
{
    var tenantId = context.User.FindFirst("TenantId")?.Value ?? ...;
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"{tenantId}:{ip}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            ...
        });
});
```

**Controllers using rate limiting:**
- `[EnableRateLimiting("ai-process")]` on `InboxController.Process()`
- `[EnableRateLimiting("ai-ingest")]` on `AgentController.Ingest()`
- `[EnableRateLimiting("ai-search")]` on `KnowledgeBaseController.ListKnowledge()`

**Tested:** Build succeeds, 314 tests pass

### F-04: Password Complexity Enforcement
**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs`

**Fix:** Enforce minimum password requirements on registration:
- Minimum 12 characters
- At least one lowercase letter
- At least one uppercase letter  
- At least one number
- At least one special character

**Code added:**
```csharp
private static (bool IsValid, string ErrorMessage) ValidatePasswordComplexity(string password)
{
    if (password.Length < 12) return (false, "Password must be at least 12 characters long.");
    var hasLower = password.Any(char.IsLower);
    var hasUpper = password.Any(char.IsUpper);
    var hasDigit = password.Any(char.IsDigit);
    var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
    // Returns error listing missing character classes
}
```

**Tested:** All 314 tests pass

### F-23: n8n Network Segmentation
**Location:** `docker-compose.yml`

**Fix:** Implemented network segmentation to prevent n8n from accessing internal services directly:

1. **Two separate Docker networks:**
   - `internal` — contains `orvix-db` (PostgreSQL) and `orvix-api`
   - `external` — contains `n8n`, `orvix-web`, and `orvix-api` (for webhook calls)

2. **Network assignment:**
   - `orvix-db` → `internal` only
   - `orvix-api` → BOTH `internal` + `external` (needs to call n8n webhooks)
   - `n8n` → `external` only (cannot reach database directly)
   - `orvix-web` → `external` only

3. **Additional security hardening:**
   - Removed `depends_on: orvix-db` from n8n service (prevents automatic network attachment)
   - Added `EXECUTIONS_MODE=internal` to disable external execution mode
   - Added `N8N_ENCRYPTION_KEY` for encryption at rest
   - Added commented-out basic auth configuration for production use

**Result:** n8n can no longer directly connect to PostgreSQL. It can only communicate with the API via the defined webhook endpoints. If n8n is compromised, the attacker cannot dump the database or access internal services directly.

**Tested:** Docker Compose configuration valid, build succeeds, tests pass
