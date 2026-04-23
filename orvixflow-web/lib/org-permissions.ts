const ORG_ADMIN_ROLES = new Set(["SuperAdmin", "InternalOperator", "CompanyOwner", "CompanyAdmin"])
const MANAGEABLE_TARGET_ROLES = {
  SuperAdmin: new Set(["CompanyOwner", "CompanyAdmin", "DepartmentManager", "Operator", "Viewer"]),
  InternalOperator: new Set(["CompanyOwner", "CompanyAdmin", "DepartmentManager", "Operator", "Viewer"]),
  CompanyOwner: new Set(["CompanyAdmin", "DepartmentManager", "Operator", "Viewer"]),
  CompanyAdmin: new Set(["DepartmentManager", "Operator", "Viewer"]),
} as const

const ASSIGNABLE_COMPANY_ROLES = {
  SuperAdmin: ["CompanyAdmin", "DepartmentManager", "Operator", "Viewer"],
  InternalOperator: ["CompanyAdmin", "DepartmentManager", "Operator", "Viewer"],
  CompanyOwner: ["CompanyAdmin", "DepartmentManager", "Operator", "Viewer"],
  CompanyAdmin: ["CompanyAdmin", "DepartmentManager", "Operator", "Viewer"],
} as const

export function canManageOrganization(role?: string | null): boolean {
  return !!role && ORG_ADMIN_ROLES.has(role)
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
