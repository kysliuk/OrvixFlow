# Archived Org Auth And Company Switch Regression Plan

> **Historical Regression Plan**
> Parts of this document are stale and predate the current three-layer RBAC model and production audit sync.
> Use current code, `memory/auth.md`, and `tasks/production/current-state-audit.md` as the source of truth. Keep this file only as historical context for unresolved UX/QA follow-up ideas.

## Execution Status

**Done:**
- [x] Identified that NextAuth serialization rules already correctly handle profile merges during `switch-company`.
- [x] Fixed "Archived tenant as active" UI bug by including `LifecycleStatus` in backend `AdminController.ListTenants` and adding UI grayscale/Archived badges in `/admin/tenants/page.tsx`.
- [x] Fixed module access regression where users switching to valid un-owned organizations (e.g. as Operator) saw no modules. Added `IEntitlementResolver` implicitly into `AccessResolver.cs` so entitled modules grant default CanUse/CanView to Operators/Viewers even without explicit `ModulePermissionGrant` assignments.

**Pending:**
- [ ] Implement formal "No-Org Landing UI" instead of generic redirects.
- [ ] QA verification of session context switches across module edge cases.


## 1. Clarifying Questions, If Needed

No blocking questions.

One non-blocking clarification remains useful for execution:

1. What exact post-switch flow is currently broken in production or staging: sidebar modules, billing plan display, Inbox Guardian access, Knowledge Base access, organization admin actions, or another path?

## 2. Context Summary From Memory

Relevant current context from `memory/` and verified against code:

- Auth source of truth is `OrvixFlow.Infrastructure/Auth/AuthService.cs`; API auth surface is `OrvixFlow.Api/Controllers/AuthController.cs`; frontend session state is `orvixflow-web/auth.ts`.
- The system has a two-layer role model:
  - Global roles in `User.Role`: `SuperAdmin`, `InternalOperator`, or empty.
  - Company roles in `UserCompanyMembership.CompanyRole`: `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer`.
- Company context is supposed to be driven by active company claims and membership resolution, not blindly by `User.TenantId`.
- Archived companies are excluded from normal selection and cannot be switched into.
- `OrganizationController.GetOrgStatus()` already supports a no-organization result and prefers `ActiveCompanyId` when present.
- Frontend middleware treats a user as authenticated if a backend `apiToken` exists, not if a company exists.
- Memory explicitly warns:
  - do not mint tokens from `User.TenantId` without validating active membership
  - do not conflate global roles with company roles
  - refresh/session-producing endpoints must return the same contract
- Important memory/code mismatch:
  - `memory/auth.md` matches current code: refresh tokens exist and are rotated.
  - `memory/memory-security.md` is stale in this area and still says there is no refresh token.
- Important stale architecture assumption now visible in code:
  - current auth/session payload still collapses `TenantId` and `ActiveCompanyId` into the same value, which conflicts with the newer "legacy default vs active company" model.

## 3. Summary Of Archived Regression Plan

The archived plan identified three primary problems:

1. Archived-only users cannot log in.
   - Local login still requires a resolved active non-archived company.
   - OAuth existing-user paths still mint from `existing.TenantId` directly.

2. Company switch does not fully restore org/plan/module behavior.
   - Switch endpoint likely works, but downstream context propagation may be stale.

3. The system needs a supported no-org authenticated state.
   - Backend auth/session contract and frontend consumers must tolerate authenticated users with no active company.

It recommended:

1. Add no-org login/session support.
2. Fix OAuth existing-user company resolution.
3. Audit session consumers for nullable/no-org compatibility.
4. Reproduce and fix switched-company propagation.
5. Add regression coverage.

## 4. What Is Still Relevant Vs Outdated

### Still relevant

- Local login still fails if no active company is resolved.
  - `AuthService.LoginAsync` blocks on `ResolveActiveCompanyIdAsync(...)` and returns `"Your account does not have an active company membership."`
- OAuth existing-user paths still bypass active-company resolution.
  - `ProvisionOAuthUserAsync` still uses `existing.TenantId` and `byEmail.TenantId` directly.
- No-org authenticated session is still not supported.
  - `UserProfile.ActiveCompanyId` is non-nullable.
  - `BuildProfileAsync` requires an active company.
  - `auth.ts` forces `session.user.activeCompanyId = token.activeCompanyId || token.tenantId`.
  - `orvixflow-web/types/next-auth.d.ts` requires both `tenantId` and `activeCompanyId` as strings.
- The archived request for concrete switch repro is still valid. The codebase does not identify a single precise broken flow.

### Partially outdated

- The archived plan treated switched-company handling as broadly suspect on backend.
- Current code already fixed several backend pieces:
  - `SwitchCompanyAsync` rejects archived target companies.
  - `RefreshSessionAsync` preserves switched company when valid and falls back when invalid.
  - `OrganizationController.GetOrgStatus()` honors the active-company claim.
- So the backend switch foundation is better than the archived plan implied.

### Outdated or incomplete in the archived plan

- Making only `UserProfile.ActiveCompanyId` nullable is not enough.
- Current code shows a wider contract problem:
  - JWT currently sets both `TenantId` and `ActiveCompanyId` to the same value.
  - Profile currently sets both `TenantId` and `ActiveCompanyId` to the same value.
  - Frontend session and types assume both always exist.
- Any real no-org fix must address token claims, profile contract, NextAuth callbacks, and type definitions together.

## 5. Key Risks And Root-Cause Hypotheses

1. Root cause A: auth still requires a company-scoped session.
   - `LoginAsync`, `RefreshSessionAsync`, and `UpdateUserAsync` all fail when no active company resolves.

2. Root cause B: OAuth existing-user branches are stale.
   - `ProvisionOAuthUserAsync` still mints against `TenantId` directly instead of using the same resolution rules as the rest of auth.

3. Root cause C: current session model erases the distinction between default company and active company.
   - `MintJwtAsync` writes both `TenantId` and `ActiveCompanyId` as the same value.
   - `BuildProfileAsync` does the same.
   - This is the central structural reason the archived plan is too narrow.

4. Root cause D: no-org state is only partially implemented.
   - Organization status UI can represent "no org".
   - Auth/session plumbing cannot.

5. Root cause E: post-switch regression is still unverified.
   - Current code suggests many pages refetch on `apiToken` change, which should help.
   - The likely remaining issue is a specific stale consumer or a missing dependency on updated session state, but this must be reproduced before speculative fixes.

6. Security/architecture risk:
   - No-org tokens must not carry an archived company as effective request scope.
   - If `TenantId` remains populated with an archived/default company in a no-org session, tenant scoping could become inconsistent or unsafe.

## 6. Detailed Step-By-Step Execution Plan

### Step 1. Freeze The Session Contract Before Editing

**Resolved contract decision**

- Step 1 is now locked. The execution agent should treat the following as the implementation contract for all later steps.
- Session states:
  - Normal session: user is authenticated and has a valid active non-archived company membership.
  - Switched-company session: user is authenticated and scoped to a valid non-default active non-archived company membership selected through switch or refresh.
  - No-org session: user is authenticated but has no active non-archived company membership.
- Claim and profile semantics:
  - `ActiveCompanyId` means the effective company scope for the current session only.
  - `TenantId` must not be used as an implicit fallback for current request scope when `ActiveCompanyId` is missing.
  - For backward compatibility, when a valid active company exists, `TenantId` may remain aligned with the current scoped company in the emitted auth payloads during this remediation.
  - When no active company exists, `ActiveCompanyId` must be null or absent in the backend profile, JWT-derived frontend session, and any session refresh response.
  - When no active company exists, `TenantId` must also be null or absent in emitted auth payloads so the session does not silently regain company scope.
- Safety rules:
  - Archived companies must never be emitted as active runtime scope.
  - No fake, empty, sentinel, or synthetic company identifiers are allowed.
  - No controller, provider, or frontend consumer may manufacture `activeCompanyId` from `tenantId` in no-org state.
- Implementation consequence:
  - Any backend or frontend code path that currently assumes `TenantId == ActiveCompanyId` must be treated as legacy behavior to be updated in Steps 2 through 5.

**Goal**

- Define the exact backend and frontend meaning of `TenantId` and `ActiveCompanyId` for normal, switched, and no-org sessions.

**Exact scope**

- Auth contracts only.

**Affected areas/files likely involved**

- `OrvixFlow.Core/Interfaces/IAuthService.cs`
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `orvixflow-web/auth.ts`
- `orvixflow-web/types/next-auth.d.ts`

**Implementation instructions**

- Use the resolved contract decision above as the source of truth.
- Do not change the contract again during implementation unless a security issue or an unavoidable hard dependency is discovered.
- If a downstream consumer cannot tolerate nullable company scope, fix the consumer instead of weakening the contract.

**Architecture/security constraints**

- Runtime scope must never point at an archived company.
- Do not introduce a fake or sentinel company ID.

**Tests to add or update**

- None yet; this is the contract checkpoint.

**Validation checklist**

- No-org session semantics are explicit.
- `TenantId` and `ActiveCompanyId` are both treated as nullable or absent in no-org session output.
- The execution agent has a single canonical rule: no active company means no emitted company scope.

**Completion criteria**

- This file contains the locked contract decision for later implementation steps.

**Checkpoint**

- Stop if the no-org claim/profile shape is still ambiguous.

### Step 2. Add Backend Support For No-Org Auth Sessions

**Goal**

- Allow successful authentication and refresh without an active company.

**Exact scope**

- Local login, refresh, and profile update paths.

**Affected areas/files likely involved**

- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Core/Interfaces/IAuthService.cs`
- `OrvixFlow.Api/Controllers/AuthController.cs`

**Implementation instructions**

- Refactor auth session construction behind one internal path that can build:
  - company-scoped session
  - no-org session
- Change `UserProfile` to allow missing active company context.
- Update `LoginAsync`:
  - if active company resolves, return normal session
  - if not, return authenticated no-org session instead of failure
- Update `RefreshSessionAsync` to preserve no-org state instead of failing.
- Update `UpdateUserAsync` so profile edits still work in no-org state.

**Architecture/security constraints**

- No-org session must not authorize company-scoped access.
- Keep endpoint response shape consistent: `token`, `profile`, `refreshToken`.

**Tests to add or update**

- `AuthServiceTests`
  - local login succeeds for archived-only/no-active-company user
  - refresh succeeds and preserves no-org state
  - update profile succeeds and preserves no-org state
- `AuthControllerTests`
  - login returns `200` with no-org profile instead of `401`

**Validation checklist**

- No-org login returns token + profile + refreshToken.
- Resulting profile has no active company.
- Refresh rotation still works.

**Completion criteria**

- Backend auth can mint and return a safe no-org session.

### Step 3. Fix OAuth Existing-User Resolution To Match Backend Rules

**Goal**

- Make OAuth existing-user sign-in use the same active-company resolution/no-org behavior as local login.

**Exact scope**

- Existing OAuth account branch and provider-migration-by-email branch.

**Affected areas/files likely involved**

- `OrvixFlow.Infrastructure/Auth/AuthService.cs`

**Implementation instructions**

- Remove direct `MintJwtAsync(existing, existing.TenantId)` and `BuildProfileAsync(existing, existing.TenantId)` usage.
- Route both existing-user branches through the same shared session-construction helper from Step 2.
- If active non-archived membership exists, use it.
- If none exists, return no-org session.

**Architecture/security constraints**

- Preserve strict OAuth account-linking behavior.
- Do not reintroduce silent cross-provider linking.

**Tests to add or update**

- `AuthServiceTests`
  - OAuth existing user with archived default tenant and another active membership signs in using active company
  - OAuth archived-only user signs in to no-org session

**Validation checklist**

- OAuth no longer depends on archived `TenantId`.
- No archived company is minted into JWT.

**Completion criteria**

- All existing-user OAuth paths use one canonical company-resolution path.

### Step 4. Make Frontend Session State Nullable-Aware

**Goal**

- Stop the frontend from manufacturing an active company when none exists.

**Exact scope**

- NextAuth JWT/session callbacks and TypeScript types.

**Affected areas/files likely involved**

- `orvixflow-web/auth.ts`
- `orvixflow-web/types/next-auth.d.ts`

**Implementation instructions**

- Make `tenantId` and `activeCompanyId` nullable or optional in session and user types.
- Remove the fallback that sets `session.user.activeCompanyId` from `tenantId`.
- Ensure `jwt` callback preserves `null` or `undefined` no-org values during:
  - initial login
  - OAuth sign-in
  - `update(data)`
  - refresh
  - refresh failure

**Architecture/security constraints**

- Do not silently backfill active company from legacy fields.
- Keep middleware auth check based on `apiToken`, not active company presence.

**Tests to add or update**

- Frontend auth unit tests if present; otherwise add focused tests around `auth.ts` callback behavior.
- Cover:
  - no-org session restore
  - refresh preserving no-org
  - stale or invalid refresh clears auth state

**Validation checklist**

- A no-org backend payload stays no-org in NextAuth session.
- Session restore does not invent `activeCompanyId`.

**Completion criteria**

- Frontend session object can represent authenticated/no-org state without type hacks.

**Checkpoint**

- Verify no-org session survives a full refresh cycle before touching UI consumers.

### Step 5. Audit And Patch No-Org UI And Route Consumers

**Goal**

- Ensure authenticated no-org users land in safe UI states instead of broken requests or misleading errors.

**Exact scope**

- Dashboard-level pages and gates that assume company context.

**Affected areas/files likely involved**

- `orvixflow-web/app/(dashboard)/organization/page.tsx`
- `orvixflow-web/app/(dashboard)/layout.tsx`
- `orvixflow-web/components/module-gate.tsx`
- `orvixflow-web/app/(dashboard)/billing/page.tsx`
- `orvixflow-web/app/(dashboard)/settings/billing/page.tsx`
- possibly `orvixflow-web/app/(dashboard)/page.tsx`

**Implementation instructions**

- Keep account-level pages working: profile and settings.
- For org-scoped pages and components:
  - if no `activeCompanyId`, render explicit no-org or restricted state
  - do not make company-scoped fetches
- In `module-gate.tsx`, short-circuit cleanly when there is no active company.
- In billing pages, replace generic error display with no-org messaging if company context is absent.
- Preserve current organization page no-org affordances.

**Architecture/security constraints**

- Frontend guards are UX only; backend remains the authority.
- Prefer small conditional guards over broad rewrites.

**Tests to add or update**

- Organization page test for authenticated no-org state
- Module gate test for no active company
- Billing or settings-billing test for no-org safe state

**Validation checklist**

- No-org user can log in and view account pages.
- Company pages do not crash or loop.
- Organization page shows create/join affordance.

**Completion criteria**

- No-org state is first-class in the dashboard UX.

### Step 6. Reproduce The Post-Switch Regression Before Fixing It

**Goal**

- Convert the vague switch regression into one or more exact failing flows.

**Exact scope**

- Reproduction only before implementation.

**Affected areas/files likely involved**

- same frontend files as above
- `ModulesController.cs`
- `BillingController.cs`
- `OrganizationController.cs`

**Implementation instructions**

- Use a user with at least two active companies with different roles, plans, or modules.
- Verify these flows immediately after switch:
  - sidebar visible modules
  - billing subscription and plan
  - module-gated page access
  - organization page role and actions
- Record which one fails, and whether failure is:
  - stale UI state
  - stale session fields
  - backend response still scoped to old company

**Architecture/security constraints**

- Do not patch based on guesswork.

**Tests to add or update**

- None before repro is confirmed.

**Validation checklist**

- There is an exact failing scenario with expected vs actual outcome.

**Completion criteria**

- Execution agent has a concrete repro or explicitly reports that no repro was found.

**Checkpoint**

- If no precise repro exists after inspection or testing, stop and report instead of speculative edits.

### Step 7. Apply Minimal Fixes For Confirmed Post-Switch Staleness

**Goal**

- Fix only the confirmed switched-company regressions.

**Exact scope**

- Targeted frontend refetch or state propagation, and backend only if repro proves server-side scoping is wrong.

**Affected areas/files likely involved**

- `orvixflow-web/app/(dashboard)/layout.tsx`
- `orvixflow-web/components/module-gate.tsx`
- whichever page reproduces the stale behavior

**Implementation instructions**

- Prefer targeted fixes such as:
  - dependency arrays using `session?.apiToken` and `session?.user?.activeCompanyId`
  - clearing stale local state on company change
  - refetch after successful `update(data)`
  - `router.refresh()` only if needed and justified
- Do not rewrite session architecture a second time here.

**Architecture/security constraints**

- Keep backend scoping on claims; do not reintroduce request-driven tenant overrides.

**Tests to add or update**

- Page or component regression test for the exact broken flow
- Extend any existing auth/session test covering switch plus refresh

**Validation checklist**

- Switched company is reflected without full logout/login.
- Plan, module, and organization role data are from the new company.

**Completion criteria**

- The reproduced switch regression is fixed and covered.

### Step 8. Regression And Safety Sweep

**Goal**

- Verify the fix set end-to-end without broad collateral damage.

**Exact scope**

- Backend, frontend, auth/session, organization context.

**Affected areas/files likely involved**

- test suites only

**Implementation instructions**

- Run backend auth and org tests first, then frontend tests.
- Re-run targeted flows manually if UI tests are limited.

**Architecture/security constraints**

- Preserve archived-company protections.
- Preserve global-vs-company role separation.

**Tests to add or update**

- See section 7 below.

**Validation checklist**

- Archived companies still cannot be switched into.
- No-org users cannot access company-scoped endpoints.
- Platform admins still authenticate correctly.

**Completion criteria**

- All targeted tests pass and no security regressions are introduced.

## 7. Tests To Add Or Update

### Backend

- `AuthServiceTests.cs`
  - local login succeeds for archived-only user and returns no-org session
  - refresh session preserves no-org state
  - update profile preserves no-org state
  - OAuth existing-user login resolves active non-archived company when `User.TenantId` is archived
  - OAuth archived-only user gets no-org session
  - archived company cannot be switched into
  - platform admin login behavior remains valid if relevant to no-org handling

- `AuthControllerTests.cs`
  - login returns `200` with no-org payload
  - refresh returns `200` with no-org payload
  - stale or invalid refresh still returns `401`

- `OrgHierarchyTests.cs` or dedicated org controller tests
  - authenticated no-org user gets `hasOrganization = false`
  - authenticated no-org user cannot create department
  - platform admin org-status behavior remains correct if no active company is present

### Frontend

- Add or update tests around `orvixflow-web/auth.ts`
  - no-org login/session restore preserves null `activeCompanyId`
  - refresh preserves no-org state
  - invalid refresh clears token state

- Add page or component tests
  - `app/(dashboard)/organization/page.tsx`
    - authenticated no-org session shows safe general state
  - `components/module-gate.tsx`
    - no active company renders safe restricted state
  - `app/(dashboard)/billing/page.tsx` and or `settings/billing/page.tsx`
    - no active company shows no-org messaging instead of generic failure
  - exact switch-regression page once reproduced
    - session update after company switch refreshes module, plan, and org state

## 8. Validation Checklist

### Login and session restore

- Archived-only local user can log in.
- Archived-only OAuth user can log in.
- Session refresh preserves no-org state.
- Stale or invalid refresh clears auth state cleanly.

### Organization context

- No-org user gets `hasOrganization = false`.
- Organization page shows create/join affordance.
- No-org user cannot reach company-scoped mutations.

### Company switch

- Switch to valid active company succeeds.
- Switch to archived company fails.
- Switched company is reflected in:
  - visible modules
  - billing and subscription data
  - module-gated pages
  - organization role and actions

### Authorization

- Normal users never receive archived-company scope.
- Global roles vs company roles remain separate.
- No-org session does not grant company access.

### Admin and superadmin

- Platform admin auth still works.
- Admin route guard still works.
- No-org changes do not break platform-admin behavior if they have no active company.

### Safety

- Response contract remains consistent across session-producing endpoints.
- No `X-Tenant-ID` style fallback is reintroduced.
- Archived-company protections remain intact.

## 9. Stop Conditions And Handoff Notes For The Execution Agent

### Stop conditions

Stop and report immediately if any of these occur:

1. The no-org token or profile contract is still ambiguous after Step 1.
2. Supporting no-org state would require emitting archived-company scope into JWT claims.
3. A proposed fix mixes global roles and company roles.
4. The execution path would require broad rewrites across unrelated controllers.
5. Post-switch regression cannot be reproduced precisely after targeted investigation.
6. A change would weaken tenant isolation or archived-company blocking.

### Handoff notes

- Use code as source of truth over stale memory where they conflict.
- Treat `memory/auth.md` as more current than `memory/memory-security.md` for refresh-token behavior.
- The most important correction to the archived plan is this:
  - no-org support is not just `ActiveCompanyId` nullable;
  - the whole auth contract currently assumes `TenantId == ActiveCompanyId`, and that must be handled deliberately.
- Keep fixes minimal:
  - unify session construction in backend
  - make frontend session nullable-aware
  - reproduce switch issue before touching unrelated pages
