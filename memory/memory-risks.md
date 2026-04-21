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

## Low: Unified Role System Logic

**Location:** `OrvixFlow.Core/Authorization/Roles.cs`

**Status:** FIXED. Legacy `OrvixFlow.Api/Roles.cs` has been removed.

**Pattern:** All role checks must use `UserRoleExtensions.ParseRole(roleClaim)` followed by extension methods (`IsPlatformAdmin()`, `IsCompanyAdmin()`, etc.).

**TenantProvider impersonation:** Strictly gated to platform admins (`IsPlatformAdmin()`).

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
| Background jobs | `InboxProcessingIntegrationTests.cs` |

---

## Registration Hardening

**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs`

**Features:**
- Password complexity: min 12 chars, upper, lower, digit, special limit.
- Prevent duplicate OAuth users bypassing registration flow.


---

## Critical: Storage Migration Hardening

### MinIO ForcePathStyle Required
- **Location:** `OrvixFlow.Infrastructure/DependencyInjection.cs`
- **Risk:** Without `ForcePathStyle = true`, the AWS SDK generates virtual-hosted requests that fail against local MinIO.
- **When changing:** Preserve `ForcePathStyle = true` for the MinIO client registration.

### IVirusScanService Double-Registration
- **Location:** `OrvixFlow.Infrastructure/DependencyInjection.cs`
- **Risk:** DI uses last-registration-wins semantics. A second `IVirusScanService` registration can silently bypass ClamAV.
- **When changing:** Search for every `IVirusScanService` registration and keep exactly one active registration path.

### Non-Seekable Object Storage Streams
- **Location:** `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`, `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs`, `OrvixFlow.Infrastructure/Storage/AzureBlobFileStorage.cs`
- **Risk:** MinIO and Azure Blob return network streams that are not seekable. Removing the current buffering path can break ingestion and retry behavior.
- **When changing:** Keep the buffering-to-`MemoryStream` pattern before code that rewinds streams.

### StoredObject Query Filter
- **Location:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs`
- **Risk:** `StoredObject` is tenant-filtered. Admin diagnostics and migration tooling can silently miss rows unless they use `IgnoreQueryFilters()` intentionally.
- **When changing:** Use `IgnoreQueryFilters()` only in authorized admin paths and migration workflows.

### Azure Blob Public Access
- **Location:** `OrvixFlow.Infrastructure/Storage/AzureBlobContainerInitializer.cs`
- **Risk:** Creating the container without `PublicAccessType.None` can expose uploaded documents outside the API's RBAC and audit path.
- **When changing:** Preserve private container creation with `PublicAccessType.None` and never allow public blob/container access.
