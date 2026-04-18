# Plan 01 - Session And Token Hardening

Status: proposed
Priority: P0
Scope: large auth/session refactor

## Goal

Repair the access-token and refresh-token model so local login, token refresh, company switching, and membership revocation behave consistently and safely.

## Issues Covered

### Issue 1. Hardcoded seeded SuperAdmin credentials

Problem:
- `DbInitializer` seeds a verified `SuperAdmin` with predictable credentials.

Proposed fix:
- Remove static credential seeding from normal startup.
- Replace with one of:
  - env-driven one-time bootstrap user creation in non-production only, or
  - an explicit admin bootstrap command/job guarded by environment configuration.
- Prevent predictable password creation in production entirely.

Acceptance criteria:
- No known platform-admin password exists in source-controlled code.
- Production startup does not create a loginable SuperAdmin unless explicitly configured.

### Issue 2. Credential login does not return refresh token

Problem:
- `POST /api/auth/login` drops `refreshToken`, but frontend refresh logic depends on it.

Proposed fix:
- Return `refreshToken` from `AuthController.Login`.
- Verify `orvixflow-web/auth.ts` captures it for credentials users exactly as it already does for OAuth.

Acceptance criteria:
- Credentials login returns `token`, `profile`, and `refreshToken`.
- Credentials sessions can refresh before access token expiry.

### Issue 3. Refresh loses active company context

Problem:
- Backend supports `activeCompanyId`, frontend refresh omits it.

Proposed fix:
- Update NextAuth refresh request to send both `refreshToken` and current `activeCompanyId`.
- Keep backend membership validation logic already present.

Acceptance criteria:
- Multi-company user stays in selected company after refresh.
- Invalid or inactive company context falls back or fails according to chosen policy.

### Issue 4. Token minting still trusts legacy `User.TenantId`

Problem:
- Login and refresh can mint tokens for `User.TenantId` without proving the user still has active membership there.

Proposed fix:
- Introduce a shared method to resolve the active company safely:
  - platform admin: allow explicit context
  - normal user: require active membership for target company
  - if no valid active membership exists, auth should fail instead of minting a token from legacy fallback
- Treat `User.TenantId` as compatibility/default preference data only, not automatic authorization truth.

Acceptance criteria:
- Local login fails when the user has no active membership in the resolved company.
- Refresh does not silently mint tokens for revoked users.

### Issue 5. Rotated refresh tokens are not returned consistently

Problem:
- Service returns refreshed session material in some flows, but controllers drop the refresh token.

Proposed fix:
- Standardize auth/session response DTOs for:
  - login
  - oauth-provision
  - refresh
  - switch-company
  - update-profile
  - invite-accept if it remains session-producing
- Return refresh token whenever a new refresh token is created.

Acceptance criteria:
- All session-issuing endpoints return a consistent contract.

### Issue 6. No backend logout/revocation endpoint

Problem:
- Frontend logout clears NextAuth only; backend refresh tokens remain usable.

Proposed fix:
- Add `POST /api/auth/logout` that revokes the presented refresh token or token family.
- Optionally add `logout-all-sessions` later.

Acceptance criteria:
- Logging out invalidates the active refresh token.
- Reuse of a logged-out refresh token fails.

## Implementation Phases

### Phase 1. Remove predictable admin bootstrap
- Change startup bootstrap design
- Update config docs if needed
- Add tests around bootstrap behavior

### Phase 2. Standardize auth session responses
- Introduce shared session response shape
- Return refresh token from all token-issuing endpoints
- Update frontend parsing accordingly

### Phase 3. Safe active-company resolution
- Add shared active-company resolver in auth service
- Enforce active membership before minting tokens
- Remove unsafe `User.TenantId` fallback for normal users

### Phase 4. Logout and revocation
- Add logout endpoint
- Revoke refresh token on logout
- Update frontend sign-out path if needed

## Tests To Add Or Update

- `AuthControllerTests`
  - login returns refresh token
  - switch-company returns refresh token
  - profile update returns refresh token
- `AuthServiceTests`
  - login fails when default tenant membership is inactive
  - refresh preserves switched company when `activeCompanyId` is passed
  - refresh fails or safely resolves when no active memberships remain
  - logout revokes refresh token
- Frontend tests/e2e
  - credentials session refresh works
  - switched company survives refresh
  - logout invalidates session and refresh path
