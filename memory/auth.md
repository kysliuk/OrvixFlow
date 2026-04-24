# Auth Memory

Last updated: 2026-04-24

This file is the auth-focused working memory for future agents. Read it before changing login, registration, JWT/session handling, company switching, invites, verification, or authorization filters.

## Core Model

- Backend auth lives in `OrvixFlow.Infrastructure/Auth/AuthService.cs`.
- API auth endpoints live in `OrvixFlow.Api/Controllers/AuthController.cs` and `OrvixFlow.Api/Controllers/InviteController.cs`.
- Frontend session state lives in `orvixflow-web/auth.ts` using NextAuth JWT strategy.
- Multi-company context is carried in JWT claims and mirrored into the frontend session.

## Non-Negotiable Role Rules

- `User.Role` is for global roles only.
- Valid global roles: `SuperAdmin`, `InternalOperator`, or empty string.
- Company roles live in `UserCompanyMembership.CompanyRole`.
- Valid company roles: `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer`.
- JWT `Role` claim contains the global role for platform admins, otherwise the active company role.
- Never write a company role into `User.Role`.

## Session Model

- Access token: backend JWT, currently 60 minutes.
- Refresh token: opaque DB-backed token, currently 7 days, rotated on refresh.
- Frontend stores the backend JWT in the NextAuth JWT/session.
- Session-producing endpoints should return the same shape:
  - `token`
  - `profile`
  - `refreshToken`

Current endpoints expected to follow that contract:
- `POST /api/auth/login`
- `POST /api/auth/oauth-provision`
- `POST /api/auth/refresh`
- `POST /api/auth/switch-company`
- `PUT /api/auth/profile`
- `POST /api/invite/accept`
- `POST /api/org`

## Active Company Rules

- Normal users must have an active `UserCompanyMembership` for the chosen company before a token is minted.
- `User.TenantId` is legacy/default preference data, not automatic authorization truth.
- `AuthService.ResolveActiveCompanyIdAsync` is the gate that decides whether a token may be minted for a company.
- Local login, refresh, and profile update now support an authenticated no-org session when no active non-archived company membership exists.
- In a no-org backend session, emitted auth payloads omit company scope: `TenantId` and `ActiveCompanyId` are null in the profile and absent from JWT claims.
- No-org backend sessions currently emit a safe fallback `Plan` of `Free` and an empty non-platform `Role` claim so company-scoped access checks fail closed.
- Existing OAuth-user sign-in now uses the same resolved session path as local login/refresh: archived default `TenantId` values are ignored in favor of an active non-archived membership, and archived-only users receive a no-org session.
- Frontend refresh must send `activeCompanyId` so switched-company context survives refresh.
- `orvixflow-web/auth.ts` now treats `tenantId` and `activeCompanyId` as nullable session fields and clears both when no active company exists; it must never derive `activeCompanyId` from `tenantId`.
- `OrganizationController.GetOrgStatus()` must reflect the JWT `ActiveCompanyId` first, not an arbitrary first membership.

## Registration And Verification

- Local registration creates a tenant in `Free` / `Active` state.
- Registration queues a verification email through `NotificationQueue`; it does not directly send mail from the auth flow.
- Outbound provider selection now lives behind `IEmailService` with `Console`, `Smtp`, and `Resend` options.
- Verification tokens are stored hashed in `User.VerificationToken`.
- Verification expiry is stored in `User.VerificationTokenExpiresAt`.
- Verification links currently expire after 48 hours.
- Verification compatibility path exists for legacy plaintext tokens still in the database; do not remove casually.
- `NotificationProcessorJob` must read `NotificationQueue` with `IgnoreQueryFilters()` because Hangfire has no request JWT tenant context.
- Notification delivery now uses queue lease/retry metadata (`AttemptCount`, `LastAttemptedAt`, `LastError`, `IsProcessing`, `ProcessingStartedAt`, `Failed`) to avoid overlapping sends and to preserve failure diagnostics.

Files to inspect before changing this area:
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Api/Jobs/NotificationProcessorJob.cs`
- `OrvixFlow.Core/Entities/User.cs`
- `OrvixFlow.Core/Entities/NotificationQueue.cs`
- `OrvixFlow.Infrastructure/Services/EmailOptions.cs`
- `OrvixFlow.Infrastructure/Services/ResendEmailService.cs`

## Invites

- Canonical invite path is `POST /api/invite` and `POST /api/invite/accept`.
- Legacy `POST /api/org/invite` is intentionally retired with `410 Gone`.
- Invite tokens are stored hashed in `Invitation.Token`.
- Invite delivery is durable via `NotificationQueue`.
- New local users accepting invites must provide a password that passes complexity validation.
- Invite acceptance marks the account verified.
- Compatibility path exists for old plaintext invite tokens; do not remove without data migration.

Files to inspect before changing this area:
- `OrvixFlow.Api/Controllers/InviteController.cs`
- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Core/Entities/Invitation.cs`
- `orvixflow-web/app/invite/page.tsx`

## Company Bootstrap

- Shared bootstrap logic now lives in `OrvixFlow.Infrastructure/Auth/CompanyBootstrapService.cs`.
- Use it whenever a new company is provisioned.
- Current bootstrap responsibilities:
  - owner company membership
  - default `general` department
  - owner department membership
  - default free subscription

Current callers:
- registration flow
- OAuth provisioning flow when creating/repairing owner bootstrap
- organization creation flow

Do not duplicate bootstrap logic in controllers or random services.

## Authorization Model

- Authentication is backend-enforced; frontend guards are defense-in-depth only.
- Module access is enforced by `RequireModuleAttribute`.
- Billing entitlement check happens before user-level permission checks.
- Platform admins bypass module checks.
- Company admins bypass user-level permission checks only after the company entitlement check passes.
- `RequireModuleAttribute` now fails closed when required user context or `IAccessResolver` is missing.
- Company-management role mutation is limited to company roles only. Platform roles must never enter `UserCompanyMembership.CompanyRole`.
- `CompanyOwner` assignment is restricted to bootstrap/platform-only flows. Normal invite and team-role update flows may assign `CompanyAdmin`, `DepartmentManager`, `Operator`, or `Viewer` only.
- Team management now includes three backend paths in `TeamController`:
  - `PUT /api/team/{userId}/role`
  - `DELETE /api/team/{userId}`
  - `PUT /api/team/{userId}/departments`
- Member removal is soft-deactivation via membership `Status`, not hard delete.
- Existing-member department assignment is a reconcile operation over the full set of department memberships for the active company.
- Invitation acceptance now revalidates stored role/department data before creating memberships so stale pending invites cannot bypass the role-layer separation.
- Company lifecycle now has a tenant-level archive state separate from billing state:
  - `Tenant.LifecycleStatus`
  - `Tenant.ArchivedAt`
  - `Tenant.ArchivedByUserId`
  - `Tenant.DeletionScheduledFor`
  - `Tenant.ArchiveReason`
- Company archive rules:
  - only `CompanyOwner`
  - only on `Free` plan
  - only when non-billable
  - typed company-name confirmation required
  - archive retains all company data for 60 days
  - admin can restore archived companies within retention via `POST /api/admin/companies/{id}/restore`
- Archived companies are excluded from normal company selection/profile building and cannot be switched into through normal auth flows.
- Archived company purge now runs as a recurring background job after the 60-day retention window.
- Purge behavior:
  - users whose `TenantId` points at the archived company are reassigned to another active company if they still have one
  - users with no remaining active company are deleted with the purged tenant
  - active refresh tokens for affected users are revoked during purge

Files to inspect before changing authz:
- `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- `OrvixFlow.Infrastructure/Auth/AccessResolver.cs`
- `OrvixFlow.Infrastructure/Auth/ScopeContext.cs`
- `OrvixFlow.Core/Authorization/Roles.cs`

## Tenant And Company Context

- Runtime tenant context is resolved by `OrvixFlow.Api/Services/TenantProvider.cs`.
- Normal requests should not rely on `X-Tenant-ID`.
- Admin impersonation is the only allowed exception path and must stay tightly controlled.
- Query-filter bypass with `IgnoreQueryFilters()` is only safe when the endpoint already enforces correct auth.

## Frontend Session Assumptions

- `orvixflow-web/auth.ts` is the highest-impact frontend auth file.
- NextAuth refresh logic must keep `apiToken`, `refreshToken`, and `activeCompanyId` aligned.
- Refresh failure should invalidate backend token state so protected routes bounce the user back to login.
- Middleware currently treats the user as logged in only if a usable backend `apiToken` exists.
- The invite accept flow uses a dedicated credentials provider named `invite-accept`.

## Common Mistakes To Avoid

- Do not compare `User.Role` against company-role values.
- Do not mint tokens from `User.TenantId` without validating active membership.
- Do not add a second invite path.
- Do not return inconsistent auth/session payload shapes from session-producing endpoints.
- Do not remove the compatibility path for legacy plaintext invite/verification tokens unless you also migrate old data.
- Do not bypass backend authorization because the frontend already hides UI.
- Do not reintroduce `X-Tenant-ID` as a normal runtime auth mechanism.
- Do not accept `CompanyOwner`, `SuperAdmin`, or `InternalOperator` in company invite/update-role flows.
- Do not update company role without also considering department-role sync for active department memberships.
- Do not treat `Tenant.Plan` or `SubscriptionStatus` as the company lifecycle source of truth for archived companies; use tenant archive fields.

## High-Impact Files

- `OrvixFlow.Infrastructure/Auth/AuthService.cs`
- `OrvixFlow.Infrastructure/Auth/CompanyBootstrapService.cs`
- `OrvixFlow.Api/Controllers/AuthController.cs`
- `OrvixFlow.Api/Controllers/InviteController.cs`
- `OrvixFlow.Api/Controllers/OrganizationController.cs`
- `OrvixFlow.Api/Filters/RequireModuleAttribute.cs`
- `OrvixFlow.Api/Services/TenantProvider.cs`
- `orvixflow-web/auth.ts`
- `orvixflow-web/middleware.ts`
- `orvixflow-web/app/invite/page.tsx`

## Must-Test Flows After Any Auth Change

- register -> verify -> login
- credentials login -> refresh
- OAuth login with existing local email conflict
- switch company -> refresh
- create organization -> session update -> org status
- invite send -> invite accept
- membership revoked / inactive membership blocks token minting
- admin route access for `SuperAdmin` and `InternalOperator`
- module access for company admin vs operator/viewer
- logout refresh-token revocation

## Known Remaining Follow-Ups

- Refresh tokens are still stored in plaintext at rest.
- There is logout for a single refresh token, but not a broader session-family revocation model.
- If the product later introduces a real trial model, tenant denormalization and default subscription provisioning must be updated together.
