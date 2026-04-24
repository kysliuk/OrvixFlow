import { describe, expect, it } from "vitest"

import {
  canAccessDepartmentScopedOrganizationSettings,
  canManageMember,
  canManageOrganization,
  getAssignableCompanyRoles,
  getManagedDepartmentIds,
  isCompanyMember,
} from "./org-permissions"

describe("org permissions", () => {
  it("recognizes company admins but not company members", () => {
    expect(canManageOrganization("SuperAdmin")).toBe(true)
    expect(canManageOrganization("CompanyOwner")).toBe(true)
    expect(canManageOrganization("CompanyAdmin")).toBe(true)
    expect(canManageOrganization("CompanyMember")).toBe(false)
  })

  it("returns assignable company roles for admins", () => {
    expect(getAssignableCompanyRoles("CompanyOwner")).toEqual(["CompanyAdmin", "CompanyMember"])
    expect(getAssignableCompanyRoles("CompanyAdmin")).toEqual(["CompanyAdmin", "CompanyMember"])
    expect(getAssignableCompanyRoles("SuperAdmin")).toEqual(["CompanyAdmin", "CompanyMember"])
    expect(getAssignableCompanyRoles("CompanyMember")).toEqual([])
  })

  it("applies company target management rules", () => {
    expect(canManageMember("CompanyAdmin", "CompanyMember")).toBe(true)
    expect(canManageMember("CompanyAdmin", "CompanyAdmin")).toBe(false)
    expect(canManageMember("CompanyOwner", "CompanyAdmin")).toBe(true)
    expect(canManageMember("CompanyOwner", "CompanyOwner")).toBe(false)
    expect(canManageMember("SuperAdmin", "CompanyOwner")).toBe(true)
    expect(canManageMember("CompanyAdmin", "CompanyMember", true)).toBe(false)
  })

  it("detects company members and department-scoped settings access", () => {
    expect(isCompanyMember("CompanyMember")).toBe(true)
    expect(
      canAccessDepartmentScopedOrganizationSettings("CompanyMember", [
        { departmentId: "dept-1", role: "DepartmentManager" },
      ])
    ).toBe(true)
    expect(
      canAccessDepartmentScopedOrganizationSettings("CompanyMember", [
        { departmentId: "dept-1", role: "DepartmentOperator" },
      ])
    ).toBe(false)
  })

  it("returns only managed department ids", () => {
    expect(
      getManagedDepartmentIds([
        { departmentId: "dept-1", role: "DepartmentManager" },
        { departmentId: "dept-2", role: "DepartmentOperator" },
        { departmentId: "dept-3", role: "DepartmentManager" },
      ])
    ).toEqual(["dept-1", "dept-3"])
  })
})
