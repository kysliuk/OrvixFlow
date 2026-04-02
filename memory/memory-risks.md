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

**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs:99-115`

**Risk:** Account takeover via email matching

**Pattern:** If email exists with different OAuth provider, account is linked automatically.

**When changing:**
- Verify existing test `ProvisionOAuthUserAsync`

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

## Architecture Boundaries

### DO NOT CROSS

1. **Api → Core**: Only interfaces and entities
2. **Api → Infrastructure**: No direct service calls (use DI)
3. **Core → Infrastructure**: No dependencies
4. **Frontend → Backend**: Only via fetchApi() or auth.ts

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
