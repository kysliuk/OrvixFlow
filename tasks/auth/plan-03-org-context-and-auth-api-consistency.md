# Plan 03 - Org Context And Auth API Consistency

Status: proposed
Priority: P1
Scope: large multi-company and frontend/backend contract repair

## Goal

Make active-company state deterministic across backend JWTs, frontend session state, organization endpoints, and organization-creation flows.

## Issues Covered

### Issue 1. `GetOrgStatus` returns first membership instead of current active company

Problem:
- Endpoint ignores the current JWT active-company context.

Proposed fix:
- Resolve current company from `ActiveCompanyId` first.
- Validate that membership is active for that company.
- Return current active company info.
- Only fall back to another active membership if the active claim is absent and business rules explicitly allow that.

Acceptance criteria:
- Multi-company users see org status for the company they are currently operating in.

### Issue 2. Create-organization API contract does not match frontend session update logic

Problem:
- Frontend calls `update(data)` as though the response contains session material.
- Backend returns only company metadata.

Proposed fix:
- Choose one contract and make both sides match.

Preferred fix:
- `CreateOrganization` should delegate to auth/session issuance flow and return updated `token`, `profile`, and `refreshToken` if it is meant to switch current context immediately.

Alternative:
- Frontend should stop calling `update(data)` and instead explicitly re-fetch org state or call switch-company.

Acceptance criteria:
- Organization creation produces a correct, deterministic active-company state in both backend and frontend.

### Issue 3. Organization creation should provision consistent company defaults

Problem:
- Current org creation path creates tenant + membership only.
- It does not mirror auth provisioning behavior like default department and subscription setup.

Proposed fix:
- Reuse the same provisioning rules used by registration/OAuth owner bootstrap:
  - owner membership
  - default department
  - default subscription/plan model
- Avoid duplicate provisioning logic by extracting a shared company-bootstrap helper/service.

Acceptance criteria:
- Newly created orgs are fully provisioned and consistent with auth-created orgs.

### Issue 4. Frontend session refresh failure is not handled cleanly

Problem:
- NextAuth can keep an authenticated shell while backend API token is blank/invalid.

Proposed fix:
- In `orvixflow-web/auth.ts`, when refresh fails:
  - mark token/session as refresh-failed
  - force sign-out or route user through re-auth on next request
- Ensure protected UI does not continue using stale session state.

Acceptance criteria:
- Failed backend refresh causes a clean re-auth path instead of partially-authenticated UI.

## Implementation Phases

### Phase 1. Make org status use active-company context
- Update endpoint behavior
- Replace tests that currently lock in first-membership semantics

### Phase 2. Align create-org contract
- Pick canonical response contract
- Update frontend `settings/page.tsx`
- Add tests around session update behavior

### Phase 3. Share company bootstrap logic
- Extract bootstrap helper used by register, oauth provision, and create-organization
- Ensure default department/subscription behavior is centralized

### Phase 4. Frontend session-failure cleanup
- Improve NextAuth refresh failure handling
- Prevent stale session UX

## Tests To Add Or Update

- `OrgHierarchyTests`
  - org status returns JWT active company
  - org creation provisions expected defaults
- New controller/service tests
  - create-org returns expected contract
  - create-org central bootstrap path matches register/oauth bootstrap behavior
- Frontend tests/e2e
  - create org updates current active company correctly
  - refresh failure triggers re-auth rather than broken partial session
