import { describe, expect, it } from "vitest"

import {
  getBillingDataTransitionState,
  getBillingPageState,
  getModuleGateState,
  getOrganizationDataTransitionState,
  getOrganizationOverviewState,
  getSidebarModulesTransitionState,
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

  it("clears sidebar modules immediately while switched-company data reloads", () => {
    expect(getSidebarModulesTransitionState(true)).toEqual({
      visibleModules: [],
      modulesLoaded: false,
    })

    expect(getSidebarModulesTransitionState(false)).toEqual({
      visibleModules: [],
      modulesLoaded: true,
    })
  })

  it("clears billing data while the new company scope is loading", () => {
    expect(getBillingDataTransitionState(true)).toEqual({
      subscription: null,
      plans: [],
      error: null,
      loading: true,
    })
  })

  it("clears organization-scoped state before refetching switched company data", () => {
    expect(getOrganizationDataTransitionState(true)).toEqual({
      orgStatus: null,
      companies: [],
      departments: [],
      orgLoading: true,
    })
  })
})
