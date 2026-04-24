/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { signOut } from "next-auth/react";
import { AlertTriangle, Building, Lock, Network, Shield, Trash2, Users } from "lucide-react";

import { TeamTab } from "@/components/settings/TeamTab";
import { DepartmentsTab } from "@/components/settings/DepartmentsTab";
import { AuditLogTab } from "@/components/settings/AuditLogTab";
import { getOrganizationDataTransitionState, getOrganizationOverviewState } from "@/lib/dashboard-access";
import { canAccessDepartmentScopedOrganizationSettings, canManageOrganization } from "@/lib/org-permissions";

type OrgStatus = {
  hasOrganization: boolean;
  activeCompanyId: string | null;
  companyName: string | null;
  role: string | null;
};

type CompanySummary = {
  companyId: string;
  companyName: string;
  role: string;
  plan: string;
};

type DepartmentSummary = {
  departmentId: string;
  name: string;
  code: string;
  role: string;
};

type DeletionEligibility = {
  companyId: string;
  companyName: string;
  plan: string;
  lifecycleStatus: string;
  canDelete: boolean;
  blockers: string[];
  deletionScheduledFor: string | null;
  retentionDays: number;
};

const orgSections = [
  { id: "general", label: "General", icon: Building, gated: false },
  { id: "departments", label: "Departments", icon: Network, gated: true },
  { id: "team", label: "Team & Roles", icon: Users, gated: true },
  { id: "security", label: "Security", icon: Shield, gated: true },
] as const;

type OrgSectionId = (typeof orgSections)[number]["id"];

export default function OrganizationPage() {
  const { data: session, update } = useSession();
  const [activeSection, setActiveSection] = useState<OrgSectionId>("general");

  const [companies, setCompanies] = useState<CompanySummary[]>([]);
  const [departments, setDepartments] = useState<DepartmentSummary[]>([]);
  const [orgStatus, setOrgStatus] = useState<OrgStatus | null>(null);
  const [orgLoading, setOrgLoading] = useState(true);

  const [showCreateOrgModal, setShowCreateOrgModal] = useState(false);
  const [newOrgName, setNewOrgName] = useState("");
  const [isCreatingOrg, setIsCreatingOrg] = useState(false);
  const [createOrgError, setCreateOrgError] = useState<string | null>(null);

  const [editedCompanyName, setEditedCompanyName] = useState("");
  const [isSavingCompanyName, setIsSavingCompanyName] = useState(false);
  const [showCompanyNameConfirm, setShowCompanyNameConfirm] = useState(false);
  const [deletionEligibility, setDeletionEligibility] = useState<DeletionEligibility | null>(null);
  const [archiveConfirmationName, setArchiveConfirmationName] = useState("");
  const [isArchivingCompany, setIsArchivingCompany] = useState(false);
  const [archiveError, setArchiveError] = useState<string | null>(null);

  const apiToken = (session as any)?.apiToken;
  const activeCompanyId = session?.user?.activeCompanyId ?? null;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  useEffect(() => {
    const organizationTransitionState = getOrganizationDataTransitionState(Boolean(apiToken));
    setOrgStatus(organizationTransitionState.orgStatus);
    setCompanies(organizationTransitionState.companies);
    setDepartments(organizationTransitionState.departments);
    setOrgLoading(organizationTransitionState.orgLoading);

    if (!apiToken) return;
    const headers = { Authorization: `Bearer ${apiToken}` };

    fetch(`${apiUrl}/api/org/status`, { headers })
      .then((response) => (response.ok ? response.json() : null))
      .then((data: OrgStatus | null) => {
        setOrgStatus(data);
        setOrgLoading(false);
      })
      .catch(() => {
        setOrgStatus({ hasOrganization: false, activeCompanyId: null, companyName: null, role: null });
        setOrgLoading(false);
      });

    fetch(`${apiUrl}/api/org/companies`, { headers })
      .then((response) => (response.ok ? response.json() : []))
      .then(setCompanies)
      .catch(() => setCompanies([]));

    fetch(`${apiUrl}/api/org/departments`, { headers })
      .then((response) => (response.ok ? response.json() : []))
      .then(setDepartments)
      .catch(() => setDepartments([]));
  }, [activeCompanyId, apiToken, apiUrl]);

  useEffect(() => {
    if (!apiToken || !orgStatus?.activeCompanyId) {
      setDeletionEligibility(null);
      return;
    }

    fetch(`${apiUrl}/api/org/companies/${orgStatus.activeCompanyId}/deletion-eligibility`, {
      headers: { Authorization: `Bearer ${apiToken}` },
    })
      .then((response) => (response.ok ? response.json() : null))
      .then(setDeletionEligibility)
      .catch(() => setDeletionEligibility(null));
  }, [apiToken, apiUrl, orgStatus?.activeCompanyId]);

  const hasOrg = orgStatus?.hasOrganization === true;
  const canManageOrg = canManageOrganization(orgStatus?.role);
  const canAccessDepartmentScopedSettings = canAccessDepartmentScopedOrganizationSettings(orgStatus?.role, departments);
  const organizationOverviewState = getOrganizationOverviewState({
    hasOrganization: hasOrg,
    isLoading: orgLoading,
    companyCount: companies.length,
  });

  useEffect(() => {
    if (orgLoading) {
      return;
    }

    if (!hasOrg) {
      const currentSection = orgSections.find((section) => section.id === activeSection);
      if (currentSection?.gated) {
        setActiveSection("general");
      }
      return;
    }

    if ((activeSection === "departments" || activeSection === "team") && !canAccessDepartmentScopedSettings) {
      setActiveSection("general");
      return;
    }

    if (activeSection === "security" && !canManageOrg) {
      setActiveSection("general");
    }
  }, [activeSection, canAccessDepartmentScopedSettings, canManageOrg, hasOrg, orgLoading]);

  const handleSwitchCompany = async (companyId: string) => {
    if (!apiToken) {
      alert("Company switch failed (Not authenticated).");
      return;
    }

    try {
      const response = await fetch(`${apiUrl}/api/auth/switch-company`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ companyId }),
      });

      if (response.ok) {
        const data = await response.json();
        await update(data);
        return;
      }

      const errorData = await response.json().catch(() => ({ error: "Could not parse JSON error body." }));
      if (response.status === 401 || response.status === 403) {
        alert(`Company Access Denied: ${errorData.error || "You do not have active rights."}`);
        return;
      }

      alert(`Company switch failed: ${errorData.error || response.statusText}`);
    } catch {
      alert("Network err during switch.");
    }
  };

  const handleCreateOrganization = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!apiToken || !newOrgName.trim()) return;

    setIsCreatingOrg(true);
    setCreateOrgError(null);

    try {
      const response = await fetch(`${apiUrl}/api/org`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ name: newOrgName.trim() }),
      });

      if (response.ok) {
        const data = await response.json();
        await update(data);
        setShowCreateOrgModal(false);
        setNewOrgName("");
      } else if (response.status === 409) {
        setCreateOrgError("An organization with this name already exists.");
      } else if (response.status === 401) {
        setCreateOrgError("Your session is invalid or expired. Please refresh and log in again.");
      } else {
        setCreateOrgError("Failed to create organization. Check your network or try again.");
      }
    } catch {
      setCreateOrgError("An unexpected error occurred.");
    } finally {
      setIsCreatingOrg(false);
    }
  };

  const handleStartEditCompanyName = () => {
    if (!orgStatus?.companyName) return;
    setEditedCompanyName(orgStatus.companyName);
    setShowCompanyNameConfirm(true);
  };

  const handleConfirmCompanyNameChange = async () => {
    if (!apiToken || !orgStatus?.activeCompanyId) return;

    setIsSavingCompanyName(true);
    try {
      const response = await fetch(`${apiUrl}/api/org/companies/${orgStatus.activeCompanyId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ name: editedCompanyName.trim() }),
      });

      if (response.ok) {
        const data = await response.json();
        setOrgStatus((current) => (current ? { ...current, companyName: data.companyName } : null));
        setShowCompanyNameConfirm(false);
        setEditedCompanyName("");
        return;
      }

      const error = await response.json();
      alert(error.error || "Failed to update company name");
    } catch {
      alert("An unexpected error occurred");
    } finally {
      setIsSavingCompanyName(false);
    }
  };

  const handleArchiveCompany = async () => {
    if (!apiToken || !orgStatus?.activeCompanyId || !deletionEligibility) return;

    setIsArchivingCompany(true);
    setArchiveError(null);

    try {
      const response = await fetch(`${apiUrl}/api/org/companies/${orgStatus.activeCompanyId}/archive`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${apiToken}`,
        },
        body: JSON.stringify({ confirmationName: archiveConfirmationName }),
      });

      const data = await response.json();
      if (!response.ok) {
        setArchiveError(data.error || "Failed to archive company.");
        return;
      }

      if (data.token && data.profile) {
        await update(data);
        return;
      }

      await signOut({ callbackUrl: "/login" });
    } catch {
      setArchiveError("An unexpected error occurred while archiving the company.");
    } finally {
      setIsArchivingCompany(false);
    }
  };

  const LockedBanner = ({ sectionLabel }: { sectionLabel: string }) => (
    <div className="flex flex-col items-center justify-center gap-4 rounded-2xl border border-white/10 bg-surface px-6 py-16 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full border border-amber-500/20 bg-amber-500/10">
        <Lock className="h-7 w-7 text-amber-400" />
      </div>
      <div>
        <h3 className="mb-1 text-lg font-semibold text-white">{sectionLabel} Restricted</h3>
        <p className="max-w-md text-sm text-muted">
          {!hasOrg
            ? `You must belong to an organization before you can manage ${sectionLabel.toLowerCase()}.`
            : activeSection === "security"
              ? `Company Admin or Company Owner access is required to manage ${sectionLabel.toLowerCase()}.`
              : `Company Admin, Company Owner, or Department Manager access is required to manage ${sectionLabel.toLowerCase()}.`}
        </p>
      </div>
      <button
        onClick={() => setActiveSection("general")}
        className="rounded-lg border border-primary/20 bg-primary/10 px-4 py-2 text-sm font-medium text-primary transition-colors hover:bg-primary/20"
      >
        Go to General
      </button>
    </div>
  );

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-2 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-white">Organization</h1>
          <p className="text-sm text-muted">Manage company membership, departments, and organization-level security from one place.</p>
        </div>
      </div>

      <div className="rounded-2xl border border-white/5 bg-surface p-4 shadow-lg sm:p-6">
        <div className="mb-6 flex flex-wrap gap-2 border-b border-white/10 pb-4">
          {orgSections.map((section) => {
            const Icon = section.icon;
            const isLocked = section.id === "security"
              ? section.gated && (!hasOrg || !canManageOrg)
              : section.gated && (!hasOrg || !canAccessDepartmentScopedSettings);
            const isActive = activeSection === section.id;

            return (
              <button
                key={section.id}
                onClick={() => {
                  if (!isLocked) setActiveSection(section.id);
                }}
                disabled={isLocked}
                title={isLocked ? (!hasOrg ? "Create an organisation to unlock this section" : section.id === "security" ? "Company Admin access is required for this section" : "Company Admin or Department Manager access is required for this section") : undefined}
                className={`inline-flex items-center gap-2 rounded-xl border px-4 py-2 text-sm font-medium transition-all ${
                  isLocked
                    ? "cursor-not-allowed border-white/5 text-white/25"
                    : isActive
                      ? "border-primary/30 bg-primary/15 text-primary"
                      : "border-white/10 text-muted hover:border-white/20 hover:bg-white/5 hover:text-white"
                }`}
              >
                {isLocked ? <Lock className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
                {section.label}
              </button>
            );
          })}
        </div>

        {activeSection === "general" && (
          <div className="space-y-8">
            {organizationOverviewState.showNoOrgBanner && (
              <div className="flex items-start gap-3 rounded-xl border border-amber-500/20 bg-amber-500/10 px-4 py-3 text-sm text-amber-300">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <div>
                  <span className="mb-0.5 block font-medium">You are not part of any organisation.</span>
                  <span className="text-amber-300/80">Create a new organization or accept an invitation to unlock organization features.</span>
                </div>
              </div>
            )}

            <section>
              <div className="mb-4 flex items-center justify-between gap-3">
                <div>
                  <h2 className="text-lg font-semibold text-white">Your Companies</h2>
                  <p className="text-sm text-muted">Switch active company context and review your current organization memberships.</p>
                </div>
                {organizationOverviewState.showCreateOrganizationCta ? (
                  <button
                    onClick={() => setShowCreateOrgModal(true)}
                    className="rounded-lg border border-primary/20 bg-primary/10 px-4 py-2 text-sm font-medium text-primary transition-colors hover:bg-primary/20"
                  >
                    Create Organization
                  </button>
                ) : null}
              </div>

              {organizationOverviewState.showCreateOrganizationCta ? (
                <div className="rounded-xl border border-white/5 bg-background p-8 text-center">
                  <Building className="mx-auto mb-3 h-8 w-8 text-white/20" />
                  <p className="mb-4 text-sm text-muted">No active company memberships found.</p>
                  <button
                    onClick={() => setShowCreateOrgModal(true)}
                    className="rounded-lg border border-primary/20 bg-primary/10 px-4 py-2 text-sm font-medium text-primary transition-colors hover:bg-primary/20"
                  >
                    Create New Organization
                  </button>
                </div>
              ) : (
                <div className="space-y-3">
                  {companies.map((company) => (
                    <div key={company.companyId} className="flex flex-col gap-4 rounded-xl border border-white/5 bg-background p-4 md:flex-row md:items-center md:justify-between">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2 text-sm font-medium text-white">
                          <span className="truncate">{company.companyName}</span>
                          {company.companyId === orgStatus?.activeCompanyId ? (
                            <span className="rounded bg-primary px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white">Active</span>
                          ) : null}
                        </div>
                        <div className="mt-1 text-xs text-muted">{company.role} • {company.plan} Plan</div>
                      </div>

                      <div className="flex items-center gap-2 self-start md:self-auto">
                        {company.companyId === orgStatus?.activeCompanyId && orgStatus?.role === "CompanyOwner" ? (
                          <button
                            onClick={handleStartEditCompanyName}
                            className="rounded-md border border-white/10 px-3 py-1.5 text-xs text-white transition-colors hover:bg-white/10"
                          >
                            Edit Name
                          </button>
                        ) : null}
                        <button
                          onClick={() => handleSwitchCompany(company.companyId)}
                          disabled={company.companyId === orgStatus?.activeCompanyId}
                          className={`rounded-md border px-3 py-1.5 text-xs transition-colors ${
                            company.companyId === orgStatus?.activeCompanyId
                              ? "cursor-not-allowed border-white/5 bg-white/5 text-white/30"
                              : "border-white/10 text-white hover:bg-white/10"
                          }`}
                        >
                          {company.companyId === orgStatus?.activeCompanyId ? "Current" : "Switch"}
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </section>

            {hasOrg && (
              <section>
                <h2 className="mb-3 text-lg font-semibold text-white">Your Department Memberships</h2>
                <div className="flex flex-wrap gap-2">
                  {departments.length === 0 ? (
                    <p className="text-sm text-muted">You are not assigned to any specific departments.</p>
                  ) : (
                    departments.map((department) => (
                      <span key={department.departmentId} className="inline-flex items-center gap-1.5 rounded-md border border-white/10 bg-background px-3 py-1.5 text-xs">
                        <Network className="h-3.5 w-3.5 text-muted" />
                        <span className="font-medium text-white">{department.name}</span>
                        <span className="text-muted">({department.role})</span>
                      </span>
                    ))
                  )}
                </div>
              </section>
            )}

            {hasOrg && orgStatus?.role === "CompanyOwner" && deletionEligibility && (
              <section className="rounded-2xl border border-danger/20 bg-danger/5 p-5">
                <div className="mb-5 flex items-start gap-3">
                  <div className="mt-0.5 rounded-full border border-danger/20 bg-danger/10 p-2 text-danger">
                    <Trash2 className="h-4 w-4" />
                  </div>
                  <div>
                    <h2 className="text-lg font-semibold text-white">Danger Zone</h2>
                    <p className="mt-1 text-sm text-muted">
                      Archive this company for deletion. All data is retained for 60 days and can be restored by an admin during that period.
                    </p>
                  </div>
                </div>

                <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
                  <div className="space-y-3">
                    <div className="rounded-xl border border-white/10 bg-background px-4 py-3 text-sm text-muted">
                      <div className="font-medium text-white">Deletion requirements</div>
                      <ul className="mt-2 space-y-1">
                        <li>Only CompanyOwner can archive the company.</li>
                        <li>The company must be on the Free plan.</li>
                        <li>The company must be non-billable.</li>
                        <li>All content and memberships are retained for {deletionEligibility.retentionDays} days.</li>
                      </ul>
                    </div>

                    {deletionEligibility.blockers.length > 0 && (
                      <div className="rounded-xl border border-amber-500/20 bg-amber-500/10 px-4 py-3 text-sm text-amber-300">
                        <div className="font-medium">Deletion is currently blocked</div>
                        <ul className="mt-2 list-disc space-y-1 pl-5 text-amber-300/90">
                          {deletionEligibility.blockers.map((blocker) => (
                            <li key={blocker}>{blocker}</li>
                          ))}
                        </ul>
                      </div>
                    )}
                  </div>

                  <div className="rounded-xl border border-white/10 bg-background p-4">
                    <div className="mb-4 text-sm text-muted">
                      Type <span className="font-semibold text-white">{deletionEligibility.companyName}</span> to confirm archival.
                    </div>
                    <input
                      type="text"
                      value={archiveConfirmationName}
                      onChange={(event) => setArchiveConfirmationName(event.target.value)}
                      placeholder={deletionEligibility.companyName}
                      className="mb-3 w-full rounded-lg border border-white/10 bg-surface px-3 py-2 text-sm text-white placeholder:text-white/25 focus:border-danger/50 focus:outline-none focus:ring-1 focus:ring-danger/40"
                    />

                    {archiveError && (
                      <div className="mb-3 rounded-lg border border-danger/20 bg-danger/10 px-3 py-2 text-sm text-danger">
                        {archiveError}
                      </div>
                    )}

                    <button
                      type="button"
                      onClick={handleArchiveCompany}
                      disabled={
                        isArchivingCompany ||
                        !deletionEligibility.canDelete ||
                        archiveConfirmationName.trim() !== deletionEligibility.companyName
                      }
                      className="w-full rounded-lg bg-danger px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-danger/90 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      {isArchivingCompany ? "Archiving..." : "Archive Company"}
                    </button>
                  </div>
                </div>
              </section>
            )}
          </div>
        )}

        {activeSection === "departments" && (
          !hasOrg ? <LockedBanner sectionLabel="Departments" /> : canAccessDepartmentScopedSettings ? <DepartmentsTab currentRole={orgStatus?.role} /> : <LockedBanner sectionLabel="Departments" />
        )}
        {activeSection === "team" && (
          !hasOrg ? <LockedBanner sectionLabel="Team & Roles" /> : canAccessDepartmentScopedSettings ? <TeamTab currentRole={orgStatus?.role} /> : <LockedBanner sectionLabel="Team & Roles" />
        )}
        {activeSection === "security" && (
          !hasOrg ? <LockedBanner sectionLabel="Security" /> : canManageOrg ? <AuditLogTab /> : <LockedBanner sectionLabel="Security" />
        )}
      </div>

      {showCreateOrgModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md overflow-hidden rounded-2xl border border-white/10 bg-surface shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
              <h2 className="text-lg font-semibold text-white">Create Organization</h2>
              <button onClick={() => setShowCreateOrgModal(false)} className="text-muted transition-colors hover:text-white">&times;</button>
            </div>
            <form onSubmit={handleCreateOrganization} className="p-6">
              <p className="mb-5 text-sm text-muted">
                Organizations separate billing, users, and resources. You will automatically be assigned as the Company Owner.
              </p>
              <div className="mb-6 flex flex-col gap-2">
                <label className="text-xs font-semibold uppercase tracking-wider text-muted">Organization Name</label>
                <input
                  type="text"
                  required
                  autoFocus
                  placeholder="e.g. Acme Corp"
                  value={newOrgName}
                  onChange={(event) => setNewOrgName(event.target.value)}
                  disabled={isCreatingOrg}
                  className="rounded-lg border border-white/10 bg-background px-4 py-2.5 text-sm text-white transition-all placeholder:text-white/20 focus:border-primary/50 focus:outline-none focus:ring-1 focus:ring-primary/50"
                />
              </div>

              {createOrgError && (
                <div className="mb-6 flex items-center gap-2 rounded-lg border border-danger/20 bg-danger/10 px-4 py-3 text-sm text-danger">
                  <AlertTriangle className="h-4 w-4 shrink-0" />
                  {createOrgError}
                </div>
              )}

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateOrgModal(false)}
                  disabled={isCreatingOrg}
                  className="rounded-lg border border-white/10 px-5 py-2.5 text-sm font-medium transition-all hover:bg-white/5"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isCreatingOrg || !newOrgName.trim()}
                  className="flex items-center gap-2 rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white transition-all hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {isCreatingOrg ? (
                    <>
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-white/20 border-t-white" />
                      Creating...
                    </>
                  ) : (
                    "Create Organization"
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showCompanyNameConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md overflow-hidden rounded-2xl border border-white/10 bg-surface shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
              <h2 className="text-lg font-semibold text-white">Confirm Company Name Change</h2>
              <button onClick={() => setShowCompanyNameConfirm(false)} className="text-muted transition-colors hover:text-white">&times;</button>
            </div>
            <div className="p-6">
              <p className="mb-5 text-sm text-muted">
                Are you sure you want to change your company name? This action will update your organization across the platform.
              </p>

              <div className="mb-6 rounded-lg border border-white/5 bg-background p-4">
                <div className="mb-1 text-xs text-muted">Current Name</div>
                <div className="text-sm text-white line-through">{orgStatus?.companyName}</div>
              </div>

              <div className="mb-6 rounded-lg border border-primary/20 bg-primary/10 p-4">
                <div className="mb-1 text-xs text-primary">New Name</div>
                <input
                  type="text"
                  value={editedCompanyName}
                  onChange={(event) => setEditedCompanyName(event.target.value)}
                  placeholder="Enter new company name"
                  className="w-full rounded-lg border border-white/10 bg-background px-3 py-2 text-sm text-white placeholder:text-white/30 focus:border-primary/50 focus:outline-none"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  onClick={() => setShowCompanyNameConfirm(false)}
                  disabled={isSavingCompanyName}
                  className="rounded-lg border border-white/10 px-5 py-2.5 text-sm font-medium transition-all hover:bg-white/5"
                >
                  Cancel
                </button>
                <button
                  onClick={handleConfirmCompanyNameChange}
                  disabled={isSavingCompanyName || !editedCompanyName.trim() || editedCompanyName.trim() === orgStatus?.companyName}
                  className="flex items-center gap-2 rounded-lg bg-danger px-5 py-2.5 text-sm font-medium text-white transition-all hover:bg-danger/90 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {isSavingCompanyName ? (
                    <>
                      <div className="h-4 w-4 animate-spin rounded-full border-2 border-white/20 border-t-white" />
                      Saving...
                    </>
                  ) : (
                    "Confirm Change"
                  )}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
