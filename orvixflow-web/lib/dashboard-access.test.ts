import { describe, expect, it } from "vitest"

import {
  getBillingPageState,
  getModuleGateState,
  getOrganizationOverviewState,
  shouldFetchCompanyScopedData,
} from "./dashboard-access"

describe("dashboard access helpers", () => {
  it("marks authenticated no-org organization overview as safe", () => {
    expect(
      getOrganizationOverviewState({
        hasOrganization: false,
        isLoading: false,
        companyCount: 0,
      })
    ).toEqual({
      showNoOrgBanner: true,
      showCreateOrganizationCta: true,
    })
  })

  it("short-circuits module gate for no-org sessions", () => {
    expect(
      getModuleGateState({
        status: "authenticated",
        activeCompanyId: null,
        permissions: null,
        limitStatus: null,
        hasActiveLimit: true,
      })
    ).toMatchObject({
      kind: "no-org",
      title: "No organization selected",
    })
  })

  it("treats billing pages without an active company as no-org views", () => {
    expect(getBillingPageState(null)).toMatchObject({
      kind: "no-org",
      ctaHref: "/organization",
    })
  })

  it("skips company-scoped fetches without an active company", () => {
    expect(shouldFetchCompanyScopedData("token", null)).toBe(false)
    expect(shouldFetchCompanyScopedData("token", "company-1")).toBe(true)
  })
})
