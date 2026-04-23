import { describe, expect, it } from "vitest"

import { canManageMember, canManageOrganization, getAssignableCompanyRoles } from "./org-permissions"

describe("org permissions", () => {
  it("recognizes organization admins", () => {
    expect(canManageOrganization("SuperAdmin")).toBe(true)
    expect(canManageOrganization("CompanyOwner")).toBe(true)
    expect(canManageOrganization("CompanyAdmin")).toBe(true)
    expect(canManageOrganization("DepartmentManager")).toBe(false)
  })

  it("returns assignable company roles for admins", () => {
    expect(getAssignableCompanyRoles("CompanyOwner")).toEqual([
      "CompanyAdmin",
      "DepartmentManager",
      "Operator",
      "Viewer",
    ])
    expect(getAssignableCompanyRoles("CompanyAdmin")).toEqual([
      "CompanyAdmin",
      "DepartmentManager",
      "Operator",
      "Viewer",
    ])
    expect(getAssignableCompanyRoles("SuperAdmin")).toEqual([
      "CompanyAdmin",
      "DepartmentManager",
      "Operator",
      "Viewer",
    ])
    expect(getAssignableCompanyRoles("Viewer")).toEqual([])
  })

  it("applies target management rules", () => {
    expect(canManageMember("CompanyAdmin", "Operator")).toBe(true)
    expect(canManageMember("CompanyAdmin", "CompanyAdmin")).toBe(false)
    expect(canManageMember("CompanyOwner", "CompanyAdmin")).toBe(true)
    expect(canManageMember("CompanyOwner", "CompanyOwner")).toBe(false)
    expect(canManageMember("SuperAdmin", "CompanyOwner")).toBe(true)
    expect(canManageMember("CompanyAdmin", "Viewer", true)).toBe(false)
  })
})
