# Plan 04 - Small Fixes And Cleanup

Status: proposed
Priority: P1-P2
Scope: smaller changes grouped into short phases

## Phase 1 - Auth Error Mapping

### Issue 1. Registration maps all service failures to `409`

Problem:
- Password policy and bad input failures are not conflicts.

Proposed fix:
- Return `400 BadRequest` for validation failures.
- Return `409 Conflict` only for duplicate-email conflicts.
- Introduce typed auth error/result categories if needed.

### Issue 2. OAuth provision maps business errors to `500`

Problem:
- Account-link conflicts and user-facing auth problems should not surface as server errors.

Proposed fix:
- Return `400` or `409` for expected auth/business failures.
- Reserve `500` for truly unexpected exceptions.

## Phase 2 - Fail-Closed Tightening

### Issue 3. `RequireModuleAttribute` can fail open in some missing-service or missing-claim cases

Problem:
- If user-level permission dependencies are missing, the filter may not deny access deterministically.

Proposed fix:
- Require valid `sub` and `IAccessResolver` when user-level permission evaluation is needed.
- Return `401`/`403`/`500` explicitly rather than implicitly allowing access.

### Issue 4. `/api/auth/me` may not resolve email claim correctly

Problem:
- JWT inbound claim mapping is cleared, but endpoint reads `ClaimTypes.Email` only.

Proposed fix:
- Read JWT `email` claim first, then fallback if needed.

## Phase 3 - Docs And Tooling Cleanup

### Issue 5. Swagger still documents `X-Tenant-ID`

Problem:
- Runtime path was removed, but API docs still suggest the old header.

Proposed fix:
- Remove the Swagger `X-Tenant-ID` security/header description.
- Replace with notes about JWT tenant resolution and admin impersonation where relevant.

### Issue 6. Trialing vs Free initial provisioning mismatch

Problem:
- Registration creates tenant marked `Trialing`, but free subscription bootstrap is created.

Proposed fix:
- Choose one consistent initial state.
- If trial is intended, create trial subscription and matching tenant denormalization.
- If free is intended, mark both as free/active.

## Phase 4 - Token Storage Hardening Follow-Up

### Issue 7. Refresh tokens are stored in plaintext

Problem:
- DB disclosure exposes reusable refresh tokens.

Proposed fix:
- Hash refresh tokens at rest.
- Store token fingerprint/identifier plus hash for lookup and rotation.

Note:
- This is smaller than the broader session contract work only if done after the response/rotation contract is stabilized.

## Tests To Add Or Update

- `AuthControllerTests`
  - correct status-code mapping for validation vs conflict
- `RequireModuleAttributeTests`
  - missing `sub` fails closed
  - missing resolver fails closed
- `AuthControllerTests` or endpoint tests
  - `/api/auth/me` returns expected email
- Provisioning tests
  - initial tenant/subscription status is internally consistent
