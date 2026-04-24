export type DepartmentScopedMembership = {
  departmentId: string
  role: string
}

const COMPANY_ADMIN_ROLES = new Set(["SuperAdmin", "InternalOperator", "CompanyOwner", "CompanyAdmin"])
const DEPARTMENT_MANAGER_ROLE = "DepartmentManager"

const MANAGEABLE_TARGET_ROLES = {
  SuperAdmin: new Set(["CompanyOwner", "CompanyAdmin", "CompanyMember"]),
  InternalOperator: new Set(["CompanyOwner", "CompanyAdmin", "CompanyMember"]),
  CompanyOwner: new Set(["CompanyAdmin", "CompanyMember"]),
  CompanyAdmin: new Set(["CompanyMember"]),
} as const

const ASSIGNABLE_COMPANY_ROLES = {
  SuperAdmin: ["CompanyAdmin", "CompanyMember"],
  InternalOperator: ["CompanyAdmin", "CompanyMember"],
  CompanyOwner: ["CompanyAdmin", "CompanyMember"],
  CompanyAdmin: ["CompanyAdmin", "CompanyMember"],
} as const

export function canManageOrganization(role?: string | null): boolean {
  return !!role && COMPANY_ADMIN_ROLES.has(role)
}

export function isCompanyMember(role?: string | null): boolean {
  return role === "CompanyMember"
}

export function isDepartmentManager(role?: string | null): boolean {
  return role === DEPARTMENT_MANAGER_ROLE
}

export function getManagedDepartmentIds(departments: DepartmentScopedMembership[] = []): string[] {
  return departments.filter((department) => isDepartmentManager(department.role)).map((department) => department.departmentId)
}

export function canAccessDepartmentScopedOrganizationSettings(
  role?: string | null,
  departments: DepartmentScopedMembership[] = []
): boolean {
  return canManageOrganization(role) || getManagedDepartmentIds(departments).length > 0
}

export function getAssignableCompanyRoles(role?: string | null): string[] {
  if (!role) return []
  return [...(ASSIGNABLE_COMPANY_ROLES[role as keyof typeof ASSIGNABLE_COMPANY_ROLES] ?? [])]
}

export function canManageMember(callerRole?: string | null, targetRole?: string | null, isSelf = false): boolean {
  if (!callerRole || !targetRole || isSelf) return false
  const manageableRoles = MANAGEABLE_TARGET_ROLES[callerRole as keyof typeof MANAGEABLE_TARGET_ROLES]
  return !!manageableRoles?.has(targetRole)
}
