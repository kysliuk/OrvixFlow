export interface PermissionData {
  canView: boolean
  canUse: boolean
}

export interface LimitData {
  allowed: boolean
  exceededLimit?: string
  currentUsage?: number
  limit?: number
  upgradeUrl?: string
}

export type ModuleGateState =
  | { kind: "loading" }
  | { kind: "hidden" }
  | { kind: "allowed" }
  | { kind: "upgrade-required" }
  | ({ kind: "limit-exceeded" } & LimitData)
  | ({ kind: "no-org" } & NoOrgState)

export type BillingPageState =
  | { kind: "company" }
  | ({ kind: "no-org" } & NoOrgState)

export interface NoOrgState {
  title: string
  description: string
  ctaHref: string
  ctaLabel: string
}

export function hasActiveCompanyScope(activeCompanyId?: string | null): boolean {
  return typeof activeCompanyId === "string" && activeCompanyId.trim().length > 0
}

export function shouldFetchCompanyScopedData(apiToken?: string | null, activeCompanyId?: string | null): boolean {
  return Boolean(apiToken) && hasActiveCompanyScope(activeCompanyId)
}


export function getSidebarModulesTransitionState(hasCompanyScope: boolean) {
  return {
    visibleModules: [],
    modulesLoaded: !hasCompanyScope,
  }
}

export function getBillingDataTransitionState(hasCompanyScope: boolean) {
  return {
    subscription: null,
    plans: [],
    error: null,
    loading: hasCompanyScope,
  }
}

export function getOrganizationDataTransitionState(hasApiToken: boolean) {
  return {
    orgStatus: null,
    companies: [],
    departments: [],
    orgLoading: hasApiToken,
  }
}

export function getOrganizationOverviewState({
  hasOrganization,
  isLoading,
  companyCount,
}: {
  hasOrganization: boolean
  isLoading: boolean
  companyCount: number
}) {
  return {
    showNoOrgBanner: !isLoading && !hasOrganization,
    showCreateOrganizationCta: companyCount === 0,
  }
}

export function getNoOrgState(target: "dashboard" | "billing" | "settings-billing" | "module"): NoOrgState {
  switch (target) {
    case "dashboard":
      return {
        title: "No organization selected",
        description: "Join or create an organization to unlock company modules, billing, and team features.",
        ctaHref: "/organization",
        ctaLabel: "Go to Organization",
      }
    case "settings-billing":
      return {
        title: "Billing requires an active organization",
        description: "Choose or create an organization before viewing subscription details and usage metrics.",
        ctaHref: "/organization",
        ctaLabel: "Choose Organization",
      }
    case "billing":
      return {
        title: "No organization selected",
        description: "Billing is managed per organization. Choose or create an organization to review plans and subscriptions.",
        ctaHref: "/organization",
        ctaLabel: "Open Organization",
      }
    case "module":
      return {
        title: "No organization selected",
        description: "Select or create an organization before opening company-scoped modules.",
        ctaHref: "/organization",
        ctaLabel: "Choose Organization",
      }
  }
}

export function getModuleGateState({
  status,
  activeCompanyId,
  permissions,
  limitStatus,
  hasActiveLimit,
}: {
  status: string
  activeCompanyId?: string | null
  permissions: PermissionData | null
  limitStatus: LimitData | null
  hasActiveLimit: boolean
}): ModuleGateState {
  if (status === "loading") {
    return { kind: "loading" }
  }

  if (!hasActiveCompanyScope(activeCompanyId)) {
    return { kind: "no-org", ...getNoOrgState("module") }
  }

  if (permissions == null) {
    return { kind: "loading" }
  }

  if (!permissions.canView) {
    return { kind: "hidden" }
  }

  if (hasActiveLimit && limitStatus !== null && !limitStatus.allowed) {
    return { kind: "limit-exceeded", ...limitStatus }
  }

  if (permissions.canUse) {
    return { kind: "allowed" }
  }

  return { kind: "upgrade-required" }
}

export function getBillingPageState(activeCompanyId?: string | null): BillingPageState {
  if (!hasActiveCompanyScope(activeCompanyId)) {
    return { kind: "no-org", ...getNoOrgState("billing") }
  }

  return { kind: "company" }
}
