# Department RBAC Redesign — Full Implementation Plan

> **Historical Implementation Record**
> This redesign has largely been implemented and synchronized into memory/code.
> Use current code and `memory/auth.md` as the source of truth; keep this file as implementation history, not as an active execution plan.

> [!CAUTION]
> This is a **breaking change to the authorization model**. Every layer (DB, backend, JWT, frontend) must be updated atomically. Run the full `dotnet test` suite and manual validation checklist before deploying to any environment with real data.

---

## Execution Status

- **2026-04-24 — Phase 1 executed in code**
  - Added `CompanyMember` role parsing/support and department-role parsing helpers.
  - Added nullable `Invitation.InvitedDepartmentRole`.
  - Added EF migration `20260424123540_AddDepartmentRbacPhase1` with data migration SQL for legacy memberships and pending invites.
  - Added focused TDD coverage in `OrvixFlow.Tests/DepartmentRbacPhase1Tests.cs` (green).
  - Full `dotnet test` currently reports expected follow-up failures in old-role tests (`TeamControllerTests`, `RoleCeilingTests`, `AuthServiceTests`, `AuthEndToEndFlowTests`, `GlobalRoleTests`) because phases 2+ are not implemented yet.

---

- **2026-04-24 — Phase 2 executed in code**
  - Reworked backend role helpers around `CompanyMember` + department-scoped role checks.
  - Updated `InviteController`, `TeamController`, `AuthService`, and `AccessResolver` to authorize department managers by `UserDepartmentMembership` instead of JWT role alone.
  - Added/updated focused TDD coverage for team scoping, scoped invites, invite acceptance, access resolution, and company-member JWT/profile expectations.
  - Full `dotnet test` is green after the phase 2 backend rollout.

- **2026-04-24 — Phase 3 executed in code**
  - Updated frontend org RBAC helpers around company-tier session roles plus department-manager access derived from `/api/org/departments`.
  - Unlocked `organization` Team and Departments tabs for `CompanyMember` users who manage at least one department, while keeping Security/company-role actions admin-only.
  - Reworked `TeamTab` invite UX so department managers issue `CompanyMember` invites with explicit department roles.
  - Added Vitest coverage for org permission helpers plus Team/Departments tab behavior under department-manager scenarios.
  - Frontend verification is green: `npm test`, `npm run lint`, and `npm run build` in `orvixflow-web/`.

---

- **2026-04-24 — Phase 4 executed in docs**
  - Updated `memory/auth.md`, `memory/memory-architecture.md`, and `memory/memory-risks.md` to describe the live three-layer RBAC model and company-member + department-role rules.
  - Updated `memory/memory-security.md` and `memory/memory-testing.md` so security/testing guidance matches the implemented backend + frontend rollout.
  - Synchronized this implementation plan's execution status with the completed documentation phase.

## 1. Clarifying Questions / Assumptions

No blocking questions remain. The following assumptions are marked where they drive design decisions:

- **[A1]** A user belonging to multiple departments can have `DepartmentManager` in one and `DepartmentOperator` in another. The system must store and check this per-row.
- **[A2]** `DepartmentManager` can invite new external users into their own department. The invited user will join the company via `UserCompanyMembership` with role `CompanyMember` (new role) and the target department via `UserDepartmentMembership` with role `DepartmentManager` or `DepartmentOperator`.
- **[A3]** `Operator` is renamed to `DepartmentOperator` at the department level. The company-level slot it occupied is replaced by `CompanyMember`.
- **[A4]** `Viewer` is retired as a standalone company role. Read-only access is modelled as `DepartmentOperator` with restricted permissions at the module level (existing `ModulePermissionGrant` handles this).
- **[A5]** `CompanyOwner` and `CompanyAdmin` retain company-wide full access — no change to their behavior.
- **[A6]** The `Invitation.AssignedRole` field currently holds a company role. After this change it will hold the **department role** (`DepartmentManager` | `DepartmentOperator`), and the company-level role assigned will always be `CompanyMember` when the invite is accepted by a non-existing company member.

> [!IMPORTANT]
> **Confirm [A6] before execution.** The alternative is keeping `AssignedRole` as the company role and adding a separate `InvitedDepartmentRole` field to `Invitation`. The plan below uses a separate field because it is safer and backward-compatible with existing pending invites.

---

## 2. Current Org/Auth/Security Model Summary

### Role storage today

| Level | Entity field | Values stored |
|---|---|---|
| Global | `User.Role` | `""`, `SuperAdmin`, `InternalOperator` |
| Company | `UserCompanyMembership.CompanyRole` | `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer` |
| Department | `UserDepartmentMembership.DepartmentRole` | `"Manager"` or `"Member"` — **string literals, not enum values** |

### What the JWT carries today

```
Role = UserCompanyMembership.CompanyRole   (e.g. "DepartmentManager", "Operator")
```

Single Role claim covers both company-level admin and department-level roles. No per-department context.

### How DepartmentManager is checked today

```csharp
// Every guard uses ONE of:
callerRole.IsCompanyAdminOrAbove()          // excludes DepartmentManager → always 403
callerRole.IsDepartmentManagerOrAbove()     // does NOT exist yet
callerRole == UserRole.DepartmentManager    // used as global flag, not per-dept
```

`DepartmentManager` currently means:
- "This user is a manager of their departments" — stored as a company-level role
- `DepartmentRole` in each `UserDepartmentMembership` is always `"Manager"` for them (derived from CompanyRole via `ToDepartmentRoleValue()`)
- But NO API endpoint actually lets them DO anything — they 403 everywhere

### Where DepartmentManager appears as incorrect assumption in the codebase

| File | Line | Problem |
|---|---|---|
| `Roles.cs` | 36 | `DepartmentManager = 20` — part of `UserRole` enum alongside company roles |
| `Roles.cs` | 85, 94 | Included in `AllRoles` and `CompanyRoles` — treated as company-scoped |
| `Roles.cs` | 165–168 | `ToDepartmentRoleValue()` derives dept role from company role unconditionally |
| `TeamController.cs` | 29, 61, 109, 158 | All guards: `IsCompanyAdminOrAbove()` — DeptManager always blocked |
| `InviteController.cs` | 34, 62, 88 | Same — DeptManager blocked |
| `AccessResolver.cs` | 95 | `role is Operator or DepartmentManager` — treated as implied company-level |
| `ScopeContext.cs` | 96–110 | Correctly returns dept IDs for DeptManager, but treated as single uniform role |
| `auth.md` | 19 | Documents `DepartmentManager` as a company role |
| `memory-risks.md` | 192 | Documents same |
| `memory-architecture.md` | 218 | Documents same |
| `RoleCeilingTests.cs` | 278–289 | Test asserts DeptManager CANNOT invite — becomes wrong after this plan |
| `org-permissions.ts` | entire | DeptManager not in `ORG_ADMIN_ROLES` = tabs locked |

---

## 3. Problems with Current Role Model

1. **`DepartmentManager` is a company-level singleton.** One user → one DeptManager label per company → affects ALL their departments. Violates [A1].
2. **`DepartmentRole` in `UserDepartmentMembership` is overwritten from `CompanyRole` every time.** Real per-department differentiation is impossible.
3. **All team management APIs are locked behind `IsCompanyAdminOrAbove`.** DeptManagers can't do anything. This is why the bug exists.
4. **No `CompanyMember` role.** Regular users who belong to a company but have no company-level management power have no clean semantic. They're forced into `Operator` or `Viewer` which implies module permissions.
5. **`Viewer` conflates "company role" with "read-only module access".** A `Viewer` is actually `DepartmentOperator` with limited grants — this is already handled by `ModulePermissionGrant`, not role name.
6. **JWT single `Role` claim is insufficient.** A user's role differs per department; the JWT can only carry one company-level role. Department operations must look up `UserDepartmentMembership` rather than trust the JWT role alone.

---

## 4. New Role Model Proposal

### Tier 0 — Platform (stored in `User.Role`)

| Role | Scope | Description |
|---|---|---|
| `SuperAdmin` | Platform | Full platform control |
| `InternalOperator` | Platform | Platform support, read-only |
| `""` | Platform | Normal user (default) |

**No change from current model.**

---

### Tier 1 — Company (stored in `UserCompanyMembership.CompanyRole`)

| Role | Scope | Description |
|---|---|---|
| `CompanyOwner` | Company-wide | Full control, archive, billing |
| `CompanyAdmin` | Company-wide | Manage people, departments, modules |
| `CompanyMember` | Company | Belongs to company, access via dept roles |

> [!IMPORTANT]
> `DepartmentManager`, `Operator`, `Viewer` are **removed** from this tier. **`CompanyMember` is new.** Existing rows must be migrated (see §9).

**Stored in `UserCompanyMembership.CompanyRole`.** JWT `Role` claim carries company role for company-level decisions.

---

### Tier 2 — Department (stored in `UserDepartmentMembership.DepartmentRole`)

| Role | Scope | Description |
|---|---|---|
| `DepartmentManager` | Per-dept | Can invite users, manage dept members, manage dept data |
| `DepartmentOperator` | Per-dept | Can use modules in their dept, view and create |

**Stored in `UserDepartmentMembership.DepartmentRole`.** The value is **per row** — the same user can be `DepartmentManager` in dept A and `DepartmentOperator` in dept B.

**NOT embedded in JWT.** Department role is always looked up from DB at authorization time.

---

### Authorization decision matrix

| Decision | Source |
|---|---|
| Company-wide actions (invite, create dept, billing) | `UserCompanyMembership.CompanyRole` from JWT |
| Is user manager of dept X? | `UserDepartmentMembership WHERE DeptId=X AND DeptRole="DepartmentManager"` |
| Does user have access to dept X data? | `UserDepartmentMembership WHERE DeptId=X AND Status=Active` |
| Module billing access | `EntitlementResolver.CanUseModuleAsync` |
| Module user access | `AccessResolver` using dept memberships |

---

## 5. Department-Scoped Permission Matrix

| Action | CompanyOwner | CompanyAdmin | CompanyMember+DeptManager | CompanyMember+DeptOperator |
|---|:---:|:---:|:---:|:---:|
| View all company members | ✅ | ✅ | Own dept members only | ❌ |
| Invite brand new user to company | ✅ | ✅ | ✅ into own dept | ❌ |
| Invite existing company user to dept | ✅ | ✅ | ✅ own dept | ❌ |
| Revoke pending invite | ✅ | ✅ | ✅ own dept invites | ❌ |
| Change company role (Tier 1) | ✅ | ✅ (not Owner) | ❌ | ❌ |
| Change dept role (Tier 2) | ✅ | ✅ | ✅ own dept only | ❌ |
| Remove member from company | ✅ | ✅ | ❌ | ❌ |
| Remove member from own dept | ✅ | ✅ | ✅ own dept | ❌ |
| Create department | ✅ | ✅ | ❌ | ❌ |
| Edit department | ✅ | ✅ | ❌ | ❌ |
| Delete department | ✅ | ✅ | ❌ | ❌ |
| View own departments | ✅ all | ✅ all | ✅ own | ✅ own |
| Module access (KB, Inbox) | ✅ (billing gates) | ✅ | Dept scoped | Dept scoped, view-only |
| Admin panel | SuperAdmin/Internal only | ❌ | ❌ | ❌ |

---

## 6. Invite/Add-User Flow Redesign

### Scenario A — Invite brand new user (no existing account)

1. DeptManager calls `POST /api/invite` with `{ email, departmentId, departmentRole: "DepartmentOperator" }`.
2. Backend validates: caller has `DepartmentManager` dept role in the specified `departmentId`.
3. Creates `Invitation` record: `AssignedRole = "CompanyMember"`, `InvitedDepartmentRole = "DepartmentOperator"`, `DepartmentId = <id>`.
4. User receives email → clicks link → `POST /api/invite/accept` (no company context in URL, token carries it).
5. `AcceptInvitationAsync`: creates `UserCompanyMembership(CompanyRole="CompanyMember")` + `UserDepartmentMembership(DepartmentRole="DepartmentOperator")`.

### Scenario B — Add existing company user to another dept

1. DeptManager calls `POST /api/team/department-assignments` (new endpoint) with `{ userId, departmentId, departmentRole }`.
2. Backend validates: user has active `UserCompanyMembership` in this company, caller has `DepartmentManager` in the target dept.
3. Creates/updates `UserDepartmentMembership` — does NOT change `UserCompanyMembership`.
4. If user already has a `UserDepartmentMembership` row for that dept (even inactive), reactivates and updates role.

### Scenario C — User already in another dept (same company)

- This is just Scenario B. The existing membership in the other dept is **not touched**. Each dept membership is independent.
- No approval needed from the other dept manager.

### Scenario D — User from another company

- Not supported at company level — org boundaries are hard-isolated.
- DeptManager cannot invite cross-company. Only CompanyAdmin/Owner can invite someone who results in a new `UserCompanyMembership`.
- DeptManager invite always goes through the invite flow (creates a `UserCompanyMembership` if the email is new to the company).

### Scenario E — External invite by DeptManager

- DeptManager CAN invite external emails (someone with no account at all).
- The invitation creates a new user on acceptance with `CompanyRole = "CompanyMember"` and the specified `DepartmentRole`.
- This is permissible because: the seat count grows by one → checked against the entitlement limit before creating the invite.

### Pending invite representation

- `Invitation.AssignedRole` = always the company role (`"CompanyMember"` for DeptManager invites, `"CompanyAdmin"` for admin invites etc.)
- `Invitation.InvitedDepartmentRole` = new nullable field: `"DepartmentManager"` | `"DepartmentOperator"` | `null`
- `Invitation.DepartmentId` = existing field, already nullable
- UI shows pending invites filtered: DeptManagers only see invites where `DepartmentId` is in their managed depts.

---

## 7. Security Impact Analysis

| Area | Impact | Action Required |
|---|---|---|
| `RequireModuleAttribute` | Low — checks company billing + user-level grants. DeptRole change is transparent here | Update `AccessResolver` dept role check (see §10) |
| `AccessResolver` | Medium — fallback grant uses `role is Operator or DepartmentManager`, both company roles | Rewrite to use dept membership presence, not company role |
| `ScopeContext` | Low — already uses `UserDepartmentMembership` for dept IDs, company role used for wide-access check | Update: `CompanyMember` = not wide-access, must filter by dept IDs |
| Admin panel | None — only `SuperAdmin`/`InternalOperator` access |
| Billing/plans | None — billing uses `CompanyOwner` company membership to find owner |
| Tenant isolation | None — all dept membership rows have `CompanyId` = correct tenant |
| Archived tenants | None — archived tenant check is on `Tenant.LifecycleStatus` |
| Storage/KB access | Low — `FileIngestionController.CanAccessDepartment` uses `IScopeContext.AllowedDepartmentIds` → stays correct |
| Email Guardian | Low — uses `IScopeContext` → correct |
| `MintJwtAsync` | High — must now set `Role = UserCompanyMembership.CompanyRole` (CompanyOwner/Admin/CompanyMember), NOT the dept role |
| `RoleCeilingTests` | Must update — `DepartmentManager_CannotInvite` becomes wrong |
| `FileIngestionControllerTests` | Must update — test setup now uses `CompanyRole=CompanyMember`, `DepartmentRole=DepartmentManager` |
| Invite validation in `AuthService` | Medium — revalidation must use `InvitedDepartmentRole` not `AssignedRole` for dept membership |

---

## 8. Detailed Implementation / Refactor Plan (Ordered)

### Wave 1 — Core domain model (no breaking changes yet)

**Step 1.1** — Add `CompanyMember` to `UserRole` enum, remove `DepartmentManager`, `Operator`, `Viewer` from company-role positions. Keep them defined for dept-level serialization.

**Step 1.2** — Add `InvitedDepartmentRole` nullable string to `Invitation` entity.

**Step 1.3** — Add EF Core migration.

**Step 1.4** — Add data migration: transform existing `UserCompanyMembership` rows where `CompanyRole = "DepartmentManager"` → `CompanyRole = "CompanyMember"`, and ensure their `UserDepartmentMembership` rows have `DepartmentRole = "DepartmentManager"`. Transform `Operator` → `"CompanyMember"` + `DepartmentRole = "DepartmentOperator"`. Transform `Viewer` → `"CompanyMember"` + `DepartmentRole = "DepartmentOperator"`.

---

### Wave 2 — Backend authorization logic

**Step 2.1 — `Roles.cs`:**
```csharp
public enum UserRole
{
    // Platform
    SuperAdmin       = 1,
    InternalOperator = 2,

    // Company
    CompanyOwner = 10,
    CompanyAdmin = 11,
    CompanyMember = 12,   // NEW — was: DepartmentManager=20, Operator=30, Viewer=31

    // Department (stored in UserDepartmentMembership.DepartmentRole only, never in JWT Role claim)
    DepartmentManager  = 20,  // persisted to UserDepartmentMembership only
    DepartmentOperator = 30,  // was: Operator
}
```

New extension methods:
```csharp
public static bool IsCompanyMemberOrAbove(this UserRole role) =>   // for basic auth
    role.IsCompanyAdminOrAbove() || role == UserRole.CompanyMember;

// Dept role parsing — for UserDepartmentMembership.DepartmentRole string column
public static UserRole ParseDeptRole(string? value) => value switch
{
    "DepartmentManager"  => UserRole.DepartmentManager,
    "Manager"            => UserRole.DepartmentManager,  // legacy alias
    "DepartmentOperator" => UserRole.DepartmentOperator,
    "Member"             => UserRole.DepartmentOperator,  // legacy alias
    _                    => UserRole.DepartmentOperator
};

public static string ToDepartmentRoleValue(this UserRole role) =>
    role == UserRole.DepartmentManager ? "DepartmentManager" : "DepartmentOperator";

// Keep CompanyRoles updated:
public static readonly IReadOnlyList<UserRole> CompanyRoles = [
    UserRole.CompanyOwner,
    UserRole.CompanyAdmin,
    UserRole.CompanyMember,
];

// Invite assignable roles:
public static bool CanAssignDepartmentRole(this UserRole caller, UserRole target)
{
    if (!target.IsDepartmentScopedRole()) return false;
    if (caller.IsPlatformAdmin() || caller.IsCompanyAdmin()) return true;
    // DeptManager can assign DeptOperator (not DeptManager to themselves, but they can assign Manager)
    return caller == UserRole.CompanyMember; // will be validated by dept membership check
}

public static bool IsDepartmentScopedRole(this UserRole role) =>
    role is UserRole.DepartmentManager or UserRole.DepartmentOperator;
```

Remove `Viewer` from enum entirely (it was `= 31`, replace all usages with `DepartmentOperator`).

**Step 2.2 — `ScopeContext.cs`:**
```csharp
// OLD: company-wide for CompanyAdminOrAbove
// NEW: company-wide for CompanyOwner and CompanyAdmin; CompanyMember always filters by depts
private static async Task<(bool companyWide, List<Guid> deptIds)> ResolveAsync(
    UserRole role, Guid userId, Guid companyId, AppDbContext db)
{
    if (role.IsPlatformAdmin() || role.IsCompanyAdmin())  // CompanyOwner, CompanyAdmin
        return (true, new List<Guid>());

    // CompanyMember: always filter by assigned departments
    var deptIds = await db.UserDepartmentMemberships...
    return (false, deptIds);
}
```

**Step 2.3 — `AccessResolver.cs` — fix fallback grant:**
```csharp
// OLD fallback:
if (role is UserRole.Operator or UserRole.DepartmentManager)
    return new ModulePermissionResult(true, true, false, ...);

// NEW fallback: check if user has active dept membership (not role-based):
var hasDeptAccess = departmentIds.Count > 0;
if (hasDeptAccess)
{
    // Check if any of the user's depts has DeptManager role for richer access
    var isDeptManager = await _db.UserDepartmentMemberships
        .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId
                    && m.Status == "Active" && departmentIds.Contains(m.DepartmentId)
                    && m.DepartmentRole == "DepartmentManager");
    return isDeptManager
        ? new ModulePermissionResult(true, true, false, false, false, false, false, false)
        : new ModulePermissionResult(true, false, false, false, false, false, false, false);
}
return Empty();
```

**Step 2.4 — `MintJwtAsync` in `AuthService.cs`:**
```csharp
// Company role → JWT claim (company-level decisions only)
// DepartmentManager/Operator/Viewer no longer emitted as JWT Role claim
var roleClaimValue = parsedUserRole.IsPlatformAdmin()
    ? parsedUserRole.ToClaimValue()
    : string.Empty;

if (activeCompanyId.HasValue)
{
    if (!parsedUserRole.IsPlatformAdmin())
    {
        var companyRole = await _db.UserCompanyMemberships...
            .Select(m => m.CompanyRole).FirstOrDefaultAsync();
        roleClaimValue = UserRoleExtensions.ParseRole(companyRole).ToClaimValue();
        // This will now emit "CompanyMember", "CompanyAdmin", or "CompanyOwner"
    }
}
```

**Step 2.5 — `TeamController.cs` — full rewrite of authorization:**

```csharp
// GET /api/team
// CompanyAdminOrAbove: all members. CompanyMember: check if DeptManager in any dept, return scoped list
[HttpGet]
public async Task<IActionResult> GetTeamMembers()
{
    var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
    if (!callerRole.IsCompanyMemberOrAbove()) return Forbid();

    var companyId = GetActiveCompanyId();
    if (companyId == null) return Unauthorized();

    if (callerRole.IsCompanyAdminOrAbove())
    {
        // unchanged — return all members
    }
    else
    {
        // CompanyMember path: check if they are DeptManager anywhere
        var callerUserId = GetCurrentUserId();
        var callerManagedDeptIds = await _db.UserDepartmentMemberships
            .Where(m => m.UserId == callerUserId && m.CompanyId == companyId
                     && m.Status == "Active" && m.DepartmentRole == "DepartmentManager")
            .Select(m => m.DepartmentId).ToListAsync();

        if (callerManagedDeptIds.Count == 0) return Forbid();

        // Return members who share at least one managed dept
        var visibleUserIds = await _db.UserDepartmentMemberships
            .Where(m => m.CompanyId == companyId && m.Status == "Active"
                     && callerManagedDeptIds.Contains(m.DepartmentId))
            .Select(m => m.UserId).Distinct().ToListAsync();

        // Return only those members with their dept roles in managed depts
        ...
    }
}

// PUT /api/team/{id}/role — still CompanyAdminOrAbove only (company-level role change)
// DELETE /api/team/{id} — still CompanyAdminOrAbove only (remove from company)

// NEW: PUT /api/team/{id}/department-role — change dept-level role within a dept
[HttpPut("{userId}/department-role")]
public async Task<IActionResult> UpdateDepartmentRole(Guid userId, [FromBody] UpdateDepartmentRoleDto dto)
{
    // Validate: caller must be DeptManager in dto.DepartmentId OR CompanyAdmin+
    var callerRole = ...;
    var companyId = ...;
    var callerUserId = ...;

    if (callerRole.IsCompanyAdminOrAbove())
    {
        // Full access — validate dept exists in company
    }
    else
    {
        // Must be DeptManager in the specified dept
        var isManagerInDept = await _db.UserDepartmentMemberships
            .AnyAsync(m => m.UserId == callerUserId && m.CompanyId == companyId
                        && m.DepartmentId == dto.DepartmentId
                        && m.Status == "Active" && m.DepartmentRole == "DepartmentManager");
        if (!isManagerInDept) return Forbid();
    }

    var targetMembership = await _db.UserDepartmentMemberships
        .FirstOrDefaultAsync(m => m.UserId == userId && m.CompanyId == companyId
                                && m.DepartmentId == dto.DepartmentId && m.Status == "Active");
    if (targetMembership == null) return NotFound();

    targetMembership.DepartmentRole = dto.NewDepartmentRole; // "DepartmentManager" | "DepartmentOperator"
    await _db.SaveChangesAsync();
    return Ok();
}

// NEW: POST /api/team/{id}/departments/{departmentId} — add user to a specific dept
// DELETE /api/team/{id}/departments/{departmentId} — remove user from a specific dept

// REMOVE: PUT /api/team/{id}/departments (full reconcile) — keep for CompanyAdmin+ only
```

**Step 2.6 — `InviteController.cs`:**

```csharp
[HttpPost]
public async Task<IActionResult> SendInvite([FromBody] SendInviteDto dto)
{
    var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);

    if (callerRole.IsCompanyAdminOrAbove())
    {
        // Existing logic: can invite to any role, optional dept
        // AssignedRole can be CompanyAdmin, CompanyMember
        // InvitedDepartmentRole can be set independently
    }
    else if (callerRole == UserRole.CompanyMember)
    {
        // Must be DeptManager in specified dept
        if (!dto.DepartmentId.HasValue)
            return BadRequest("DepartmentManager must specify a department.");

        var callerUserId = ...;
        var isManagerInDept = await _db.UserDepartmentMemberships
            .AnyAsync(m => m.UserId == callerUserId && m.CompanyId == companyId
                        && m.DepartmentId == dto.DepartmentId
                        && m.Status == "Active" && m.DepartmentRole == "DepartmentManager");
        if (!isManagerInDept) return Forbid();

        // Force company role to CompanyMember; set dept role from dto
        dto = dto with { AssignedRole = "CompanyMember" };
        // InvitedDepartmentRole = dto.InvitedDepartmentRole (must be DeptManager or DeptOperator)
    }
    else return Forbid();
    ...
}
```

Update `SendInviteDto`:
```csharp
public record SendInviteDto(
    string Email,
    string AssignedRole,          // CompanyMember | CompanyAdmin (company tier)
    Guid? DepartmentId = null,
    string? InvitedDepartmentRole = null  // DepartmentManager | DepartmentOperator
);
```

**Step 2.7 — `AcceptInvitationAsync` in `AuthService.cs`:**
```csharp
// After creating UserCompanyMembership with invitation.AssignedRole:
if (invitation.DepartmentId.HasValue)
{
    var deptRole = invitation.InvitedDepartmentRole ?? "DepartmentOperator";  // safe fallback
    // Create UserDepartmentMembership with deptRole
}
```

**Step 2.8 — `AuthService.InviteUserAsync`:**
```csharp
// Store InvitedDepartmentRole onto the created Invitation entity
```

**Step 2.9 — `OrganizationController.GetDepartments`:**
```csharp
// CompanyAdminOrAbove: all depts with "Admin" role label
// CompanyMember: their assigned depts with their DepartmentRole per dept
```

---

## 9. Data Migration Strategy

### Step 9.1 — EF Core schema migration (new column)

```csharp
// New nullable string column on Invitations
public string? InvitedDepartmentRole { get; set; }

// Migration will add:
// ALTER TABLE "Invitations" ADD COLUMN "InvitedDepartmentRole" text NULL;
```

### Step 9.2 — Data upgrade script (run ONCE in migration)

```sql
-- Upgrade existing DepartmentManager company memberships → CompanyMember
UPDATE "UserCompanyMemberships"
SET "CompanyRole" = 'CompanyMember'
WHERE "CompanyRole" = 'DepartmentManager';

-- Their UserDepartmentMembership rows already have DepartmentRole = 'Manager'
-- Rename to canonical new value:
UPDATE "UserDepartmentMemberships"
SET "DepartmentRole" = 'DepartmentManager'
WHERE "DepartmentRole" = 'Manager';

UPDATE "UserDepartmentMemberships"
SET "DepartmentRole" = 'DepartmentOperator'
WHERE "DepartmentRole" = 'Member';

-- Upgrade existing Operator company memberships → CompanyMember + DeptOperator
-- For users with CompanyRole = 'Operator' who have no UserDepartmentMembership rows,
-- we must create DepartmentOperator rows or assign them to the default department
UPDATE "UserCompanyMemberships"
SET "CompanyRole" = 'CompanyMember'
WHERE "CompanyRole" IN ('Operator', 'Viewer');

-- Pending invitations with AssignedRole = 'DepartmentManager' → CompanyMember + set InvitedDepartmentRole
UPDATE "Invitations"
SET "InvitedDepartmentRole" = 'DepartmentManager', "AssignedRole" = 'CompanyMember'
WHERE "AssignedRole" = 'DepartmentManager' AND "Status" = 'Pending';

UPDATE "Invitations"
SET "InvitedDepartmentRole" = 'DepartmentOperator', "AssignedRole" = 'CompanyMember'
WHERE "AssignedRole" IN ('Operator', 'Viewer') AND "Status" = 'Pending';
```

> [!WARNING]
> For `Operator` users with no `UserDepartmentMembership` rows: they become `CompanyMember` with no dept assignments. They will lose all module access. Decide whether to assign them to the default `general` department or require manual reassignment. **Recommendation:** assign to `general` dept with `DepartmentOperator` role.

### Step 9.3 — Backward compatibility for legacy invite tokens

Existing accepted/expired invitations: `InvitedDepartmentRole = null`. Harmless — they're already resolved.

---

## 10. Backend Changes Summary

| File | Change type | Impact |
|---|---|---|
| `OrvixFlow.Core/Authorization/Roles.cs` | Enum + extension methods | High — all role checks |
| `OrvixFlow.Core/Entities/Invitation.cs` | Add `InvitedDepartmentRole` | Schema change |
| `OrvixFlow.Core/Interfaces/IScopeContext.cs` | Docs update | None |
| `OrvixFlow.Infrastructure/Auth/ScopeContext.cs` | Company-wide check: `IsCompanyAdmin` not `IsCompanyAdminOrAbove` | Medium |
| `OrvixFlow.Infrastructure/Auth/AccessResolver.cs` | Fallback grant rewrite | Medium |
| `OrvixFlow.Infrastructure/Auth/AuthService.cs` | `MintJwtAsync`, `InviteUserAsync`, `AcceptInvitationAsync` | High |
| `OrvixFlow.Api/Controllers/TeamController.cs` | Full rework + new endpoints | High |
| `OrvixFlow.Api/Controllers/InviteController.cs` | Guard + DeptManager path | High |
| `OrvixFlow.Api/Controllers/OrganizationController.cs` | GetDepartments minor | Low |
| `OrvixFlow.Api/Filters/RequireModuleAttribute.cs` | Role claim parse: `CompanyMember` now reaches user-level check | Low |
| `OrvixFlow.Infrastructure/Migrations/` | New migration + data migration | Schema |
| `memory/auth.md` | Role list update | Docs |
| `memory/memory-architecture.md` | Role system section | Docs |
| `memory/memory-risks.md` | Critical roles section | Docs |

---

## 11. Frontend Changes

### `lib/org-permissions.ts`

```ts
// Company roles — for company-level guards
const COMPANY_ADMIN_ROLES = new Set(["SuperAdmin", "InternalOperator", "CompanyOwner", "CompanyAdmin"])
const COMPANY_MEMBER_OR_ABOVE = new Set([...COMPANY_ADMIN_ROLES, "CompanyMember"])

// Dept roles — for dept-level UI decisions
const DEPT_MANAGER_ROLE = "DepartmentManager"

export function canManageOrganization(role?: string | null): boolean {
  return !!role && COMPANY_ADMIN_ROLES.has(role)  // unchanged — CompanyOwner/Admin only
}

export function isCompanyMember(role?: string | null): boolean {
  return role === "CompanyMember"
}
```

For dept-specific actions (add/remove dept members, change dept role), the frontend must check `departmentRole` from the member's dept membership, NOT the session `role`.

### Session shape update

The frontend session no longer carries a meaningful role for "what can I do in this department". Instead:
- `session.user.role` = `"CompanyMember"` | `"CompanyAdmin"` | `"CompanyOwner"` (company tier)
- Department-level permissions are fetched per-department when needed (the `GET /api/org/departments` response already returns `role` per dept from `UserDepartmentMembership.DepartmentRole`)

### `organization/page.tsx`

```tsx
// "Departments" and "Team" tabs: visible to CompanyMember if they are DeptManager in any dept
// Problem: we don't know this from session.role alone
// Solution: use the departments list — if any returned dept has role="DepartmentManager", allow tab access
const isDeptManager = departments.some(d => d.role === "DepartmentManager")
const canAccessTeamTab = canManageOrganization(role) || isDeptManager
```

### `TeamTab.tsx`

- "Invite Member" button: show for CompanyAdmin+ AND DeptManager (scoped to own depts)
- "Remove from company" button: only for CompanyAdmin+
- "Change company role" dropdown: only for CompanyAdmin+
- "Change dept role" / "Edit depts": for DeptManager within their managed depts
- Pending invites: filter by dept if the caller is DeptManager

### `DepartmentsTab.tsx`

- "Create department" / "Edit" / "Delete": only for CompanyAdmin+
- Dept member list: show for DeptManager in that dept
- "Add user to dept" / "Remove user from dept": for DeptManager in that dept

---

## 12. Tests to Add or Update

### Delete / update

- `RoleCeilingTests.DepartmentManager_CannotInvite_ForbidAtAdminCheck` → **Delete** (now allowed for DeptManager with dept)
- `FileIngestionControllerTests` — update role setup to `CompanyRole=CompanyMember + DepartmentRole=DepartmentManager`
- `ScopeContextTests.InitializeAsync_ShouldResolveScope_ForDepartmentManager` → update expected company-wide = false, deptIds = correct list

### New xUnit tests

```
RoleModelTests:
  - ParseRole_CompanyMember_ReturnsCompanyMember
  - ParseRole_LegacyOperator_ReturnsCompanyMember (migration alias)
  - ParseDeptRole_DepartmentManager_Returns_DepartmentManager
  - ParseDeptRole_LegacyManager_Returns_DepartmentManager
  - IsCompanyMemberOrAbove_CompanyMember_ReturnsTrue
  - IsCompanyMemberOrAbove_Viewer_ReturnsFalse

TeamControllerTests:
  - GetTeam_CompanyMember_NoDeptManager_Returns403
  - GetTeam_CompanyMember_IsDeptManager_ReturnsOwnDeptMembers
  - UpdateDepartmentRole_CompanyMember_DeptManager_OwnDept_Succeeds
  - UpdateDepartmentRole_CompanyMember_DeptManager_OtherDept_Returns403
  - AddUserToDept_CompanyMember_DeptManager_Succeeds
  - RemoveUserFromDept_CompanyMember_DeptManager_Succeeds
  - RemoveUserFromCompany_CompanyMember_Returns403

InviteControllerTests:
  - SendInvite_CompanyMember_NoDept_Returns400
  - SendInvite_CompanyMember_IsDeptManager_OwnDept_Succeeds
  - SendInvite_CompanyMember_IsDeptManager_OtherDept_Returns403
  - AcceptInvite_CreatesUserCompanyMembership_CompanyMember
  - AcceptInvite_CreatesDeptMembership_WithCorrectDeptRole

DataMigrationTests:
  - ExistingDepartmentManagerRows_MigratedTo_CompanyMemberWithDeptManager
  - ExistingOperatorRows_MigratedTo_CompanyMemberWithDeptOperator
  - PendingInvites_DepartmentManager_MigratedWithInvitedDepartmentRole

AccessResolverTests:
  - CompanyMember_DeptOperator_HasViewAccess_WhenEntitled
  - CompanyMember_DeptManager_HasUseAccess_WhenEntitled
  - CompanyMember_NoDeptMembership_Returns403
```

### Frontend Vitest tests

```
org-permissions.test.ts:
  - canManageOrganization("CompanyMember") returns false (company admin actions)
  - isCompanyMember("CompanyMember") returns true

TeamTab.test.tsx:
  - renders invite form for CompanyMember when isDeptManager
  - hides remove-from-company for CompanyMember
  - shows change-dept-role for DeptManager's dept
  - hides change-company-role dropdown for CompanyMember
```

---

## 13. Validation Checklist

After implementation, manually verify:

- [ ] Existing CompanyOwner login → JWT `Role = "CompanyOwner"`, sees all members, all depts, can invite anyone
- [ ] Existing CompanyAdmin login → JWT `Role = "CompanyAdmin"`, sees all, can manage, cannot set Owner
- [ ] Migrated DepartmentManager (now CompanyMember) login → JWT `Role = "CompanyMember"`, access Departments tab, sees only own depts
- [ ] CompanyMember who is DeptManager in Dept A: can see Dept A members in Team tab
- [ ] CompanyMember who is DeptManager in Dept A: CANNOT see Dept B members
- [ ] CompanyMember who is DeptManager in Dept A: can invite user to Dept A, gets 400 if no dept specified
- [ ] CompanyMember who is DeptManager in Dept A: cannot invite to Dept B
- [ ] CompanyMember who is DeptOperator: cannot access Team tab at all (no DeptManager role anywhere)
- [ ] User in Dept A as DeptManager, Dept B as DeptOperator: can manage Dept A, cannot manage Dept B
- [ ] Invite accepted by new user: creates `CompanyMember` + correct dept role
- [ ] Invite accepted by existing company user: creates only dept membership, doesn't change company role
- [ ] Module access (KB, Inbox): CompanyMember+DeptOperator can view/use (billing permitting)
- [ ] Module access: CompanyMember with no dept = no module access
- [ ] SuperAdmin still bypasses all checks
- [ ] `GET /api/org/departments` returns `role = "DepartmentManager"` or `"DepartmentOperator"` (not `"Manager"/"Member"`)
- [ ] Admin panel: only SuperAdmin/InternalOperator can access, confirms CompanyMember cannot

---

## 14. Risks and Phased Rollout Recommendation

### Risk Table

| Risk | Severity | Mitigation |
|---|---|---|
| JWT `Role = "CompanyMember"` breaks existing role checks throughout the API | High | Audit every `IsCompanyAdminOrAbove` call — these are all safe (blocks CompanyMember correctly). New guards needed for DeptManager paths. |
| Frontend caches old session role (`"DepartmentManager"`) | Medium | Force JWT refresh on login/switch after deploy. Session expiry = 60 min, so self-heals quickly. |
| Operator users lose dept access if not assigned to default dept | Medium | Data migration script assigns to `general` dept as DeptOperator before go-live |
| Tests asserting `"DepartmentManager"` as company role now fail | Low | Update tests before deploy |
| `Invitation.InvitedDepartmentRole = null` for old invites | Low | Safe fallback = `"DepartmentOperator"`, documents in comments |
| Stripe/UsageAlertService looks up `CompanyOwner` by `CompanyRole = "CompanyOwner"` | None | CompanyOwner unchanged, these are safe |

### Recommended Rollout Order

**Phase 1 — Schema only (no behavior change)**
1. Add `Invitation.InvitedDepartmentRole` column (nullable)
2. Add data migration SQL
3. Add `CompanyMember` to `UserRole` enum with backward-compat aliases

**Phase 2 — Backend authorization (deploy alone, no frontend changes yet)**
4. Rewrite `Roles.cs` extensions
5. Update `ScopeContext`, `AccessResolver`, `MintJwtAsync`
6. Rewrite `TeamController` + `InviteController`
7. Run full `dotnet test`

**Phase 3 — Frontend update**
8. Update session type, `org-permissions.ts`, `TeamTab`, `DepartmentsTab`
9. Deploy frontend

**Phase 4 — Memory update**
10. Update `auth.md`, `memory-architecture.md`, `memory-risks.md`

**What needs manual verification:** the data migration SQL (run against a staging DB copy first, verify row counts before/after).

**What should be phased:** Roles.cs enum change is atomic — cannot be done partially. Deploy Phase 2 as a single release.

**What to fix first:** The data migration schema change (Phase 1) must happen before any code touches the new `InvitedDepartmentRole` field.
