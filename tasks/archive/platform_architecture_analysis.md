# OrvixFlow - Platform Architecture & Authorization Analysis

> **Obsolete / Historical Analysis**
> Superseded by current code, `memory/auth.md`, `memory/memory-security.md`, and `tasks/production/current-state-audit.md` on 2026-06-11.
> This analysis predates the current RBAC model and should not be used as the source of truth for company and department roles.

## 1. Executive Summary
This report details the structural, security, and domain architecture of the OrvixFlow platform based on an in-depth code and memory analysis.
OrvixFlow employs a robust Clean Architecture in .NET 9 interacting with a Next.js 16 frontend. The platform operates as a multi-tenant B2B application featuring a hybrid role-based access control (RBAC) and entitlement-based gating system. It bridges traditional B2B SaaS features (company roles, subscription tiers, Stripe billing) with dense AI and orchestrator concepts (RAG, vector embeddings, n8n webhook triggers).

---

## 2. Current Tenancy Model
The multi-tenant architecture strictly enforces data isolation at the ORM layer using Entity Framework Core's Global Query Filters.

- **Tenant Representation:** Tenants are explicitly modeled via the `Tenant.cs` entity (often used interchangeably with "Company"). This serves as the top-level boundary for almost all other entities.
- **Data Isolation:** `AppDbContext` applies `.HasQueryFilter(e => e.CompanyId == _tenantProvider.GetTenantId())`. This guarantees that operations implicitly target the correct tenant data boundary without requiring explicit `Where` clauses in each service.
- **Tenant Resolution:** Determined exclusively through JWT claims (`ActiveCompanyId` or `TenantId`). To prevent horizontal escalation, HTTP header fallbacks (`X-Tenant-ID`) have been explicitly removed.
- **Admin Impersonation:** SuperAdmins can impersonate tenants, handled safely via an `X-Impersonate-Tenant` header that triggers mandatory, structured audit logging (`Warning` level: `"SECURITY: Admin impersonation started"`).

---

## 3. Current User Model
The identity model successfully decouples global platform authentication from tenant-specific authorization.

- **User Entity:** The `User` represents a global identity (identified by `Email`) capable of spanning multiple organizations.
- **Authentication:** Supports local (BCrypt password hash + email verification) and OAuth ("google", "microsoft"). OAuth uses an `ExternalId` subject claim. Account linking via email is securely prevented to avoid account takeover vectors.
- **Legacy Artifacts:** `User.TenantId` exists purely for backward compatibility but the modern, robust relationship operates via `UserCompanyMembership`.

---

## 4. Current Organization Structure
The platform utilizes a multi-level hierarchical structure for organizational scoping.

- **Organizations (Tenants/Companies):** The root container for data, billing, module assignments, and integrations.
- **User-Company Linkage:** Users belong to companies via `UserCompanyMembership`, enabling a user to be a part of multiple tenants seamlessly.
- **Departments:** Modeled via `Department.cs`, companies can subdivide their structure.
- **User-Department Linkage:** Users are assigned to departments via `UserDepartmentMembership`.
- **Data Scoping:** Resources like `ModuleAssignment` can be assigned globally to the Company, restrictively to a Department, or specifically to a User.

---

## 5. Current Roles and Permissions
The RBAC system has been notably refactored to separate platform-level control from tenant-level operations.

### Global Roles (Stored in `User.Role`)
Platform staff only. Normal users have an empty string. Evaluated globally.
- **SuperAdmin:** Full systemic control.
- **InternalOperator:** Platform support, read-only across tenants.

### Company Roles (Stored in `UserCompanyMembership.CompanyRole`)
Contextual roles based on the active tenant.
- **CompanyOwner:** Full control within their tenant.
- **CompanyAdmin:** Delegated management (cannot assign Owner).
- **DepartmentManager:** Visibility and control scoped to their assigned departments.
- **Operator:** Execution capabilities within allowed modules.
- **Viewer:** Read-only access within allowed modules.

### Permissions
Granular access control is heavily structured via **Module Permission Grants**. Access is not hardcoded but defined by `ModulePermissionGrant.cs` flags (e.g., `CanView`, `CanUse`, `CanTest`, `CanConfigure`).

---

## 6. Current Rules and Access Logic
Access logic is aggressively layered. For a user to execute an action on a module, two distinct hurdles must be cleared in the `RequireModuleAttribute`:

1. **Billing/Entitlement Gating:** The system completely blocks the request at the company level if the active `CompanySubscription` (via `EntitlementResolver`) does not include the requested module, irrespective of the user's role.
2. **Access Resolution (RBAC):** If the company has the module:
   - **Admins:** `SuperAdmin`, `InternalOperator`, `CompanyOwner`, and `CompanyAdmin` automatically bypass granular checks.
   - **Standard Users:** `AccessResolver` queries the union of `ModulePermissionGrants` spanning the User's explicit grants, Department grants, and Company grants. If they lack `CanUse`, a 403 is returned.

**Admin Cross-Tenant Operations:**
When accessing data globally (e.g., retrieving another company's subscription), services use `.IgnoreQueryFilters()`. This is deemed safe because the controllers invoke authorization policies like `IsSuperAdmin()`.

---

## 7. Current Billing / Plans / Pricing Strategy
The application employs an enterprise-grade multi-tier billing architecture with Stripe integration.

- **Plan Catalog:** Defined by `PlanTemplate.cs`, handling `Free`, `Starter`, `Pro`, etc. Evaluates monthly/yearly pricing with specific `StripePriceId` mapping.
- **Company Subscriptions:** Tracked via `CompanySubscription` (`SubscriptionState`: Trialing, Active, PastDue, Suspended, Cancelled). A background job (`TrialExpirationJob`) automatically enforces free-tier downgrade logic post-trial.
- **Plan Entitlements:** `PlanEntitlements.cs` caps tokens, API requests, storage (MB), Knowledge Bases, and Mailbox connections.
- **Metering & Limits:** `UsageEvent` captures metrics (e.g., `AiTokens`, `StorageMb`). The `EntitlementResolver` sums usage across the current billing period to evaluate limits in real time.
- **Overrides:** The system exceptionally supports manual B2B negotiation through `CompanyEntitlementOverride` and `CompanyModuleOverride` which forcefully adjust default Plan limits.

---

## 8. Implemented vs Partial vs Missing
Based on the exact state of the codebase:

- **Implemented:**
  - Multi-tenancy with strict EF Core Global Query Filters.
  - Hybrid Role architecture accurately segregating platform roles (`User.Role`) and tenant roles (`UserCompanyMembership.CompanyRole`).
  - Strict OAuth provisioning and duplicate account prevention.
  - Dynamic Entitlement Engine (`EntitlementResolver`) and Limit-checking logic.
  - Subscription trial expiration background hooks.
  - Two-stage `RequireModule` gating.
- **Partial / Legacy Transitions:**
  - The `User.TenantId` property remains in the entity model. Though labeled "Legacy", it presents a minor architectural residue.
- **Missing / Unverified:**
  - While "Stripe" properties exist on models (`ExternalCustomerId`, `ExternalSubscriptionId`), no dedicated Stripe Webhook processor was observed in the immediate domain layer scope during this inspection.

---

## 9. Key Inconsistencies and Risks
- **Admin Query Filter Blindspots:** Admin-facing data updates strictly require the use of `.IgnoreQueryFilters()`. If an engineer forgets this on a new repository method, it will cause silent 404s or Unique Constraint Violations (if fallback inserts trigger).
- **Navigation Include Failures with Soft-Data:** Using `Include()` during validations on EF InMemory databases causes total query failure when the inner related entity is missing (e.g., testing OAuth user creation before setting Tenant).
- **Role Assignment Subversion Mitigation:** Fixed recently, the codebase relies on string comparisons (`CompanyOwner` vs `Operator`), backed closely by `UserRoleExtensions.IsHigherThan()` logic to ensure users do not elevate themselves. If the enum integer mappings change unexpectedly, role assignment logic breaks.

---

## 10. Final Assessment
OrvixFlow features a mature, highly resilient backend tailored for a B2B SaaS platform. Its implementation of Data Isolation, Layered Role Based Access Control, and metered Plan Entitlements is robust, production-ready, and aligns perfectly with modern .NET 9 ASP.NET Core practices. The structural delineation of "Platform Authority" versus "Tenant Context" addresses one of the most perilous security pitfalls in generic SaaS development securely.
