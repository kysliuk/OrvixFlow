# OrvixFlow — Super Admin, Plan/Module & Role System Design

> **Document type**: Architecture design + implementation spec  
> **Status**: Draft for review  
> **Based on**: Actual project state as of 2026-04-02  
> **Source of truth**: `memory/`, `OrvixFlow.Core/`, `OrvixFlow.Infrastructure/`

---

## 1. Current State Findings

### What Already Exists (and Works)

| Area | Status | Notes |
|---|---|---|
| `UserRole` enum | ✅ Exists | `Roles.cs` — clean 7-role enum with `IsPlatformAdmin()`, `IsCompanyAdmin()` helpers |
| `PlanTemplate` entity | ✅ Exists | Name, slug, price, seats, trial, lifecycle |
| `PlanEntitlements` entity | ✅ Exists | Tokens, API req/day, storage MB, KB count |
| `PlanModuleInclusion` entity | ✅ Exists | M:M join between plan and module |
| `ModuleDefinition` entity | ✅ Exists | Key, displayName, category, tier, isPremium |
| `ModuleAssignment` entity | ✅ Exists | Company/Dept/User-scoped module grants |
| `CompanySubscription` entity | ✅ Exists | Status, period, pending plan, external ID |
| `PlanCatalog` seed | ✅ Exists | 5 seeded plans (Free → Enterprise) with entitlements |
| `EntitlementResolver` service | ✅ Exists | Full limit-checking logic (`IsWithinTokenLimit`, etc.) |
| `CompanySubscriptionService` | ✅ Exists | Assign, change, suspend, reactivate, cancel |
| `PlansController` | ✅ Exists | SuperAdmin CRUD for plans + modules |
| `AdminController` | ✅ Exists | Company listing, usage, assign plan, suspend/reactivate |
| `AuditTrail` + `AuditService` | ✅ Exists | Full audit logging |
| `UsageEvent` + `UsageService` | ✅ Exists | Token, storage, KB usage tracking |
| `RequireModuleAttribute` filter | ✅ Exists | Gate on `module-key` |

### What Is Missing (Gaps)

| Area | Status | Priority |
|---|---|---|
| `CompanyEntitlementOverride` entity | ❌ Missing | **Phase 2** |
| `CompanyModuleOverride` entity | ❌ Missing | **Phase 2** |
| `UserModuleOverride` entity | ❌ Missing | **Phase 3 / optional** |
| Module-level per-limit overrides | ❌ Missing | **Phase 2** |
| `ModulesController` — full CRUD for modules | ⚠️ Partial | Phase 2 |
| `BillingHistory` table | ❌ Missing | Phase 3 |
| Trial expiration enforcement job | ❌ Missing | Phase 2 |
| Dunning/past-due flow | ❌ Missing | Phase 3 |
| InternalOperator role enforcement | ⚠️ Defined but not enforced | Phase 2 |
| Role-based module permission matrix (fine-grained) | ⚠️ Exists via `ModulePermissionGrant` but not consistently wired | Phase 2 |
| Super Admin UI pages (plans, modules, companies) | ⚠️ Exists `/admin/plans`, `/admin/companies/[id]` — needs expansion | Phase 2 |
| Override management UI | ❌ Missing | Phase 2 |
| `Tenant.Plan` field (string) duplicates `CompanySubscription` | ⚠️ Inconsistency — both exist, creates drift risk | Fix in migration |

### What Is Inconsistent (Design Debt)

1. **`Tenant.Plan` string vs `CompanySubscription.PlanTemplate`** — Two sources of truth for "what plan is this company on". `AdminController.GetGlobalMetrics()` reads `Tenant.Plan` directly (line 49). Danger of divergence.
2. **`UserRole` enum has `InternalOperator`** — referenced in `IsPlatformAdmin()` but has zero enforcement anywhere. Effectively unused.
3. **`ModuleAssignment` used for two distinct purposes**: (a) access grants from module ownership system, and (b) subscription-driven access from plan. These are conflated — needs clean separation.
4. **`PlanEntitlements` has no `MaxInboxMessages` or `MaxN8nNodes`** — tracked in `UsageEvent` but not capped.
5. **`CompanySubscription` has `BillingInterval` on the subscription** — but `PlanTemplate` also has it. Should be canonical on subscription.

---

## 2. Role System Design (Global vs Company)

### Design Principle

> **Global roles** = who you are on the platform  
> **Company roles** = what you can do inside a company  
> These MUST NEVER be mixed. A user carries exactly ONE global role, and ONE company role per company membership.

### Global Roles (Platform-Level)

Stored in `User.Role` and emitted as `Role` claim in JWT. These govern **cross-tenant, platform-wide** capabilities.

| Role | Enum Value | Who Has It | Can Do |
|---|---|---|---|
| `SuperAdmin` | 1 | OrvixFlow internal team | Everything. All companies, all plans, billing, modules. No tenant scope. |
| `InternalOperator` | 2 | OrvixFlow support staff | Read-only access to companies and usage. Cannot change billing or plans. |

**Boundaries for global roles:**
- Global roles bypass tenant query filters (`IgnoreQueryFilters()`)
- Global roles do NOT inherit company-level permissions
- `SuperAdmin` cannot "become" a company user — they operate above company scope
- `InternalOperator` is read-only everywhere

### Company Roles (Tenant-Level)

Stored in `UserCompanyMembership.Role`. Resolved from JWT claim when user is acting inside a company context.

| Role | Enum Value | Who Has It | Scope |
|---|---|---|---|
| `CompanyOwner` | 10 | First user of the company | Full company control |
| `CompanyAdmin` | 11 | Delegated admin | Member management, billing view |
| `DepartmentManager` | 20 | Department lead | Data within assigned dept(s) |
| `Operator` | 30 | Standard team member | Use modules per plan |
| `Viewer` | 31 | Read-only | View-only inside assigned modules |

**Boundaries for company roles:**
- Company roles NEVER see other tenants — EF query filters enforce this
- `CompanyOwner` has 1 per company (cannot be demoted, only transferred)
- `DepartmentManager` sees only their assigned department(s)
- `Operator` / `Viewer` see only modules enabled for their company via plan

### JWT Claim Design

```
sub: UserId (Guid)
email: user@domain.com
TenantId: Guid             -- active company ID (current session)
ActiveCompanyId: Guid      -- same as TenantId (explicit for clarity)
Role: "SuperAdmin"         -- Global platform role (canonical)
CompanyRole: "CompanyOwner" -- Company-level role in active company
DisplayName: "Jane Smith"
Plan: "Growth"             -- Plan slug for quick reads (non authoritative)
```

> **New field**: `CompanyRole` claim added to JWT. Currently only `Role` is emitted. This is required to cleanly separate global from company roles.

### Permission Matrix

| Action | SuperAdmin | InternalOp | CompanyOwner | CompanyAdmin | DeptManager | Operator | Viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Create/edit/archive plans | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Create/edit modules | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| View all companies | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Assign plan to company | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Override company entitlements | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Override company modules | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Suspend/reactivate company | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| View platform metrics | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| View audit log (any company) | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Invite members (own company) | ✅ | ❌ | ✅ | ✅ | ❌ | ❌ | ❌ |
| Change member role (own co) | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| View billing (own company) | — | — | ✅ | ✅ | ❌ | ❌ | ❌ |
| Upgrade/downgrade plan | — | — | ✅ | ❌ | ❌ | ❌ | ❌ |
| View own company audit log | — | — | ✅ | ✅ | ❌ | ❌ | ❌ |
| Use modules (per plan+role) | — | — | ✅ | ✅ | ✅ | ✅ | ✅ |
| Configure module settings | — | — | ✅ | ✅ | ❌ | ❌ | ❌ |
| View dept-scoped data | — | — | ✅ | ✅ | (own dept) | (assigned) | (assigned) |

---

## 3. Plan and Module System Design

### Plan Entity (`PlanTemplate`) — Current + Extensions

The existing `PlanTemplate` is solid. **Extensions needed:**

```csharp
// EXISTING — keep as-is:
public class PlanTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Slug { get; set; }         // unique, URL-safe
    public string Description { get; set; }
    public int MonthlyPriceCents { get; set; }
    public int YearlyPriceCents { get; set; }
    public string Currency { get; set; }
    public int? MaxSeats { get; set; }       // null = unlimited
    public bool IsActive { get; set; }
    public bool IsFree { get; set; }
    public bool IsTrialAllowed { get; set; }
    public int TrialDays { get; set; }
    public bool LegacyLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    // EXTEND: Add display ordering and public visibility flag
    public int SortOrder { get; set; } = 0;           // NEW
    public bool IsPubliclyVisible { get; set; } = true; // NEW — hides enterprise/custom from pricing page
}
```

**Plan State Machine:**
```
Draft (not yet wired up, exists in DB) 
  → Active (visible, assignable)
  → Inactive (no new signups, existing continue unchanged) 
  → Archived (soft-deleted, historical only)
  
LegacyLocked flag: prevents appearing in upgrade/downgrade lists
```

### Module Entity (`ModuleDefinition`) — Current + Extensions

```csharp
// EXISTING — keep as-is:
public class ModuleDefinition
{
    public Guid Id { get; set; }
    public string Key { get; set; }          // e.g., "inbox-guardian"
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }     // "AI", "Workflow", "Integration"
    public string Tier { get; set; }         // "Core", "Premium", "Enterprise"
    public string Visibility { get; set; }   // "UserFacing", "Internal", "Hidden"
    public bool IsOperational { get; set; }  // platform-internal (infra modules)
    public bool IsActive { get; set; }
    public bool IsPremium { get; set; }

    // EXTEND:
    public string? IconKey { get; set; }             // NEW — lucide icon name for UI
    public string? UpgradePromptText { get; set; }   // NEW — "Upgrade to Growth to unlock..."
    public int SortOrder { get; set; } = 0;          // NEW
}
```

### Module Limit System (NEW: `PlanModuleInclusion` + limits)

Currently `PlanModuleInclusion` is a flat join table. For modules with their own limits (e.g., inbox messages, n8n nodes), limits need to be per-module-per-plan:

```csharp
// EXTEND PlanModuleInclusion to support per-module limits:
public class PlanModuleInclusion
{
    public Guid Id { get; set; }
    public Guid PlanTemplateId { get; set; }
    public Guid ModuleDefinitionId { get; set; }
    public DateTime CreatedAt { get; set; }

    // NEW: per-module limits (null = no limit for this module in this plan)
    public int? MaxUsagePerMonth { get; set; }       // generic usage cap (e.g., inbox messages)
    public int? MaxItemsTotal { get; set; }          // e.g., max mailbox connections
    public string? LimitDescription { get; set; }    // human-readable, shown in UI

    public PlanTemplate PlanTemplate { get; set; } = null!;
    public ModuleDefinition ModuleDefinition { get; set; } = null!;
}
```

### Module Catalog (as currently seeded)

| Module Key | Display Name | Category | Included From |
|---|---|---|---|
| `inbox-guardian` | Inbox Guardian | AI | Free+ |
| `doc-intel` | Document Intelligence | AI | Starter+ |
| `lead-qualifier` | Lead Qualifier | AI | Growth+ |
| `finance-flow` | Finance Flow | AI | Growth+ |
| `legal-scribe` | Legal Scribe | AI | Business+ |
| `sop-generator` | SOP Generator | AI | Business+ |
| `data-guardian` | Data Guardian | AI | Enterprise |

### Can Modules Exist Outside Plans? (Add-ons)

**Yes, via `CompanyModuleOverride`** (Phase 2 entity, designed below). This allows:
- Super Admin grants a module to a specific company not in their plan
- Super Admin suppresses a module from a company that their plan includes

Add-ons at company level = override. User-level add-ons are NOT recommended for MVP.

### Plan Versioning Strategy

- Plans are **immutable once `LegacyLocked = true`**
- Changing a plan's entitlements affects ALL companies on that plan — use with care
- For company-specific changes → use `CompanyEntitlementOverride` (not plan edits)
- When a plan needs significant restructuring: create a new plan, migrate companies, archive old one
- Migration: audit log entry written at every plan assignment change

### Backward Compatibility

- `EntitlementResolver` reads overrides FIRST, then falls back to plan defaults
- Plan archiving sets `ArchivedAt` but never deletes — `CompanySubscription` FK survives
- Existing subscriptions are never auto-downgraded by plan changes

---

## 4. Free Plan Proposal

### Composition

| Entitlement | Free Limit | Rationale |
|---|---|---|
| **Seats** | 2 | Solo + 1 collaborator |
| **Monthly AI Tokens** | 50,000 | ~50 email drafts |
| **API Requests/Day** | 100 | Light usage |
| **Storage** | 100 MB | 1-2 documents |
| **Knowledge Bases** | 1 | Single KB |
| **Inbox messages/month** | 50 | Light inbox automation |
| **Mailbox connections** | 1 | One email account |

### Included Modules

- `inbox-guardian`: ✅ (with limits: 50 processed messages/month)
- All other modules: ❌ (upgrade prompt shown)

### Restrictions

- No trial — Free IS the trial experience
- No `DraftFeedback` learning loop (premium feature)
- No custom `AgentPersona` configuration
- No n8n workflow provisioning (n8n automation is Growth+)
- Branding watermark in email drafts (optional UX decision)

### Upgrade Triggers

- Seat limit reached → invite blocked → "Upgrade to Starter"
- Token limit at 80% → warning banner → "You have 20% of tokens left"
- Token limit at 100% → hard block → "Upgrade to continue this month"
- Trying to enable a premium module → modal → "This module requires Growth plan"

### Abuse Prevention

- Hard seat cap of 2 (no grace)
- Rate limiting on `/api/v1/knowledge/upload` (already implemented)
- `IsWithinTokenLimitAsync()` checked before every AI operation
- One free company per user email domain (anti-abuse on company creation)

---

## 5. Super Admin Capabilities

### What Super Admin Can Do

| Capability | API Endpoint | Status |
|---|---|---|
| List all companies | `GET /api/admin/companies` | ✅ Exists |
| Get company detail + subscription | `GET /api/admin/companies/{id}` | ✅ Exists |
| Get company usage | `GET /api/admin/companies/{id}/usage` | ✅ Exists |
| Assign plan to company | `PUT /api/admin/companies/{id}/plan` | ✅ Exists |
| Suspend company | `POST /api/admin/companies/{id}/suspend` | ✅ Exists |
| Reactivate company | `POST /api/admin/companies/{id}/reactivate` | ✅ Exists |
| Global platform metrics | `GET /api/admin/metrics` | ✅ Exists |
| List all plans | `GET /api/plans` | ✅ Exists |
| Create plan | `POST /api/plans` | ✅ Exists |
| Edit plan | `PUT /api/plans/{id}` | ✅ Exists |
| Archive plan | `POST /api/plans/{id}/archive` | ✅ Exists |
| Add module to plan | `POST /api/plans/{id}/modules/{moduleId}` | ✅ Exists |
| Remove module from plan | `DELETE /api/plans/{id}/modules/{moduleId}` | ✅ Exists |
| Set plan entitlements | `PUT /api/plans/{id}/entitlements` | ✅ Exists |
| **Override company entitlements** | `PUT /api/admin/companies/{id}/entitlements` | ❌ Missing |
| **Add module override for company** | `POST /api/admin/companies/{id}/modules` | ❌ Missing |
| **Remove module override** | `DELETE /api/admin/companies/{id}/modules/{moduleId}` | ❌ Missing |
| **List modules + CRUD** | `GET/POST/PUT /api/admin/modules` | ⚠️ Partial |
| **Cancel subscription** | `POST /api/admin/companies/{id}/cancel` | ❌ Missing |
| **View audit log for company** | `GET /api/admin/companies/{id}/audit` | ❌ Missing |

### Permission Enforcement Pattern

All `AdminController` methods use the existing pattern:

```csharp
private bool IsSuperAdmin() => CurrentUserRole() == UserRole.SuperAdmin;

// For read-only admin endpoints, also allow InternalOperator:
private bool IsGlobalAdmin() => CurrentUserRole().IsPlatformAdmin();
```

A cleaner approach for Phase 2: introduce `[Authorize(Policy = "SuperAdminOnly")]` and `[Authorize(Policy = "PlatformAdmin")]` policies registered in `Program.cs`.

---

## 6. Assignment Model and Override Hierarchy

### Override Precedence (top wins)

```
UserModuleOverride      (Phase 3 — optional, avoid at MVP)
      ↓
CompanyModuleOverride   (Phase 2 — company-level add/suppress)
      ↓
PlanModuleInclusion     (plan default — what the plan includes)
```

For entitlements:

```
CompanyEntitlementOverride   (Phase 2 — custom limit for this company)
      ↓
PlanEntitlements             (plan default limits)
```

### Access Resolution Algorithm (full)

```csharp
// Called by EntitlementResolver — single authority
async Task<EffectiveAccess> ResolveAccessAsync(Guid companyId, Guid? userId)
{
    // 1. Get active subscription + plan modules
    var plan = await GetActivePlanAsync(companyId);
    var planModules = plan.ModuleInclusions.Select(m => m.ModuleDefinitionId).ToHashSet();

    // 2. Apply company module overrides (Phase 2)
    var companyOverrides = await GetCompanyModuleOverridesAsync(companyId);
    foreach (var o in companyOverrides)
    {
        if (o.IsEnabled) planModules.Add(o.ModuleDefinitionId);   // grant
        else planModules.Remove(o.ModuleDefinitionId);             // suppress
    }

    // 3. Apply user module overrides (Phase 3, skip at MVP)
    // var userOverrides = await GetUserModuleOverridesAsync(userId);

    // 4. Get effective entitlements
    var entitlements = await GetCompanyEntitlementsWithOverridesAsync(companyId);

    return new EffectiveAccess(planModules, entitlements);
}
```

### Plan Assignment Lifecycle

```
[New Company Created]
  → Auto-assign: Starter trial (14 days)
  OR → Auto-assign: Free (no trial)
  
[Super Admin assigns plan]
  → Validates: seat count <= plan.MaxSeats
  → Updates CompanySubscription.PlanTemplateId
  → Sets status = Active (or Trialing if trial allowed)
  → Writes AuditTrail entry
  → Clears any PendingPlanId
  
[Company Owner upgrades] (self-serve — Phase 2)
  → Upgrade: immediate, prorate charge calculated
  → Downgrade: queued to CurrentPeriodEnd
  → Sets PendingPlanId + PendingChangeAt
```

### Module Inheritance by Users in a Company

A user inside a company inherits:
1. All modules from company's active plan (via `PlanModuleInclusion`)
2. Plus any modules granted via `CompanyModuleOverride.IsEnabled = true`
3. Minus any modules suppressed via `CompanyModuleOverride.IsEnabled = false`

Then, role-based restrictions apply on top:
- `ModulePermissionGrant` can restrict specific operations per module per scope
- Example: module `audit-logs` is in plan, but `CanView = true` only for `CompanyAdmin` and above

---

## 7. Full Data Model

### New Entities Required (Phase 2)

```csharp
// --- CompanyEntitlementOverride ---
// Per-company custom limits that supersede plan defaults.
// Any field = null means "use plan default".
public class CompanyEntitlementOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }

    // Override fields — null = no override for this dimension
    public int? MaxSeats { get; set; }
    public int? MaxMonthlyTokens { get; set; }
    public int? MaxApiRequestsPerDay { get; set; }
    public int? MaxStorageMb { get; set; }
    public int? MaxKnowledgeBases { get; set; }
    public int? MaxInboxMessages { get; set; }    // extension
    public int? MaxMailboxConnections { get; set; } // extension

    public string Note { get; set; } = string.Empty; // reason for override
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Company { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

// --- CompanyModuleOverride ---
// Add or suppress a specific module for this company, outside of plan.
public class CompanyModuleOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ModuleDefinitionId { get; set; }

    public bool IsEnabled { get; set; } = true; // false = suppress from plan
    public string Note { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Company { get; set; } = null!;
    public ModuleDefinition ModuleDefinition { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

// --- UserModuleOverride --- (Phase 3 / optional)
// Grant or suppress a specific module for one user within a company.
// Use only when justified — most access is company-scoped.
public class UserModuleOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public Guid ModuleDefinitionId { get; set; }

    public bool IsEnabled { get; set; } = true;
    public string Note { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant Company { get; set; } = null!;
    public User User { get; set; } = null!;
    public ModuleDefinition ModuleDefinition { get; set; } = null!;
}
```

### Updated `IEntitlementResolver` (Phase 2)

```csharp
public interface IEntitlementResolver
{
    // EXISTING:
    Task<CompanySubscription?> GetSubscriptionAsync(Guid companyId);
    Task<PlanTemplate?> GetActivePlanAsync(Guid companyId);
    Task<IEnumerable<ModuleDefinition>> GetCompanyModulesAsync(Guid companyId);
    Task<CompanyEntitlements> GetEntitlementsAsync(Guid companyId);
    Task<bool> CanUseModuleAsync(Guid companyId, string moduleKey);
    Task<bool> CanInviteUserAsync(Guid companyId, int currentMemberCount);
    Task<bool> IsWithinTokenLimitAsync(Guid companyId, int tokensToConsume);
    Task<bool> IsWithinApiLimitAsync(Guid companyId);
    Task<bool> IsWithinStorageLimitAsync(Guid companyId, int mbToConsume);
    Task<bool> IsWithinKnowledgeBaseLimitAsync(Guid companyId);
    Task<LimitCheckResult> CheckLimitAsync(Guid companyId, string limitType, int amount = 1);

    // NEW (Phase 2):
    Task<CompanyEntitlementOverride?> GetEntitlementOverrideAsync(Guid companyId);
    Task<IEnumerable<CompanyModuleOverride>> GetModuleOverridesAsync(Guid companyId);
    Task<CompanyEntitlements> GetEffectiveEntitlementsAsync(Guid companyId); // plan + override merged
    Task<bool> CanUseModuleWithOverridesAsync(Guid companyId, string moduleKey); // override-aware
}
```

### `CompanyEntitlements` Extension (Phase 2)

```csharp
public class CompanyEntitlements
{
    // EXISTING:
    public int? MaxSeats { get; set; }
    public int MaxMonthlyTokens { get; set; }
    public int MaxApiRequestsPerDay { get; set; }
    public int MaxStorageMb { get; set; }
    public int MaxKnowledgeBases { get; set; }
    public int TokensUsedThisPeriod { get; set; }
    public int ApiRequestsUsedToday { get; set; }
    public int StorageUsedMb { get; set; }
    public int KnowledgeBasesCount { get; set; }

    // NEW (Phase 2):
    public int MaxInboxMessagesPerMonth { get; set; }     // 0 = not applicable
    public int InboxMessagesUsedThisMonth { get; set; }
    public int MaxMailboxConnections { get; set; }        // 0 = not applicable

    // Override metadata (for UI "Custom" badge):
    public bool HasEntitlementOverride { get; set; }
    public string? OverrideNote { get; set; }

    // EXISTING helpers:
    public bool CanAddSeats(int count) => MaxSeats == null || (MaxSeats.Value >= count);
    public bool CanAddTokens(int count) => MaxMonthlyTokens >= (TokensUsedThisPeriod + count);
    public bool CanAddApiRequests => MaxApiRequestsPerDay > ApiRequestsUsedToday;
    public bool CanAddStorage(int mb) => MaxStorageMb >= (StorageUsedMb + mb);
    public bool CanAddKnowledgeBase => MaxKnowledgeBases > KnowledgeBasesCount;

    // NEW helpers:
    public bool CanProcessInboxMessage => MaxInboxMessagesPerMonth == 0 || 
        MaxInboxMessagesPerMonth > InboxMessagesUsedThisMonth;
}
```

### Full Schema Overview

```
PlanTemplates
    PlanModuleInclusions  → ModuleDefinitions
    PlanEntitlements

Tenants (= Companies)
    CompanySubscriptions  → PlanTemplates
    CompanyEntitlementOverrides  [Phase 2]
    CompanyModuleOverrides  → ModuleDefinitions  [Phase 2]
    UserCompanyMemberships
        └── role (CompanyRole enum)
    UsageEvents
    AuditTrail

Users
    └── role (UserRole enum — global)
    UserCompanyMemberships → Tenants  (one per company)
    UserModuleOverrides  → ModuleDefinitions  [Phase 3]

ModuleDefinitions
    ModuleAssignments (Company/Dept/User-scoped grants — existing system)
    ModulePermissionGrants (fine-grained CanView/CanUse etc)
    PlanModuleInclusions
    CompanyModuleOverrides
```

### Fix Required: `Tenant.Plan` String Field

The `Tenant.Plan` string field is legacy. It conflicts with `CompanySubscription → PlanTemplate`. Resolution:

**Option A (safe)**: Keep `Tenant.Plan` as a **denormalized read model** — update it whenever `CompanySubscription` is changed. Explicit sync in `CompanySubscriptionService.AssignPlanAsync()`.

**Option B (clean)**: Deprecate `Tenant.Plan` entirely, always read from `CompanySubscription`. Requires updating all code that reads `Tenant.Plan` directly (currently: `AdminController.GetGlobalMetrics()`).

**Recommendation**: Do **Option A** now (sync on assignment), plan **Option B** for a cleanup migration.

---

## 8. Admin UI Structure

### Super Admin Navigation

```
/admin
├── /admin                          Dashboard: MRR, active companies, trials, usage alerts
├── /admin/companies                List: plan badge, seat count, status, usage health
│   └── /admin/companies/[id]       Detail:
│       ├── Overview tab            plan, status, billing period, custom badge
│       ├── Modules tab             plan modules + override list (add/suppress)
│       ├── Entitlements tab        limit table with override fields + note
│       ├── Members tab             member list with roles
│       ├── Usage tab               usage meters (tokens, storage, inbox, seats)
│       ├── Billing History tab     billing events (Phase 3)
│       └── Audit Log tab           filterable audit trail for this company
├── /admin/plans                    List: plan cards with status, price, companies count
│   └── /admin/plans/[id]           Detail: modules, entitlements, assigned companies
│       ├── New: /admin/plans/new
│       └── Edit: /admin/plans/[id]/edit
├── /admin/modules                  List + CRUD for module definitions
└── /admin/settings                 Platform config (future: webhooks, integrations)
```

### Company Admin Navigation (in-app)

```
/settings
├── /settings/organization          Name, logo, domain
├── /settings/team                  Invite, role change, remove member
├── /settings/billing               Current plan, usage meters, upgrade CTA
│   └── /settings/billing/upgrade   Plan selection + comparison table
├── /settings/access                Read-only module list (from plan + overrides)
└── /settings/security              Audit log (own company), API keys
```

### UI Component Rules

| Component | Visibility |
|---|---|
| "Plan" badge on any entity | All roles (read-only for non-admins) |
| Usage meters | CompanyAdmin+, SuperAdmin |
| "Override" badge | SuperAdmin only in /admin context |
| "Upgrade" CTA | CompanyOwner only |
| Module list (plan modules) | CompanyAdmin+ (read-only for others) |
| Suspend button | SuperAdmin only |
| Audit log | CompanyAdmin+ (own co), SuperAdmin (any co) |

---

## 9. Limits and Enforcement Design

### Limit Types and Enforcement Mode

| Limit | Enforcement | Action on breach |
|---|---|---|
| `MaxSeats` | **Hard** | Block invite — `403 SEAT_LIMIT_EXCEEDED` |
| `MaxMonthlyTokens` | **Soft then Hard** | Warn at 80% (response header); block at 100% — `402` |
| `MaxApiRequestsPerDay` | **Hard** | `429 TOO_MANY_REQUESTS` |
| `MaxStorageMb` | **Soft then Hard** | Warn at 80%; block upload at 100% — `403` |
| `MaxKnowledgeBases` | **Hard** | Block KB creation — `403` |
| `MaxInboxMessages` | **Soft then Hard** | Message silently held at 100% (no auto-discard); warn at 80% |
| `MaxMailboxConnections` | **Hard** | Block new mailbox — `403` |

### Warning System

```
X-Usage-Warning: tokens:80   // response header when >80% consumed
X-Usage-Limit: 50000         // current limit
X-Usage-Used: 41000          // current usage
```

### Enforcement Locations

| Service Method | Checked Before |
|---|---|
| `AgentService.ProcessInternalAsync()` | Token write |
| `InboxGuardianService.ProcessAsync()` | Inbox message count |
| `IngestionPipelineService.IngestAsync()` | Storage + KB limit |
| `InviteController.InviteUser()` | Seat limit |
| `MailboxConnectionsController` | Mailbox connection limit |

**Single entry point**: All limits flow through `IEntitlementResolver.CheckLimitAsync()`. Never do ad-hoc limit checks in controllers.

### Usage Reset Logic

- Monthly limits reset at `CompanySubscription.CurrentPeriodStart` renewal date
- NOT calendar month — billing period month
- A scheduled job (Hangfire) resets `UsageEvent` aggregates on period renewal
- Raw `UsageEvent` records are kept for audit; aggregation is recalculated periodically

### Soft Limit Warning Implementation

```csharp
// In UsageService or middleware:
public async Task RecordAndWarnAsync(HttpContext ctx, Guid companyId, string metric, long quantity)
{
    await _usageService.RecordAsync(companyId, metric, quantity);

    var result = await _entitlementResolver.CheckLimitAsync(companyId, metric);
    if (result.CurrentUsage > 0 && result.Limit > 0)
    {
        var pct = (double)result.CurrentUsage / result.Limit;
        if (pct >= 0.8)
        {
            ctx.Response.Headers["X-Usage-Warning"] = $"{metric}:{(int)(pct * 100)}";
            ctx.Response.Headers["X-Usage-Limit"] = result.Limit.ToString();
            ctx.Response.Headers["X-Usage-Used"] = result.CurrentUsage.ToString();
        }
    }
}
```

---

## 10. Production Concerns

### Audit Logging

- **All mutations in Super Admin context write to `AuditTrail`**: plan assignments, suspensions, overrides, module changes
- Existing `AuditService.RecordAsync()` is sufficient for Phase 1
- **Phase 2 extension**: Add `Before`/`After` JSON snapshot for override changes

```csharp
// AuditTrail entity extension (Phase 2):
public class AuditTrail
{
    // EXISTING:
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Action { get; set; }
    public string Details { get; set; }
    public DateTime CreatedAt { get; set; }

    // NEW (Phase 2):
    public Guid? ActorUserId { get; set; }           // who performed the action
    public string? EntityType { get; set; }          // "CompanySubscription", "PlanTemplate"
    public Guid? EntityId { get; set; }              // affected entity ID
    public string? BeforeJson { get; set; }          // state before mutation
    public string? AfterJson { get; set; }           // state after mutation
}
```

### Security

- **`IsSuperAdmin()` check at controller method level** — already done consistently
- **Do NOT use `[Authorize(Roles = "SuperAdmin")]`** — the existing role parsing pattern is safer
- **Phase 2**: Introduce `GlobalAdminPolicy` and `SuperAdminPolicy` in `Program.cs` for cleaner attribute-based auth
- `IgnoreQueryFilters()` — must ONLY be used in SuperAdmin-scoped queries (already correct in `AdminController`)
- **Never trust `Tenant.Plan` string** for access decisions — always resolve from `CompanySubscription`

### Tenant Isolation

- All override entities (`CompanyEntitlementOverride`, `CompanyModuleOverride`) must have `CompanyId` and be included in EF query filter scope
- Override resolution must use `IgnoreQueryFilters()` only in `EntitlementResolver` when called from admin context — or query by explicit `companyId`
- `BackgroundTenantProvider` handles jobs — no change needed

### Migration Strategy

1. **Add `CompanyEntitlementOverride` table** — nullable override fields; no data migration required
2. **Add `CompanyModuleOverride` table** — no data migration required
3. **Extend `PlanModuleInclusion`** — add nullable `MaxUsagePerMonth`, `MaxItemsTotal` columns; existing rows unaffected
4. **Extend `ModuleDefinition`** — add `IconKey`, `UpgradePromptText`, `SortOrder`; nullable, no migration risk
5. **Extend `PlanTemplate`** — add `SortOrder`, `IsPubliclyVisible`; default values set in migration
6. **Sync `Tenant.Plan`** — trigger update logic in `CompanySubscriptionService.AssignPlanAsync()` immediately

### Testing Strategy

| Layer | Test |
|---|---|
| `EntitlementResolver` | Unit: override applied when present; plan default when no override |
| `CompanySubscriptionService` | Integration: assign plan validates seats, writes audit |
| Seat limit enforcement | Integration: invite blocked when at limit |
| Module override resolution | Unit: suppressed module not returned even if in plan |
| Soft limit warnings | Unit: header present at 80%, absent at 50% |
| Super Admin access | Integration: non-SuperAdmin returns 403 |
| InternalOperator access | Integration: can read, cannot mutate |

---

## 11. Implementation Phases

### Phase 1 (MVP — already done ✅)

Core plan system is implemented. The following are confirmed complete:
- `PlanTemplate` CRUD
- `PlanModuleInclusion`, `PlanEntitlements`
- `CompanySubscription`, `CompanySubscriptionService`
- `EntitlementResolver` — all limit checks
- `AdminController` — companies, assign, suspend
- `PlansController` — full CRUD
- `UsageEvent`, `AuditTrail`, services

### Phase 2 (Next — these are the gaps)

**Goal**: Company-level overrides, fine-grained module control, InternalOperator enforcement

Priority order:

1. **Fix `Tenant.Plan` sync** — add sync call in `AssignPlanAsync()` (1 line fix, immediate)
2. **Add `CompanyEntitlementOverride`** entity, migration, service methods, admin API endpoint
3. **Add `CompanyModuleOverride`** entity, migration, service methods, admin API endpoint
4. **Extend `EntitlementResolver`** — override-aware `GetEffectiveEntitlementsAsync()` and `CanUseModuleWithOverridesAsync()`
5. **Add `CompanyRole` claim to JWT** — modify `AuthService.MintJwtAsync()` to emit company role separately from global role
6. **Enforce `InternalOperator`** — add read-only access to admin endpoints
7. **Trial expiration job** — Hangfire job: check `TrialEndsAt`, downgrade to Free if expired
8. **Extend `AuditTrail`** — add `ActorUserId`, `EntityType`, `EntityId`, `BeforeJson`, `AfterJson`
9. **Extend `PlanModuleInclusion`** — add `MaxUsagePerMonth`, `MaxItemsTotal` for per-module limits
10. **Add company audit log endpoint** — `GET /api/admin/companies/{id}/audit`
11. **Modules CRUD** — `GET/POST/PUT /api/admin/modules` (currently partial)

### Phase 3 (Future)

- `UserModuleOverride` — user-level module grants (only if product requires it)
- `BillingHistory` table — payment records
- Stripe/payment provider integration
- Revenue dashboard (MRR, churn)
- Dunning flow (PastDue → Suspended)
- Multi-currency plan support
- SAML/OIDC per company
- Seat add-ons

---

## 12. Recommendations

### What to Build First (MVP Gap Closure)
1. **`Tenant.Plan` sync** — 1-line fix. Prevents data drift. Do it now.
2. **`CompanyEntitlementOverride`** — most requested SA capability; straightforward entity + 2 endpoints
3. **`CompanyModuleOverride`** — enables trial deals ("we'll give you LeadQualifier for 30 days")
4. **`CompanyRole` in JWT** — enables cleaner permission checks; required before frontend expansion

### What Can Wait
- `UserModuleOverride` — adds complexity without clear product need at MVP
- `BillingHistory` — nice to have, not blocking
- Stripe — integrate only when accepting payments
- Dunning — needs Stripe first

### What to Avoid Early
- **Per-user entitlement overrides** — almost always a smell; model it at company level first
- **Embedding plan logic in controllers** — all access decisions through `EntitlementResolver`
- **Conflating plan modules with `ModuleAssignment`** — the `ModuleAssignment` system (scope-based) is for role-based module restrictions within a company. The plan system is for what the company CAN use. These are different layers and must stay separate.
- **Hardcoded plan slugs in UI/logic** — use `PlanCatalog.GrowthId` constants, not string literals

### How to Scale Later Without Redesign
- The override hierarchy (plan → company override → user override) scales to any depth
- `EntitlementResolver` as the single authority means pricing changes are DB operations
- New module? Add `ModuleDefinition` row + `PlanModuleInclusion` rows. Zero code changes needed.
- New limit dimension? Add field to `PlanEntitlements` + `CompanyEntitlementOverride`. All resolvers pick it up.
- Multi-product expansion? Each product gets its own `ModuleDefinition.Category`. Plans include modules from one or more categories.

---

## 13. Summary: Files to Create or Modify

### New Core Entities (Phase 2)
- `OrvixFlow.Core/Entities/CompanyEntitlementOverride.cs` [NEW]
- `OrvixFlow.Core/Entities/CompanyModuleOverride.cs` [NEW]
- `OrvixFlow.Core/Entities/UserModuleOverride.cs` [NEW — Phase 3]

### Modified Entities (Phase 2)
- `OrvixFlow.Core/Entities/PlanModuleInclusion.cs` — add `MaxUsagePerMonth`, `MaxItemsTotal`
- `OrvixFlow.Core/Entities/ModuleDefinition.cs` — add `IconKey`, `UpgradePromptText`, `SortOrder`
- `OrvixFlow.Core/Entities/PlanTemplate.cs` — add `SortOrder`, `IsPubliclyVisible`
- `OrvixFlow.Core/Entities/AuditTrail.cs` — add actor, entity, before/after JSON
- `OrvixFlow.Core/Interfaces/IEntitlementResolver.cs` — add override-aware methods

### New Infrastructure (Phase 2)
- `OrvixFlow.Infrastructure/Services/EntitlementResolver.cs` — extend with override logic
- `OrvixFlow.Infrastructure/Services/CompanySubscriptionService.cs` — sync `Tenant.Plan`
- `OrvixFlow.Infrastructure/Data/AppDbContext.cs` — register new entities + global filters

### New API Endpoints (Phase 2)
- `OrvixFlow.Api/Controllers/AdminController.cs` — add override endpoints
- `OrvixFlow.Api/Controllers/ModulesController.cs` — full CRUD for module definitions

### New Migration (Phase 2)
- `OrvixFlow.Infrastructure/Migrations/AddOverrideSystem/` — new EF migration

### Frontend (Phase 2)
- `orvixflow-web/app/admin/companies/[id]/page.tsx` — Entitlements + Modules tabs
- `orvixflow-web/app/admin/modules/page.tsx` [NEW] — modules CRUD
- `orvixflow-web/app/admin/companies/[id]/audit/page.tsx` [NEW] — per-company audit log

---

*This document is the source of truth for Phase 2 implementation. Design decisions are based on: actual code inspection, existing entity model, current service layer, and admin panel design plan in `tasks/admin_panel_plan.md`.*
