# Company Admin Permission Audit And Fix Plan

## Execution Status

**Done:**
- [x] Initial audit of current permissions and capabilities.
- [x] Interim UI module access fix applied to `AccessResolver` to ensure non-admins at least inherit basic Plan entitlements.

**Pending (High Priority):**
- [ ] Fix `UserRoleExtensions.IsHigherThan` which currently assumes higher integer values = higher privilege.
- [ ] Fix `TeamController.UpdateRole` to use corrected role hierarchy.
- [ ] Implement explicit member removal endpoint.
- [ ] Ensure Admin queries use `IgnoreQueryFilters` securely.

## 1. Clarifying Questions

- None required to identify the current backend/frontend behavior and produce a concrete fix plan.
- One product-level policy point remains open:
  - Should `CompanyOwner` be allowed to create or promote another `CompanyOwner`, or should owner-role assignment be restricted to bootstrap/manual platform actions only?
  - The current system is inconsistent on this.
  - I suggest that CompanyOwner should not be able to create or promote another CompanyOwner. This should be restricted to bootstrap/manual platform actions only.

## 2. Context Summary From Memory

- OrvixFlow uses a strict two-layer role model:
  - Global/platform roles live in `User.Role`: `SuperAdmin`, `InternalOperator`, or empty.
  - Company roles live in `UserCompanyMembership.CompanyRole`: `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer`.
- JWT `Role` claim contains:
  - global role for platform admins
  - otherwise active company role
- Backend is the security boundary; frontend guards are defense-in-depth only.
- Tenant isolation is enforced by EF query filters and `ITenantProvider`.
- Relevant memory rules:
  - Never write company roles into `User.Role`.
  - Never compare `User.Role` against company-role values.
  - Company membership and invite flows are auth-sensitive.
  - Department managers are intended to manage "within assigned departments", but memory does not define a complete org-management action matrix for them.
- Feature ownership from memory:
  - `TeamController` owns team members
  - `OrganizationController` owns departments
  - `InviteController` owns invites
- Test guidance exists for org hierarchy and scope, but not much for member-management mutations.

## 3. Current Company Admin Capability Analysis

Implemented reality from code:

### Backend capabilities available today for `CompanyAdmin`

- Can view team members:
  - `GET /api/team`
  - `OrvixFlow.Api/Controllers/TeamController.cs:23-52`
- Can view pending invites:
  - `GET /api/invite`
  - `OrvixFlow.Api/Controllers/InviteController.cs:29-55`
- Can send invites:
  - `POST /api/invite`
  - `InviteController.cs:83-133`
- Can revoke invites:
  - `DELETE /api/invite/{id}`
  - `InviteController.cs:57-79`
- Can create/update/delete departments:
  - `POST/PUT/DELETE /api/org/departments`
  - `OrganizationController.cs:182-239`
- Can view all company departments:
  - `GET /api/org/departments`
  - `OrganizationController.cs:143-180`

### What Company Admin cannot do today

- Remove a user from a company:
  - No backend endpoint exists.
  - No frontend action exists.
- Assign a user to a department after the user exists:
  - No backend endpoint exists.
- Reassign a user to another department:
  - No backend endpoint exists.
- Remove a user from a department:
  - No backend endpoint exists.
- Reliably change a user's company role:
  - Endpoint exists, but the authorization logic is broken.
  - `TeamController.UpdateRole` is effectively inverted.
  - `TeamController.cs:55-90`

### What fails in `PUT /api/team/{userId}/role`

- The code forbids role changes using inverted comparisons:
  - `if (newRole >= callerRole) return Forbid(...)`
  - `if (targetRole >= callerRole) return Forbid(...)`
  - `TeamController.cs:72-83`
- Because lower enum number means higher privilege, these checks block nearly every legitimate subordinate update.
- Example:
  - `CompanyAdmin` = 11
  - `Viewer` = 31
  - `31 >= 11` is true, so demoting/promoting to lower-privilege company roles is incorrectly denied.
- Same issue for `CompanyOwner`.

### More severe backend issue nearby

- `UpdateRole` validates against `UserRoleExtensions.AllRoles`, which includes platform roles:
  - `SuperAdmin`, `InternalOperator`
  - `Roles.cs:80-86`
- It then writes the selected value directly into `UserCompanyMembership.CompanyRole`:
  - `membership.CompanyRole = newRole.ToClaimValue();`
  - `TeamController.cs:85`
- Since `MintJwtAsync` parses `CompanyRole` into the JWT role claim for normal users:
  - `AuthService.cs:285-301`
- This means `UpdateRole` can corrupt company-role data with platform-role strings.
- Combined with the inverted comparisons, this is not just "Company Admin can't update roles"; it is a broken and potentially dangerous role-mutation path.

## 4. Similar Permission Issues Found

### Frontend shows management UI to users who are not allowed to use it

- `settings/page.tsx` gates Team and Departments tabs only on `hasOrganization`, not role:
  - `orvixflow-web/app/(dashboard)/settings/page.tsx:534-539`
- `TeamTab` always renders:
  - invite form
  - revoke invite action
  - role dropdowns
  - `orvixflow-web/components/settings/TeamTab.tsx:150-287`
- `DepartmentsTab` always renders:
  - create/edit/delete department UI
  - `orvixflow-web/components/settings/DepartmentsTab.tsx:120-254`
- Backend then rejects non-admin users with `403`.
- Result: frontend/backend mismatch, confusing UX, and false appearance of permission bugs.

### Frontend hides supported capability

- Backend invite API supports `DepartmentId` on invite:
  - `InviteController.cs:119-125`
  - `IAuthService.cs:40-46`
- Frontend invite form does not expose department selection:
  - `TeamTab.tsx:71-103`
- So department-scoped invite assignment exists in backend but is not usable in the current UI.

### Backend returns data the frontend ignores

- Team API returns `departmentIds`:
  - `TeamController.cs:43-48`
- Pending invite API returns department-related info only indirectly via entity but not surfaced in UI.
- Frontend does not display or manage member department assignments.

### Invite flow and role-update flow are inconsistent

- Invite flow allows same-rank assignment:
  - `InviteController.cs:97-100`
  - `RoleCeilingTests.cs:148-225`
- Role-update flow forbids equal-rank assignment and, due to the inverted comparison, also forbids intended subordinate updates:
  - `TeamController.cs:72-83`

### Owner/Admin boundary inconsistency

- `CompanyAdmin` can invite another `CompanyAdmin`:
  - verified by tests in `RoleCeilingTests.cs:148-161`
- But `CompanyAdmin` cannot set an existing member to `CompanyAdmin` through `UpdateRole`.
- `CompanyOwner` can invite another `CompanyOwner`:
  - `RoleCeilingTests.cs:212-225`
- But `CompanyOwner` cannot set an existing member to `CompanyOwner` through `UpdateRole`.

### DepartmentManager boundary is underimplemented

- Memory says `DepartmentManager` manages within assigned departments.
- Actual org-management endpoints do not support that:
  - Team, invite, and department CRUD all require `IsCompanyAdminOrAbove()`
  - `DepartmentManager` is excluded.
- This may be intentional or incomplete, but implemented reality does not match the broad wording in memory.

## 5. Intended Vs Actual Permission Matrix

Assumed intended behavior is based on current code comments, memory, and existing invite tests, not a full product spec.

| Action | Should Allow | Current Behavior | Enforcement | Consistent? |
|---|---|---|---|---|
| View members | Owner, Admin | Owner/Admin allowed | `TeamController.GetTeamMembers` | Yes |
| View members | DeptManager, Operator, Viewer | Unclear/likely no | Backend denies | UI misleading |
| Change subordinate company role | Owner, Admin | Mostly denied due to inverted checks | `TeamController.UpdateRole` | No |
| Change equal-rank company role | Admin->Admin unclear, Owner->Owner unclear | Denied in role update, allowed in invite | `TeamController` vs `InviteController` | No |
| Change higher-rank company role | Deny | Current logic may allow some invalid cases due to inverted comparisons + full role list | `TeamController.UpdateRole` | No |
| Assign platform role through company role update | Deny | Backend path appears vulnerable | `TeamController.UpdateRole` + `AuthService.MintJwtAsync` | No |
| Remove member from company | Owner/Admin should likely allow | No endpoint | Missing backend/frontend | No |
| Assign member to department | Owner/Admin should likely allow | No endpoint | Missing backend/frontend | No |
| Reassign member department | Owner/Admin should likely allow | No endpoint | Missing backend/frontend | No |
| Remove member from department | Owner/Admin should likely allow | No endpoint | Missing backend/frontend | No |
| Send invite with subordinate role | Owner/Admin allow | Allowed | `InviteController.SendInvite` | Yes |
| Send invite with equal role | Admin->Admin allowed by tests; Owner->Owner allowed by tests | Allowed | `InviteController.SendInvite` | Inconsistent with role update |
| Revoke pending invite | Owner/Admin allow | Allowed | `InviteController.RevokeInvite` | Yes |
| Department CRUD | Owner/Admin allow | Allowed | `OrganizationController` | Yes |
| Department CRUD | DeptManager, Operator, Viewer deny | Denied | `OrganizationController` | UI misleading |
| View departments | Owner/Admin see all | Allowed | `OrganizationController.GetDepartments` | Yes |
| View departments | Non-admin sees assigned only | Allowed | `OrganizationController.GetDepartments` | Yes |
| Edit company name | Owner only | Owner only | `OrganizationController.UpdateCompanyName` | Yes |

### Role-specific summary

- CompanyOwner
  - Actual: can view members, manage invites, manage departments, rename company
  - Broken: cannot reliably update member roles
  - Missing: cannot remove users or manage member department assignments
- CompanyAdmin
  - Actual: can view members, manage invites, manage departments
  - Broken: cannot reliably update member roles
  - Missing: cannot remove users or manage member department assignments
- DepartmentManager
  - Actual: can only view assigned departments
  - Cannot view team, invites, or manage departments
  - UI still exposes tabs/actions misleadingly
- Regular company member (`Operator`, `Viewer`)
  - Actual: no org-management mutations
  - UI still exposes management tabs/actions misleadingly
- Global roles
  - Platform admin handling is separate and should never be written into company membership
  - `UpdateRole` currently risks violating that rule

## 6. Root Causes

### Root cause 1: Broken role hierarchy comparisons in `TeamController.UpdateRole`

- Exact cause:
  - Uses `>=` against enum values where lower value means more privilege.
  - `TeamController.cs:72-83`
- Type:
  - Backend logic bug
- Impact:
  - Company Admin and Company Owner cannot perform intended subordinate role changes.
  - May also allow invalid higher-privilege/platform-role writes in some cases.
- Severity:
  - Critical

### Root cause 2: Company role mutation endpoint accepts platform roles

- Exact cause:
  - `UpdateRole` validates against `AllRoles`, which includes `SuperAdmin` and `InternalOperator`.
  - `Roles.cs:80-86`
  - It then persists the chosen role into `UserCompanyMembership.CompanyRole`.
  - `TeamController.cs:85`
- Type:
  - Backend authorization/data-integrity bug
- Impact:
  - Role-domain corruption.
  - Potential privilege escalation via JWT role minting path.
- Severity:
  - Critical

### Root cause 3: No backend capability for member removal

- Exact cause:
  - No controller/service endpoint exists to deactivate/remove `UserCompanyMembership`.
- Type:
  - Missing backend capability
- Impact:
  - Company Admin cannot remove users from a company at all.
- Severity:
  - High

### Root cause 4: No backend capability for department assignment management

- Exact cause:
  - No endpoint exists for create/update/delete of `UserDepartmentMembership` for existing members.
- Type:
  - Missing backend capability
- Impact:
  - Company Admin cannot assign, reassign, or remove departments for members after invite acceptance.
- Severity:
  - High

### Root cause 5: Frontend exposes admin-only management UI to all org members

- Exact cause:
  - Settings tabs gated only by organization existence, not effective org-management role.
  - `settings/page.tsx:534-539`
  - `TeamTab.tsx` and `DepartmentsTab.tsx` render mutating controls unconditionally.
- Type:
  - Frontend UX/authorization consistency bug
- Impact:
  - Users see actions backend will reject.
  - Creates false bug reports and confusing behavior.
- Severity:
  - Medium

### Root cause 6: Frontend omits department-aware invite capability that backend already supports

- Exact cause:
  - `InviteController` accepts `DepartmentId`, but `TeamTab` does not send one.
- Type:
  - Frontend feature gap
- Impact:
  - Partial backend capability is unreachable in normal UI.
- Severity:
  - Medium

### Root cause 7: DepartmentManager org-management semantics are underdefined/underimplemented

- Exact cause:
  - Memory describes DepartmentManager as managing within assigned departments, but org-management controllers do not implement that boundary.
- Type:
  - Product/authorization-model ambiguity
- Impact:
  - Hard to know whether current denials are correct or incomplete.
- Severity:
  - Medium

## 7. Detailed Prioritized Fix Plan

### Priority 0: Lock role-domain invariants before adding new capabilities

- Fix `TeamController.UpdateRole` first.
- Restrict accepted target roles to company roles only:
  - `CompanyOwner`, `CompanyAdmin`, `DepartmentManager`, `Operator`, `Viewer`
- Explicitly reject platform roles in company role mutation endpoints.
- Replace current numeric comparisons with a helper aligned to business rules, not raw enum ordering in controller code.
- Reuse `IsHigherThan()` semantics or add a dedicated mutation rule helper with tests.
- Decide equal-rank policy explicitly:
  - If invites allow same-rank company-role assignment, role update should match.
  - If same-rank should be denied, change invite path and tests to match.
- Recommended smallest clean fix:
  - Keep same-rank behavior consistent with invite flow for now.
  - CompanyAdmin can assign `CompanyAdmin` and lower, but not `CompanyOwner`.
  - CompanyOwner can assign `CompanyOwner` and lower only if product wants multi-owner support; otherwise deny in both invite and update.

### Priority 1: Add missing backend member-management APIs

- Add company membership management endpoints, likely under `TeamController`:
  - remove/deactivate member from company
  - assign member to department
  - replace/reconcile department memberships
  - remove member from department
- Keep changes minimal:
  - operate on `UserCompanyMembership` and `UserDepartmentMembership`
  - do not redesign auth/RBAC broadly
- Recommended API set:
  - `DELETE /api/team/{userId}` or `DELETE /api/team/{userId}/membership`
  - `PUT /api/team/{userId}/departments`
  - optional `DELETE /api/team/{userId}/departments/{departmentId}`
- Enforce same-company constraints and active membership checks.
- Protect against self-removal/self-role-bricking unless explicitly intended.
- Protect owner/admin boundaries:
  - cannot remove or demote users at equal/higher effective authority unless explicitly allowed
  - likely prevent last-owner removal/demotion

### Priority 2: Align frontend with backend capabilities

- Gate Team and Departments tabs by role, not only `hasOrganization`.
- Hide/disable invite, role-edit, department create/edit/delete controls for unauthorized roles.
- Add proper department-selection UI in Team tab:
  - invite-time department assignment
  - existing member department assignment/reassignment/removal once backend exists
- Display returned `departmentIds` in member rows or replace with richer department DTOs.

### Priority 3: Normalize role rules across invite and update flows

- Use one shared company-role mutation policy for:
  - invite creation
  - role updates
  - possibly member removal constraints
- Eliminate controller-local ad hoc comparisons.
- Keep platform-role logic fully separate from company-role mutation logic.

### Priority 4: Tighten edge-case protections

- Prevent writing invalid/legacy role strings where possible.
- Add explicit last-owner safeguards if the business model expects every company to have at least one owner.
- Ensure department assignment endpoints validate:
  - department belongs to active company
  - target user belongs to active company
  - no cross-company memberships
  - no duplicate active department membership rows unless multi-department is intentionally allowed

### Priority 5: Clarify DepartmentManager scope

- Decide whether DepartmentManager should:
  - only view department data
  - or also manage operators/viewers within assigned departments
- Do not redesign now unless product requires it.
- For this fix track, recommend:
  - keep DepartmentManager denied for org-management mutations
  - document that clearly
  - revisit separately if needed

## 8. Tests To Add Or Update

### Backend tests

- Add `TeamControllerTests.cs`
- Cover role update matrix:
  - Owner -> Admin/DeptManager/Operator/Viewer
  - Admin -> DeptManager/Operator/Viewer
  - Admin cannot set Owner and Admin
  - no company-role mutation endpoint can write `SuperAdmin` or `InternalOperator`
  - cannot modify equal/higher target when policy says deny
  - can modify equal-rank target if policy says allow
- Add member removal tests:
  - CompanyAdmin removes Operator/Viewer
  - cannot remove equal/higher role if forbidden
  - self-removal behavior
  - last-owner protection if implemented
- Add department membership tests:
  - assign department
  - reassign department
  - remove department
  - cross-company department blocked
  - non-member user blocked
- Add invite/update consistency tests:
  - same rank allowed/denied consistently in both paths
- Add regression test for platform-role contamination:
  - `PUT /api/team/{userId}/role` with `SuperAdmin` must fail

### Existing tests to revisit

- `RoleCeilingTests.cs`
- `OrgHierarchyTests.cs`
- `ScopeContextTests.cs`
- Possibly `AuthEndToEndFlowTests.cs` if membership mutation affects session flows

### Frontend tests

- Add tests for Team/Departments tab gating by role.
- Add tests that non-admin users do not see mutating controls.
- Add tests for department assignment UI once implemented.
- Add tests for role dropdown options matching backend-supported company roles only.

## 9. Validation Checklist

### Backend validation

- CompanyAdmin can list members in active company.
- CompanyAdmin can change `Viewer` -> `Operator`.
- CompanyAdmin can change `Operator` -> `DepartmentManager`.
- CompanyAdmin cannot set any user to `CompanyOwner` or `Company Admin`.
- No actor can set `UserCompanyMembership.CompanyRole` to `SuperAdmin` or `InternalOperator`.
- CompanyOwner can perform intended subordinate role changes.
- Member removal works only within same company and allowed hierarchy.
- Department assignment/reassignment/removal works only within same company.
- Cross-company manipulation is rejected.
- No endpoint allows mutation without `[Authorize]` and valid company context.
- CompanyOwner able to delete it's company(IsActive = false should be added), with verification via email. 

### Frontend validation

- CompanyAdmin sees Team and Departments management controls.
- DepartmentManager/Operator/Viewer do not see unauthorized controls.
- Frontend does not offer actions backend will reject in normal flow.
- Invite UI supports department selection if backend supports it.
- Member list clearly shows available actions per role.

### Security validation

- Platform-role strings never enter `UserCompanyMembership.CompanyRole`.
- JWT `Role` claim remains correct after role changes for newly minted sessions.
- Tenant isolation still holds for all membership/department operations.
- No cross-company member or department assignment path exists.

## 10. Any Unclear Or Risky Areas

- Multi-owner policy is unclear.
  - Current invite tests allow `CompanyOwner` invites by `CompanyOwner`. - should be fixed.
  - Current company-name edit path is owner-only.
- DepartmentManager intended org-management powers are not clearly specified. - should be able to invite and remove users from his departament.
  - Memory language suggests broader authority than actual code implements.
- JWT staleness remains a systemic behavior.
  - Role changes do not retroactively revoke already`-issued access JWTs until refresh/expiry.
  - This is known architecture, not a new bug, but it affects "permission changed but still works for a while" scenarios.
- `DepartmentRole` semantics are inconsistent across bootstrap and invite acceptance:
  - bootstrap uses `"Manager"`
  - entity default uses `"Member"`
  - invite acceptance copies company role into department role
  - not the main blocker here, but it is a nearby cleanup candidate once member-department management is implemented.
