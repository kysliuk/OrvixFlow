# Auth Audit - 2026-04-18

This document preserves the source-based auth audit so future agents can investigate from a stable baseline.

## Scope Reviewed

- `memory/` docs
- `AGENTS.md`
- `tasks/ORVIXFLOW_AUTH_FIXES.md`
- Backend auth/authz code paths
- Frontend session/auth guard code paths
- Auth-related tests

## Current Auth Model

- Backend auth entry point: `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- API surface: `OrvixFlow.Api/Controllers/AuthController.cs`
- Frontend session layer: `orvixflow-web/auth.ts` with NextAuth JWT strategy
- Tenant scoping: JWT claims + `TenantProvider`
- Authorization: two-layer role system

### Role Model

- Global roles in `User.Role`: `SuperAdmin`, `InternalOperator`, or empty string
- Company roles in `UserCompanyMembership.CompanyRole`: `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer`
- JWT `Role` claim contains the global role for platform admins, otherwise the active company role

### Session Model

- Access token: backend JWT, 60-minute lifetime
- Refresh token: DB-backed opaque token, 7-day lifetime, rotated on refresh
- Frontend stores backend JWT inside NextAuth session state

## What Is Working

- Global-role vs company-role separation is implemented in `Roles.cs` and `MintJwtAsync`
- `X-Tenant-ID` fallback was removed from `TenantProvider`
- Admin impersonation is restricted to platform admins and logged
- Email verification gate exists for local login
- Password hashing uses bcrypt
- Password complexity exists for registration
- OAuth cross-provider auto-linking was blocked
- `RequireModuleAttribute` now checks company entitlement before user permission bypass
- `ScopeContext` has async initialization and `AccessResolver` uses it
- Frontend middleware guards `/admin` routes server-side

## Broken Or Incorrect Areas

### 1. Hardcoded seeded SuperAdmin credentials

Files:
- `OrvixFlow.Infrastructure/Data/DbInitializer.cs`

Issue:
- A verified `SuperAdmin` user is auto-seeded with predictable credentials.

Risk:
- Production-blocking if deployed as-is.

### 2. Credential login does not return refresh token

Files:
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `orvixflow-web/auth.ts`

Issue:
- Backend login returns only `token` and `profile`.
- Frontend expects `refreshToken` for session refresh logic.

Impact:
- Email/password sessions cannot refresh correctly.

### 3. Switched-company context is lost on refresh

Files:
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `orvixflow-web/auth.ts`

Issue:
- Backend supports refresh with `activeCompanyId`.
- Frontend refresh request sends only `refreshToken`.

Impact:
- Multi-company users can silently fall back to default company after token refresh.

### 4. Login and refresh still trust legacy default tenant too much

Files:
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`

Issue:
- Login defaults to `user.TenantId`.
- Refresh falls back to `user.TenantId`.
- JWT role selection can also fall back through `user.Role` path when no active membership row is found.

Impact:
- A user can still get a token for a legacy default tenant even when active membership state is no longer valid.

### 5. Invite flow claims delivery but does not send invitation email

Files:
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Api/Controllers/InviteController.cs`

Issue:
- Invite token is created and stored, but never emailed.
- API response says invitation was sent.

Impact:
- Invite workflow is incomplete and misleading.

### 6. Verification tokens never expire

Files:
- `OrvixFlow.Core/Entities/User.cs`
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`

Issue:
- `VerificationToken` exists but there is no expiry field or expiry check.

Impact:
- Stale verification links remain valid indefinitely.

### 7. Registration flow is non-transactional

Files:
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`

Issue:
- User and tenant are saved before email send and before all related setup completes.

Impact:
- Partial auth/account state can be persisted if email sending fails mid-flow.

### 8. Legacy organization invite path bypasses the hardened invite flow

Files:
- `OrvixFlow.Api/Controllers/OrganizationController.cs`

Issue:
- `OrganizationController.Invite` creates invited users/memberships directly and does not use the hardened invitation flow.

Impact:
- Duplicate invite logic, inconsistent behavior, regression risk.

### 9. Org status endpoint ignores current active company context

Files:
- `OrvixFlow.Api/Controllers/OrganizationController.cs`

Issue:
- `GetOrgStatus` returns the first active membership rather than current JWT company context.

Impact:
- Frontend can show wrong company context for multi-company users.

### 10. Create-organization contract does not match frontend session update behavior

Files:
- `OrvixFlow.Api/Controllers/OrganizationController.cs`
- `orvixflow-web/app/(dashboard)/settings/page.tsx`

Issue:
- Backend returns only company metadata.
- Frontend calls `update(data)` as if a token/profile payload was returned.

Impact:
- Session and active-company state can become inconsistent after org creation.

### 11. Auth error mapping is inconsistent

Files:
- `OrvixFlow.Api/Controllers/AuthController.cs`

Issue:
- Register maps all service failures to `409`.
- OAuth provision maps expected business errors to `500`.

Impact:
- Incorrect client behavior and poor diagnostics.

### 12. Invite acceptance allows weak or missing local passwords

Files:
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`

Issue:
- New invited local users may be created without a password, or with a password that bypasses complexity checks.

Impact:
- Inconsistent local-account security.

### 13. Several endpoints drop rotated refresh tokens

Files:
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Api/Controllers/InviteController.cs`

Issue:
- Service methods return refresh tokens for switch-company, profile update, and invite acceptance, but controllers do not return them.

Impact:
- Frontend session contract is incomplete and fragile.

## Risky Or Inconsistent Areas

- Backend JWT is still exposed to client-side JS via NextAuth session usage
- No explicit backend logout/revocation endpoint
- Refresh tokens and invite tokens are stored in plaintext
- Registration seeds tenant as `Trialing`, but initial subscription setup creates `Free`
- Swagger still documents `X-Tenant-ID` even though the runtime path was removed
- `RequireModuleAttribute` does not fail closed when permission services or `sub` claim are unavailable

## Recommended Fix Priority

1. Remove predictable seeded platform-admin credentials
2. Repair access-token/refresh-token contract end to end
3. Enforce active membership before minting tokens
4. Repair invite and verification flows
5. Repair multi-company context and frontend/backend session contracts
6. Clean up smaller auth inconsistencies and tighten fail-closed behavior

## Files Most Likely To Cause Auth Regressions

- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Api/Services/TenantProvider.cs`
- `OrvixFlow.Core/Authorization/Roles.cs`
- `OrvixFlow.Infrastructure/Auth/ScopeContext.cs`
- `OrvixFlow.Infrastructure/Auth/AccessResolver.cs`
- `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- `OrvixFlow.Infrastructure/Data/AppDbContext.cs`
- `orvixflow-web/auth.ts`
- `orvixflow-web/middleware.ts`

## Must-Test Flows After Any Auth Change

- Register -> verify -> login
- OAuth login with existing local email
- Login -> refresh
- Switch company -> refresh
- Invite send -> invite accept
- Membership revocation / inactive membership handling
- Admin route access for `SuperAdmin` and `InternalOperator`
- Module access for `CompanyAdmin`, `Operator`, and `Viewer`
