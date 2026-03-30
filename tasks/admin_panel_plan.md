# OrvixFlow Admin Panel — Production Design Plan

## 1. Executive Summary

OrvixFlow is a multi-tenant SaaS platform where companies (tenants) are the primary billing unit. Each company subscribes to a plan, and users inside that company inherit access governed by:

1. The **company's active billing plan** (what modules are unlocked, what limits apply)
2. The **user's internal role** within the company (what actions they're permitted to take)

The admin panel has two consumers:
- **Super Admins** (OrvixFlow internal team) — manage everything: plans, modules, pricing, companies
- **Company Admins / Owners** — manage their own company: members, roles, billing overview, module visibility

> The guiding principle: **Plans govern access at the company level. Roles govern permissions within a company. Never conflate the two.**

---

## 2. Recommended Business Model

### Plan granularity: **Company-level plans, globally defined templates**

Use **global plan templates** that you define once, assign to any company. Companies can receive **entitlement overrides** (e.g., a custom limit on seats or tokens) without creating an entirely new plan.

### Plan tiers (recommended MVP)

| Tier | Price | Target |
|---|---|---|
| **Free** | $0 | Solo users, evaluation |
| **Starter** | $29/mo | Small teams (up to 5 seats) |
| **Growth** | $99/mo | Mid-size teams (up to 25 seats) |
| **Business** | $299/mo | Large teams (up to 100 seats) |
| **Enterprise** | Custom | Unlimited, SLA, custom modules |

### Pricing Model: **Seat-Based within Company tier**

- Each plan has a max seat count; adding users beyond it requires upgrade
- Keep annual billing as a discount option (e.g., 2 months free = 20% off)
- One currency at MVP: **USD**; multi-currency in Phase 3

### Trial support
- Every new company gets **14 days Trialing** on the Growth plan
- After trial, downgrade to Free unless payment method added

---

## 3. Recommended Entity Model and Relationships

```
PlanTemplate (global)
  └── PlanModuleInclusion[] (which modules are included in this plan)
  └── PlanEntitlements (limits: seats, tokens, storage, requests)

Company (= Tenant)
  └── CompanySubscription → PlanTemplate
  └── CompanyEntitlementOverride (custom limits for this company)
  └── CompanyModuleOverride (extra modules added outside of plan)
  └── UserCompanyMembership (user ↔ company ↔ role)
  └── UsageRecord[] (per-company, per-period usage tracking)
  └── BillingHistory[]
  └── AuditLog[]

ModuleDefinition
  └── PlanModuleInclusion[] (which plans include this module)
  └── CompanyModuleOverride[] (which companies have this module ad hoc)

User
  └── UserCompanyMembership (user belongs to 1+ companies, each with a role)
```

### Key rule
Access for a user = `company plan modules + company module overrides`, filtered by their role-based permissions within the company.

---

## 4. Admin Panel Information Architecture

### Super Admin Sections

```
/admin
├── Dashboard          — KPIs: MRR, active companies, trials expiring, usage alerts
├── Plans              — manage plan templates
│   ├── List
│   ├── Create / Edit
│   └── [Plan Detail] — modules, limits, assigned companies
├── Modules            — manage module definitions
│   ├── List
│   └── Create / Edit / Archive
├── Companies          — all tenants
│   ├── List (with plan badge, seat count, status)
│   └── [Company Detail]
│       ├── Overview (plan, subscription status, billing)
│       ├── Modules (plan defaults + overrides)
│       ├── Entitlements (limit overrides)
│       ├── Members
│       ├── Usage
│       ├── Billing History
│       └── Audit Log
├── Billing            — global billing overview
│   ├── Revenue dashboard
│   └── Failing subscriptions / dunning
└── Settings           — platform config
```

### Company Admin Sections (in-app)

```
/settings
├── Organization       — company name, logo, status
├── Members            — invite, change role, remove
├── Plan & Billing     — current plan, usage meter, upgrade CTA, billing history
├── Access & Modules   — read-only: which modules are active for their company
└── Security           — SSO config, audit log (own company)
```

---

## 5. Billing Plan Design

### PlanTemplate fields

| Field | Type | Notes |
|---|---|---|
| [id](file:///d:/Antigravity/OrvixFlow/OrvixFlow.Api/Controllers/OrganizationController.cs#270-279) | UUID | |
| `name` | string | "Starter", "Growth", etc. |
| `slug` | string | URL-safe identifier |
| `description` | string | |
| `monthlyPriceCents` | int | Stored in cents to avoid float issues |
| `yearlyPriceCents` | int | |
| `currency` | string | "USD" at MVP |
| `billingInterval` | enum | Monthly / Yearly / Custom |
| `maxSeats` | int | null = unlimited |
| `isActive` | bool | Inactive = no new signups, existing unchanged |
| `isFree` | bool | |
| `isTrialAllowed` | bool | Only one trial per company |
| `trialDays` | int | 14 default |
| `legacyLocked` | bool | Grandfathered — no upgrade/downgrade to this |
| `createdAt` | datetime | |
| `archivedAt` | datetime | null = active |

### Plan state transitions

```
Draft → Active → Archived (not deleted — billing history must survive)
Active ↔ Inactive (toggle: disables new assignments, existing continue)
Grandfathered: new flag, never surfaced in upgrade flows
```

### Upgrade/downgrade
- Upgrade: immediate with proration applied (charge difference)
- Downgrade: at end of billing period, queued
- Store `pendingPlanChangeId` + `pendingChangesAt` on `CompanySubscription`

---

## 6. Module and Entitlement Design

### ModuleDefinition

| Field | Notes |
|---|---|
| `key` | Unique string, e.g., `inbox.auto` |
| `displayName` | |
| `description` | |
| `category` | e.g., [ai](file:///d:/Antigravity/OrvixFlow/orvixflow-web/app/%28dashboard%29/inbox/pending/page.tsx#135-149), `workflow`, `integrations` |
| `isActive` | |
| `isPremium` | Helps UI show upgrade prompts |

### Access resolution algorithm (evaluated on every request)

```
1. Get user's active company
2. Get company's active plan → get plan's included modules
3. Get company's module overrides (add/remove on top of plan)
4. Check user's role → apply role-based restrictions
5. Grant or deny
```

### Role-based module restrictions
- The plan says "what the company CAN use"
- The user's role says "what the user CAN DO inside that scope"
- Example: Company has `audit.logs` module via plan, but only `CompanyAdmin` role can view them — `Member` role cannot even though the module is active

### Entitlements

| Entitlement | Type | Limit model |
|---|---|---|
| `maxSeats` | int | Hard — blocks user invites |
| `maxMonthlyTokens` | int | Soft — warn at 80%, hard block at 100% |
| `maxApiRequestsPerDay` | int | Hard — 429 response |
| `maxStorageMb` | int | Soft warn → hard block on upload |
| `maxKnowledgeBases` | int | Hard |

**Overages**: Not supported at MVP. Soft limits log an event and warn, hard limits return `402/429/403` with a clear `LIMIT_EXCEEDED` code.

**Reset model**: Monthly limits reset on billing renewal date (not calendar month).

---

## 7. Company / User Access Model

### On registration
1. User registers → [User](file:///d:/Antigravity/OrvixFlow/OrvixFlow.Api/Controllers/OrganizationController.cs#283-291) record created
2. Auto-create [Company](file:///d:/Antigravity/OrvixFlow/orvixflow-web/app/%28dashboard%29/settings/page.tsx#65-117) (Tenant) named after user's email domain or name
3. Create `CompanySubscription` with `PlanTemplate = Trial(Growth)`, `trialEndsAt = now + 14d`
4. Create `UserCompanyMembership` with role `CompanyOwner`

### Role hierarchy

```
SuperAdmin        (platform level — OrvixFlow team only)
  └── CompanyOwner      (full control of own company)
      └── CompanyAdmin  (manage members, billing view)
          └── Member    (use modules per plan, no management access)
              └── Viewer (read-only inside company)
```

### Access inheritance rule
> A user's effective access = **intersection of company plan entitlements and user role permissions**

Neither role alone, nor plan alone, grants access — both must permit it.

---

## 8. Core Workflows

### Create Plan
1. Admin fills name, price, limits, modules included
2. Status=Draft — not visible to companies
3. Publish → Status=Active
4. Assign to companies manually or set as default for new signups

### Assign Plan to Company
1. Super Admin → Company Detail → Billing tab → Change Plan
2. System validates seats (current users ≤ new plan maxSeats)
3. Proration calculated, confirmation modal shown
4. `CompanySubscription.planId` updated, `changedAt` logged
5. Audit log entry written

### Override Company Entitlement
1. Super Admin → Company Detail → Entitlements tab
2. Override specific fields (e.g., `maxSeats = 50` on a 25-seat plan)
3. Override stored in `CompanyEntitlementOverride`, takes precedence over plan defaults
4. Visible in Company Detail with "Custom" badge

### Add Module Override
1. Super Admin → Company Detail → Modules tab
2. Check modules included by plan (read-only)
3. Add extra module → `CompanyModuleOverride` record created with note
4. Remove module → override stored with `isEnabled=false` (can suppress a plan module too)

### Suspend Company
1. Admin sets `CompanySubscription.status = Suspended`
2. All API calls for that tenant return `403 ACCOUNT_SUSPENDED`
3. Users still see their data but cannot use features
4. Audit entry created

### Upgrade/Downgrade
1. Company Admin → Plan & Billing → Upgrade/Downgrade
2. New plan selected, proration shown
3. Confirm → `pendingPlanId` set (immediate for upgrade, end-of-period for downgrade)
4. Webhook triggers billing provider

---

## 9. Permission Model

| Action | SuperAdmin | CompanyOwner | CompanyAdmin | Member |
|---|---|---|---|---|
| Create/edit plans | ✅ | ❌ | ❌ | ❌ |
| Assign plan to company | ✅ | ❌ | ❌ | ❌ |
| Override entitlements | ✅ | ❌ | ❌ | ❌ |
| Suspend company | ✅ | ❌ | ❌ | ❌ |
| View all companies | ✅ | ❌ | ❌ | ❌ |
| Invite members (own co) | ✅ | ✅ | ✅ | ❌ |
| Change member role | ✅ | ✅ | ❌ | ❌ |
| View plan & billing (own) | ✅ | ✅ | ✅ | ❌ |
| Upgrade/downgrade plan | ✅ | ✅ | ❌ | ❌ |
| View audit log (own) | ✅ | ✅ | ✅ | ❌ |
| Use modules | Per plan+role | Per plan+role | Per plan+role | Per plan+role |

---

## 10. Data Model Draft

```sql
-- Global plan templates
PlanTemplates (Id, Name, Slug, Description, MonthlyPriceCents, YearlyPriceCents,
               Currency, MaxSeats, IsActive, IsFree, IsTrialAllowed, TrialDays,
               LegacyLocked, CreatedAt, ArchivedAt)

-- Which modules are included in each plan
PlanModuleInclusions (Id, PlanTemplateId FK, ModuleDefinitionId FK)

-- Entitlements attached to a plan
PlanEntitlements (Id, PlanTemplateId FK, MaxMonthlyTokens, MaxApiRequestsPerDay,
                  MaxStorageMb, MaxKnowledgeBases)

-- Module catalog
ModuleDefinitions (Id, Key, DisplayName, Description, Category, IsActive, IsPremium)

-- Company subscription
CompanySubscriptions (Id, CompanyId FK, PlanTemplateId FK, Status [Trialing/Active/PastDue/Suspended/Cancelled],
                      BillingInterval, CurrentPeriodStart, CurrentPeriodEnd,
                      TrialEndsAt, PendingPlanId FK, PendingChangeAt,
                      ExternalSubscriptionId, CreatedAt, UpdatedAt)

-- Per-company limit overrides
CompanyEntitlementOverrides (Id, CompanyId FK, MaxSeats, MaxMonthlyTokens,
                              MaxApiRequestsPerDay, MaxStorageMb, MaxKnowledgeBases,
                              Note, CreatedBy FK, CreatedAt)

-- Per-company module overrides
CompanyModuleOverrides (Id, CompanyId FK, ModuleDefinitionId FK, IsEnabled, Note,
                        CreatedBy FK, CreatedAt)

-- Usage tracking (write often, aggregate for display)
UsageRecords (Id, CompanyId FK, PeriodStart, PeriodEnd, TokensUsed, ApiRequests,
              StorageUsedMb, CreatedAt)

-- Billing history
BillingHistory (Id, CompanyId FK, Amount, Currency, Status, Description,
                ExternalInvoiceId, PaidAt, CreatedAt)

-- Audit log
AuditLogs (Id, ActorId FK, ActorType [User/System], CompanyId FK,
           Action, EntityType, EntityId, Before JSON, After JSON, CreatedAt)
```

---

## 11. UX Recommendations

- **Plan detail page**: Show modules as checkboxes (read-only for assigned companies), usage limits as a table with current vs max
- **Company detail page**: Always show a "billing health" banner (Trialing → X days left, PastDue → payment failed, Suspended → blocked)
- **Confirmation modals**: Any plan change, suspension, or entitlement override must show a confirmation modal with a clear summary of what changes and who's affected
- **Usage meters**: Show bar charts for seat usage, token usage. Color-code: green <70%, yellow 70-90%, red >90%
- **Inline module list**: On company detail, show all modules with their source (From Plan / Custom Override / Suppressed)
- **Audit log**: Always visible, filterable by action type, actor, entity type, date range

---

## 12. Production-Readiness Requirements

| Concern | Recommendation |
|---|---|
| **Audit logs** | Required from Day 1 — every mutation writes to AuditLogs |
| **Soft delete** | Plans archived not deleted. AuditLogs never deleted |
| **Validation** | Never allow seat count downgrade below current active members |
| **Safe defaults** | New companies get Trial plan auto; modules off by default |
| **Migration strategy** | PlanTemplates are add-only. Old plans locked with `LegacyLocked` |
| **Backward compat** | Plan entitlement resolution is additive; existing companies unaffected by new plan changes |
| **Observability** | Log every access-denied event with reason code; expose usage metrics via `/admin/usage` endpoint |
| **Security** | Super Admin role enforced at both route and API level; company admins scoped to own tenant via [TenantId](file:///d:/Antigravity/OrvixFlow/OrvixFlow.Api/Services/TenantProvider.cs#16-47) claim |
| **Testing** | Unit test entitlement resolution logic; integration test plan assignment and override flows |

---

## 13. Phased Implementation Plan

### Phase 1: MVP (implement first)

**Goal**: Functional admin panel with plan management and company assignment

- [x] `PlanTemplates` CRUD (create, edit, archive, list)
- [x] `ModuleDefinitions` CRUD
- [x] `PlanModuleInclusion` — associate modules with plans
- [x] `PlanEntitlements` — store limits per plan
- [x] `CompanySubscriptions` — assign plan to company (Super Admin only)
- [x] Entitlement resolution service — single source of truth for "what can this company do"
- [x] Module access check middleware (replace current [ModuleGate](file:///d:/Antigravity/OrvixFlow/orvixflow-web/components/module-gate.tsx#14-63) hardcoded approach)
- [x] Super Admin: Company detail page (plan, status, members)
- [x] Company Admin: Plan & Billing page (read-only view of current plan + usage)
- [x] Seat limit enforcement on user invite
- [x] AuditLog writes for plan assignments + member changes

> **Skip in Phase 1**: entitlement overrides, module overrides, billing history, payment integration

---

### Phase 2: Growth (next milestone)

**Goal**: Self-serve upgrade/downgrade + entitlement flexibility

- [ ] `CompanyEntitlementOverrides` — per-company limit customization
- [ ] `CompanyModuleOverrides` — add/remove modules per company outside plan
- [x] Upgrade/downgrade flow (in-app by Company Owner)
- [x] Proration calculation (placeholder - Stripe integration pending)
- [x] BillingHistory table + display
- [x] Usage tracking writes (`UsageRecords`) with monthly reset
- [x] Usage meters in admin + company dashboards
- [ ] Trial expiration flow (warn at 7d, 3d, 0d)
- [ ] Dunning flow (payment failure → PastDue → Suspended after X days)

> **Skip in Phase 2**: Stripe integration (moved to Phase 3)

---

### Phase 3: Scale (future)

**Goal**: Enterprise readiness, multi-currency, seat add-ons

- [ ] Grandfathered plan support (`LegacyLocked`)
- [ ] Multi-currency (store in plan, convert at checkout)
- [ ] Seat add-ons (buy extra seats without upgrading tier)
- [ ] Overage billing (soft overage → charge for extra tokens/requests)
- [ ] SSO integrations per company (SAML/OIDC)
- [ ] API-level rate limiting tied to plan entitlements (per-minute burst)
- [ ] Revenue dashboard for Super Admin (MRR, churn, expansion)
- [ ] GDPR/data export at company level
- [ ] Stripe / payment provider integration
- [ ] External webhook events (plan changed, trial expired, limit exceeded)

---

## 14. Final Recommendation

**Organize the system around three pillars:**

1. **PlanTemplate** — the immutable contract: what modules and limits a tier provides  
2. **CompanySubscription** — the live binding: which plan a company is on right now, with status  
3. **EntitlementResolver** — the single service that every module gate, invite flow, and API check calls

**Never hardcode plan logic in controllers or UI.** All access decisions must flow through `EntitlementResolver`, which reads plan + overrides in one place. This makes pricing changes, new plans, and feature flags a database operation, not a code deployment.

**For MVP safety**: implement plans as static records in the DB with no payment integration yet. Manually assign plans via Super Admin. This gives you the full data model from Day 1 without the complexity of a payment provider. Add Stripe in Phase 2 once the admin surfaces are working.

**What not to build yet**: multi-currency, overages, SAML, seat add-ons. These are premature at early stage and add significant complexity. The model above supports adding them without rewrites.
