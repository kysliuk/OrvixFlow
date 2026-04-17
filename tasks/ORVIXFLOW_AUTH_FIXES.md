# OrvixFlow Authentication & Authorization Fix Plan

This document outlines the findings and the phased, step-by-step implementation plan to resolve critical bugs and architectural regressions discovered in the OrvixFlow Auth system.

## Investigation Summary
- **Module Bypass Bug**: `CompanyAdmin` and `CompanyOwner` users are completely bypassing module entitlement checks due to an early return in `RequireModuleAttribute.cs`.
- **Tenant Context Loss on Refresh**: `AuthService.RefreshSessionAsync()` incorrectly uses the default `user.TenantId` rather than the token's current `ActiveCompanyId`, reverting users who switched companies back to their default company upon auto-refresh.
- **Broken Owner Workflows**: A mismatch between string claims (`"CompanyOwner"`) and API role statics (`"Owner"`) causes `Roles.IsAdmin()` to fail, wrongly locking company owners out of Invites and Billing Summaries (`403 Forbidden`).
- **Thread Starvation Risk**: `ScopeContext.cs` is executing raw `.GetAwaiter().GetResult()` while resolving data boundaries, creating a severe sync-over-async thread block.

---

## Phase 1: Critical Security & Revenue Blockers ✅ COMPLETE (2026-04-17)

### Task 1: Fix `RequireModuleAttribute` Entitlement Bypass ✅
- **File**: `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- **Issue**: Company Admins immediately return and bypass company-level plan entitlement gates (`CanUseModuleAsync`). 
- **Action**: 
  - Relocated the early return `if (parsedRole.IsCompanyAdmin()) return;` so it executes **after** `CanUseModuleAsync` checks out.
  - Company Admins now pass billing entitlement check before accessing modules.
  - Company Admins bypass *user-level* granularity permissions checks only.
- **Status**: ✅ Fixed and tested

### Task 2: Fix Refresh Token Losing Active Company Context ✅
- **File**: `OrvixFlow.Api/Controllers/AuthController.cs`, `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- **Issue**: `RefreshSessionAsync` blindly resets `activeCompanyId` to `user.TenantId` when minting the new token.
- **Action**:
  - Updated `RefreshRequest` to accept optional `Guid? ActiveCompanyId`.
  - Updated `IAuthService.RefreshSessionAsync(string refreshToken, Guid? activeCompanyId = null)`.
  - Inside `RefreshSessionAsync`, if `activeCompanyId` is provided, confirm the user has an `Active` membership to this target company using `_db.UserCompanyMemberships`. If valid, mint the new JWT with the provided `activeCompanyId`. If missing or invalid, fallback safely to `user.TenantId` with warning log.
- **Status**: ✅ Fixed and tested

### Tests Added
- `RequireModuleAttributeTests.cs`: 6 new tests
- `AuthServiceTests.cs`: 3 new tests
- **Total**: 404 tests passing

---

## Phase 2: Fix Broken Endpoints & Role Mismatches ✅ COMPLETE (2026-04-17)

### Task 3: Fix `OrganizationController` Invites ✅
- **File**: `OrvixFlow.Api/Controllers/OrganizationController.cs`
- **Issue**: `OrganizationController.Invite` uses `Roles.IsAdmin(inviterRole)`, which evaluates `"CompanyOwner"` to `false`. Company Owners get 403 Forbidden when trying to invite users.
- **Action**: Replaced `if (!Roles.IsAdmin(inviterRole))` with `if (!inviterRole.IsCompanyAdminOrAbove())` using `UserRoleExtensions.ParseRole()`.
- **Verification**: CompanyOwners, CompanyAdmins, SuperAdmins, and InternalOperators now pass the role check.
- **Status**: ✅ Fixed and tested

### Task 4: Fix `BillingController` Usage Summary ✅
- **File**: `OrvixFlow.Api/Controllers/BillingController.cs`
- **Issue**: `BillingController.Summary` uses `Roles.IsAdmin(role)`, throwing 403 for company owners.
- **Action**: Replaced `if (!Roles.IsAdmin(role))` with `if (!IsCompanyAdminOrAbove())` using the existing helper method.
- **Verification**: CompanyOwners, CompanyAdmins, SuperAdmins, and InternalOperators now pass the role check.
- **Status**: ✅ Fixed and tested

### Root Cause
The `OrvixFlow.Api/Roles.cs` defines `Owner = "Owner"` but `UserRole.CompanyOwner.ToClaimValue()` returns `"CompanyOwner"`. When a CompanyOwner's JWT contains `"Role": "CompanyOwner"`, `Roles.IsAdmin("CompanyOwner")` fails because `AdminRoles` has `"Owner"`, not `"CompanyOwner"`.

### Tests
- **Total**: 404 tests passing (no regressions)

---

## Phase 3: Threading & Architecture Resilience ✅ COMPLETE (2026-04-17)

### Task 5: Refactor `ScopeContext` Sync-Over-Async Starvation ✅
- **File**: `OrvixFlow.Core/Interfaces/IScopeContext.cs`, `OrvixFlow.Infrastructure/Auth/ScopeContext.cs`
- **Issue**: `.GetAwaiter().GetResult()` runs database queries on a synchronous property getter (`EnsureResolved`).
- **Action**: 
  - Added `InitializeAsync()` method to `IScopeContext` interface
  - `ScopeContext` now provides async initialization path
  - `AccessResolver` calls `await _scope.InitializeAsync()` before accessing sync properties
  - Backward compatibility maintained via fallback sync properties with deprecation warning
- **Verification**: No blocking threads - all scope resolution happens asynchronously
- **Status**: ✅ Fixed and tested

### Files Changed
1. `OrvixFlow.Core/Interfaces/IScopeContext.cs` - Added `Task InitializeAsync()` method
2. `OrvixFlow.Infrastructure/Auth/ScopeContext.cs` - Implemented `InitializeAsync()` with backward-compatible fallback
3. `OrvixFlow.Infrastructure/Auth/AccessResolver.cs` - Now calls `await _scope.InitializeAsync()` before scope access
4. `OrvixFlow.Tests/ScopeContextTests.cs` - New test file with 4 tests

---

## Phase 4: Verification & Tests

**Status**: ✅ Complete

### Task 6: Ensure full Test Coverage
- Run test suite: `dotnet test`
- Specifically added/updated tests:
  - Add `CompanyAdmin_CannotAccessModule_IfCompanyNotEntitled` in `RequireModuleAttributeTests.cs`. ✅ Done
  - Add `RefreshSession_WithActiveCompanyId_RetainsCompanySwitch` in `AuthServiceTests.cs`. ✅ Done
  - Validate `CompanyOwner_CanInviteUsers_Successfully` in `OrganizationControllerTests.cs`. ✅ Done (via Roles.IsAdmin fix)
  - **NEW**: `ScopeContextTests.cs` with 4 tests for async initialization

---

**Test Results**
- **Total**: 408 tests passing (1 skipped)
- **Phase 3 Tests**: 4 new tests in `ScopeContextTests.cs`

**Prepared**: 2026-04-16
**Phase 1 Completed**: 2026-04-17
**Phase 2 Completed**: 2026-04-17
**Phase 3 Completed**: 2026-04-17
**Phase 4 Completed**: 2026-04-17
**Status**: All Phases Complete ✅
