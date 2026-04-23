# Archived Org Auth And Company Switch Regression Plan

## Objective

Document the current regressions introduced around archived organizations and company switching, and define the minimal production-safe fix plan.

## Confirmed Regressions

### 1. Archived-only users cannot log in

This affects both local email/password login and OAuth login.

#### Local login root cause

`OrvixFlow.Infrastructure/Auth/AuthService.cs`

- `LoginAsync()` still requires `ResolveActiveCompanyIdAsync(user, user.TenantId)` to return a valid active company.
- If the user's only company is archived, `ResolveActiveCompanyIdAsync()` returns `null`.
- `LoginAsync()` then returns:
  - `Your account does not have an active company membership.`

This blocks archived-only users from logging in even though the frontend now supports a no-org Organization state.

#### OAuth login root cause

`OrvixFlow.Infrastructure/Auth/AuthService.cs`

- Existing OAuth user paths still mint JWT/profile using `existing.TenantId` directly.
- They do not resolve the current active non-archived company first.
- If `existing.TenantId` points to an archived company, OAuth sign-in fails even when another active membership exists.

Affected paths:

- existing OAuth account branch
- email-matched migrated OAuth branch

### 2. Company switch does not fully restore org/plan functionality

Static inspection suggests the switch endpoint itself is mostly correct, but there is likely a follow-up context propagation bug after switching.

Observed symptom reported by user:

- after switching to another org, org functionality and/or plan-based functionality no longer behaves correctly

Current status:

- backend company-scoped module and billing filters do use `ActiveCompanyId` first
- frontend updates session on switch via `update(data)`
- likely remaining issue is stale/default-company assumptions in session consumers, layout module loading, or page refetch timing

This regression still needs reproduction against a precise broken flow.

## Relevant Code Paths

### Auth and session

- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Core/Interfaces/IAuthService.cs`
- `orvixflow-web/auth.ts`

### Company switch and org UI

- `orvixflow-web/app/(dashboard)/organization/page.tsx`
- `orvixflow-web/app/(dashboard)/layout.tsx`

### Module and plan enforcement

- `OrvixFlow.Api/Controllers/ModulesController.cs`
- `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- `OrvixFlow.Infrastructure/Auth/AccessResolver.cs`
- `OrvixFlow.Infrastructure/Services/EntitlementResolver.cs`
- `OrvixFlow.Api/Controllers/BillingController.cs`
- `orvixflow-web/components/module-gate.tsx`

## Root Cause Summary

### Root cause A: auth contract still assumes a valid active company is required to log in

The system now supports archived companies and no-org UI states, but auth still treats "no active company" as a fatal login condition.

### Root cause B: OAuth existing-user flow bypasses active-company resolution

OAuth paths reuse `TenantId` directly, which is no longer safe after introducing archived company lifecycle.

### Root cause C: switched-company state likely still leaks default-company assumptions

The stack now distinguishes:

- legacy default company: `User.TenantId`
- active company: JWT `ActiveCompanyId`

The switch flow updates active-company tokens, but some consumers likely still behave as if default tenant and active tenant are interchangeable.

## Recommended Fix Strategy

## Phase 1. Allow no-org login/session state

### Goal

Allow users with no active non-archived company to authenticate into a safe no-org session instead of being fully locked out.

### Required backend changes

Update auth/profile generation so login can succeed even when no active company membership exists.

Key change:

- stop treating missing active company as automatic login failure

Required design adjustment:

- support a no-org session/profile state

Implementation options:

1. make `UserProfile.ActiveCompanyId` nullable
2. or return a sentinel/no-company profile structure

Recommended:

- make active company nullable in the profile contract and handle that explicitly

This will require reviewing:

- `UserProfile`
- auth controller responses
- NextAuth token/session update logic
- frontend pages that currently assume active company always exists

### Validation

- archived-only local user can log in
- archived-only OAuth user can log in
- resulting session does not grant archived-company access
- no-org Organization page still works

## Phase 2. Fix OAuth existing-user company resolution

### Goal

Make existing OAuth sign-in use the same active-company resolution rules as local login.

### Required backend changes

In `ProvisionOAuthUserAsync()`:

- replace direct `MintJwtAsync(existing, existing.TenantId)` usage
- resolve the active company first using `ResolveActiveCompanyIdAsync()`
- if none exists, return the no-org session state from Phase 1

Apply the same change to:

- the existing OAuth account branch
- the legacy email-matched migration branch

### Validation

- user with archived default tenant plus another active company signs in successfully
- active company is resolved correctly
- archived tenant is not used for JWT minting

## Phase 3. Audit no-org compatibility across session consumers

### Goal

Ensure frontend and backend tolerate authenticated users with no active company.

### Areas to verify

- `orvixflow-web/auth.ts`
- dashboard layout
- billing page
- module-gated pages
- organization page
- any route that currently assumes `session.user.activeCompanyId` is always present

### Expected behavior

- account-level pages still function
- org-scoped pages show restricted/no-org states rather than breaking
- refresh token rotation still works in no-org state

## Phase 4. Fix switched-company context propagation

### Goal

Make sure plan/module/org functionality reflects the switched company consistently.

### Likely investigation points

- `orvixflow-web/app/(dashboard)/layout.tsx`
  - visible module fetch should react to active-company changes cleanly
- `orvixflow-web/components/module-gate.tsx`
  - permissions and limit checks should rerun after switch
- pages that fetch billing/module/org data on mount only
- any consumer relying on `tenantId` instead of `activeCompanyId`

### Likely fix pattern

- ensure session update after switch refreshes all dependent data
- ensure company-sensitive fetch effects depend on active company context, not just token existence
- remove any remaining reliance on legacy `tenantId` for runtime company context

### Validation

Verify at least these flows after switch:

- sidebar visible modules update correctly
- billing page shows switched company plan and entitlements
- Inbox Guardian access matches switched company plan
- Knowledge Base access matches switched company plan
- Organization actions match switched company role and plan

## Tests To Add Or Update

### Backend

- local login succeeds for archived-only user with no active company
- OAuth login succeeds for archived-only user with no active company
- OAuth existing-user login resolves a non-archived active company when default tenant is archived
- archived company still cannot be switched into
- refresh session preserves no-org state when appropriate

### Frontend

- no-org authenticated session renders safe Organization general state
- session update after company switch refreshes module visibility
- plan-gated pages respond correctly to switched-company context

## Risks

### 1. `UserProfile` contract change

If `ActiveCompanyId` becomes nullable, all consumers must be reviewed carefully.

### 2. Legacy `User.TenantId`

This remains a compatibility field and is still a source of ambiguity.

Long-term, runtime company context should come from:

- active JWT/company context
- active membership resolution

not from `User.TenantId`.

### 3. Partial fix risk

Fixing login without fixing switched-company context will still leave inconsistent user behavior.

## Recommended execution order

1. Implement no-org login/session support
2. Fix OAuth existing-user active-company resolution
3. Audit nullable/no-org session compatibility
4. Reproduce and fix switched-company plan/module regression
5. Add regression tests

## Open item needing concrete repro

The switched-company regression still needs a precise failing user flow to complete the fix efficiently.

Requested clarification from user:

- which exact functionality breaks after switching org?
  - sidebar module visibility
  - billing page/current plan
  - Inbox Guardian access
  - Knowledge Base access
  - Organization admin actions
  - other specific flow
