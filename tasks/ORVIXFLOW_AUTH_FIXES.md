# OrvixFlow Authentication & Authorization Fix Plan

This document outlines the findings and the phased, step-by-step implementation plan to resolve critical bugs and architectural regressions discovered in the OrvixFlow Auth system.

## Investigation Summary
- **Module Bypass Bug**: `CompanyAdmin` and `CompanyOwner` users are completely bypassing module entitlement checks due to an early return in `RequireModuleAttribute.cs`.
- **Tenant Context Loss on Refresh**: `AuthService.RefreshSessionAsync()` incorrectly uses the default `user.TenantId` rather than the token's current `ActiveCompanyId`, reverting users who switched companies back to their default company upon auto-refresh.
- **Broken Owner Workflows**: A mismatch between string claims (`"CompanyOwner"`) and API role statics (`"Owner"`) causes `Roles.IsAdmin()` to fail, wrongly locking company owners out of Invites and Billing Summaries (`403 Forbidden`).
- **Thread Starvation Risk**: `ScopeContext.cs` is executing raw `.GetAwaiter().GetResult()` while resolving data boundaries, creating a severe sync-over-async thread block.

---

## Step-By-Step Implementation Plan

### Phase 1: Critical Security & Revenue Blockers

**Task 1: Fix `RequireModuleAttribute` Entitlement Bypass**
- **File**: `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- **Issue**: Company Admins immediately return and bypass company-level plan entitlement gates (`CanUseModuleAsync`). 
- **Action**: 
  - Relocate the early return `if (parsedRole.IsCompanyAdmin()) return;` so it executes **after** `CanUseModuleAsync` checks out.
  - Ensure Company Admins are only bypassing the *user-level* granularity permissions checks (i.e. `accessResolver.GetEffectivePermissionsAsync`), not the billing entitlement checks.

**Task 2: Fix Refresh Token Losing Active Company Context**
- **File**: `OrvixFlow.Api/Controllers/AuthController.cs`, `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- **Issue**: `RefreshSessionAsync` blindly resets `activeCompanyId` to `user.TenantId` when minting the new token.
- **Action**:
  - Update `RefreshRequest` to accept an optional `Guid? ActiveCompanyId`.
  - Pass it to `AuthService.RefreshSessionAsync(string refreshToken, Guid? activeCompanyId = null)`.
  - Inside `RefreshSessionAsync`, if `activeCompanyId` is provided, confirm the user has an `Active` membership to this target company using `_db.UserCompanyMemberships`. If valid, mint the new JWT with the provided `activeCompanyId`. If missing or invalid, fallback safely to `user.TenantId`.

### Phase 2: Fix Broken Endpoints & Role Mismatches 

**Task 3: Fix `OrganizationController` Invites**
- **File**: `OrvixFlow.Api/Controllers/OrganizationController.cs`
- **Issue**: `OrganizationController.Invite` uses `Roles.IsAdmin(inviterRole)`, which evaluates `"CompanyOwner"` to `false`. Company Owners get 403 Forbidden when trying to invite users.
- **Action**: Replace `if (!Roles.IsAdmin(inviterRole))` with `if (!UserRoleExtensions.ParseRole(inviterRole).IsCompanyAdminOrAbove())`.

**Task 4: Fix `BillingController` Usage Summary**
- **File**: `OrvixFlow.Api/Controllers/BillingController.cs`
- **Issue**: `BillingController.Summary` uses `Roles.IsAdmin(role)`, throwing 403 for company owners.
- **Action**: Replace `if (!Roles.IsAdmin(role))` with `if (!UserRoleExtensions.ParseRole(role).IsCompanyAdminOrAbove())`. 

### Phase 3: Threading & Architecture Resilience

**Task 5: Refactor `ScopeContext` Sync-Over-Async Starvation**
- **File**: `OrvixFlow.Infrastructure/Auth/ScopeContext.cs`
- **Issue**: `.GetAwaiter().GetResult()` runs database queries on a synchronous property getter (`EnsureResolved`).
- **Action**: 
  - Refactor data boundary resolution. Ideally, since `ScopeContext` is used by `AccessResolver`, remove synchronous properties and introduce an `InitializeAsync()` or just pass the parameters dynamically down when resolving. 
  - Alternatively, if `IScopeContext` forces synchronous properties in the architecture, switch it to evaluate the query dynamically without blocking, or ensure it's bootstrapped via middleware asynchronously.

### Phase 4: Verification & Tests

**Task 6: Ensure full Test Coverage**
- Run test suite: `dotnet test`
- Specifically add/update tests:
  - Add `CompanyAdmin_CannotAccessModule_IfCompanyNotEntitled` in `RequireModuleAttributeTests.cs`.
  - Add `RefreshSession_WithActiveCompanyId_RetainsCompanySwitch` in `AuthServiceTests.cs`.
  - Validate `CompanyOwner_CanInviteUsers_Successfully` in `OrganizationControllerTests.cs`.

--- 
**Prepared**: 2026-04-16
**Status**: Ready for Execution
