"use client";

import { useSession } from "next-auth/react";
import { User, Shield, Key, Building, BellRing, Users, Network, Lock, AlertTriangle, CreditCard } from "lucide-react";
import { useEffect, useState } from "react";
import { TeamTab } from "../../../components/settings/TeamTab";
import { DepartmentsTab } from "../../../components/settings/DepartmentsTab";
import { AuditLogTab } from "../../../components/settings/AuditLogTab";
import SettingsBillingPage from "./billing/page";

type OrgStatus = {
  hasOrganization: boolean;
  activeCompanyId: string | null;
  companyName: string | null;
  role: string | null;
};

export default function SettingsPage() {
  const { data: session, update } = useSession();
  const [activeTab, setActiveTab] = useState("profile");
  const [orgActiveTab, setOrgActiveTab] = useState("general");
  
  const [companies, setCompanies] = useState<Array<{ companyId: string; companyName: string; role: string; plan: string }>>([]);
  const [departments, setDepartments] = useState<Array<{ departmentId: string; name: string; code: string; role: string }>>([]);
  const [orgStatus, setOrgStatus] = useState<OrgStatus | null>(null);
  const [orgLoading, setOrgLoading] = useState(true);

  // Profile form state
  const [displayName, setDisplayName] = useState("");
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSuccess, setProfileSuccess] = useState(false);

  // Create Org Modal State
  const [showCreateOrgModal, setShowCreateOrgModal] = useState(false);
  const [newOrgName, setNewOrgName] = useState("");
  const [isCreatingOrg, setIsCreatingOrg] = useState(false);
  const [createOrgError, setCreateOrgError] = useState<string | null>(null);

  // Edit Company Name State
  const [isEditingCompanyName, setIsEditingCompanyName] = useState(false);
  const [editedCompanyName, setEditedCompanyName] = useState("");
  const [isSavingCompanyName, setIsSavingCompanyName] = useState(false);
  const [showCompanyNameConfirm, setShowCompanyNameConfirm] = useState(false);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  // Fetch org status first – gates all org-dependent tabs
  useEffect(() => {
    if (!apiToken) return;
    const headers = { Authorization: `Bearer ${apiToken}` };

    fetch(`${apiUrl}/api/org/status`, { headers })
      .then((r) => (r.ok ? r.json() : null))
      .then((data: OrgStatus | null) => {
        setOrgStatus(data);
        setOrgLoading(false);
      })
      .catch(() => {
        setOrgStatus({ hasOrganization: false, activeCompanyId: null, companyName: null, role: null });
        setOrgLoading(false);
      });

    fetch(`${apiUrl}/api/org/companies`, { headers })
      .then((r) => r.ok ? r.json() : [])
      .then(setCompanies)
      .catch(() => setCompanies([]));

    fetch(`${apiUrl}/api/org/departments`, { headers })
      .then((r) => r.ok ? r.json() : [])
      .then(setDepartments)
      .catch(() => setDepartments([]));
  }, [apiToken]);

  // Set initial display name from session
  useEffect(() => {
    if (session?.user?.name) {
      setDisplayName(session.user.name as string);
    }
  }, [session]);

  const handleSaveProfile = async () => {
    if (!apiToken) return;
    
    setSavingProfile(true);
    setProfileError(null);
    setProfileSuccess(false);

    try {
      const res = await fetch(`${apiUrl}/api/auth/profile`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ displayName }),
      });

      if (res.ok) {
        const data = await res.json();
        await update(data);
        setProfileSuccess(true);
        setTimeout(() => setProfileSuccess(false), 3000);
      } else {
        const err = await res.json();
        setProfileError(err.error || "Failed to save profile");
      }
    } catch (err) {
      setProfileError("An unexpected error occurred");
    } finally {
      setSavingProfile(false);
    }
  };

  const hasOrg = orgStatus?.hasOrganization === true;

  const handleSwitchCompany = async (companyId: string) => {
    console.log("[DEBUG][CompanySwitch] Initiating switch-company request.", { 
      targetCompanyId: companyId, 
      hasToken: !!apiToken,
      userId: (session as any)?.user?.id || (session as any)?.user?.userId
    });

    if (!apiToken) {
      console.error("[DEBUG][CompanySwitch] Aborted: Missing API token in active session.");
      alert("Company switch failed (Not authenticated).");
      return;
    }

    try {
      const startTime = Date.now();
      const res = await fetch(`${apiUrl}/api/auth/switch-company`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ companyId }),
      });
      console.log(`[DEBUG][CompanySwitch] Received HTTP ${res.status} in ${Date.now() - startTime}ms.`);

      if (res.ok) {
        const data = await res.json();
        console.log("[DEBUG][CompanySwitch] Decoding success payload.", { 
          hasNewToken: !!data.token, 
          profileTenant: data.profile?.tenantId 
        });
        await update(data); // Silent token swap via Next-Auth
        console.log("[DEBUG][CompanySwitch] Session updated successfully natively.");
        return;
      }
      
      // Parse detailed 401/403 errors from backend JSON
      let errorData: any;
      try {
        errorData = await res.json();
      } catch {
        errorData = { error: "Could not parse JSON error body." };
      }
      console.error("[DEBUG][CompanySwitch] Request rejected.", errorData);
      
      if (res.status === 401 || res.status === 403) {
        alert(`Company Access Denied: ${errorData.error || "You do not have active rights."}`);
      } else {
        alert(`Company switch failed: ${errorData.error || res.statusText}`);
      }
    } catch (err: any) {
      console.error("[DEBUG][CompanySwitch] Catastrophic network error.", err.message);
      alert("Network err during switch.");
    }
  };

  const handleCreateOrganization = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiToken || !newOrgName.trim()) return;

    setIsCreatingOrg(true);
    setCreateOrgError(null);

    try {
      const res = await fetch(`${apiUrl}/api/org`, {
        method: "POST",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ name: newOrgName.trim() }),
      });

      if (res.ok) {
        const data = await res.json();
        await update(data); // Silent token swap via Next-Auth
        setShowCreateOrgModal(false);
        setNewOrgName("");
      } else if (res.status === 409) {
        setCreateOrgError("An organization with this name already exists.");
      } else if (res.status === 401) {
        setCreateOrgError("Your session is invalid or expired. Please refresh and log in again.");
      } else {
        setCreateOrgError("Failed to create organization. Check your network or try again.");
      }
    } catch (err) {
      setCreateOrgError("An unexpected error occurred.");
    } finally {
      setIsCreatingOrg(false);
    }
  };

  const handleStartEditCompanyName = () => {
    if (orgStatus?.companyName) {
      setEditedCompanyName(orgStatus.companyName);
      setIsEditingCompanyName(true);
    }
  };

  const handleCancelEditCompanyName = () => {
    setIsEditingCompanyName(false);
    setEditedCompanyName("");
  };

  const handleSaveCompanyNameClick = () => {
    if (editedCompanyName.trim() && editedCompanyName.trim() !== orgStatus?.companyName) {
      setShowCompanyNameConfirm(true);
    } else {
      handleCancelEditCompanyName();
    }
  };

  const handleConfirmCompanyNameChange = async () => {
    if (!apiToken || !orgStatus?.activeCompanyId) return;

    setIsSavingCompanyName(true);
    try {
      const res = await fetch(`${apiUrl}/api/org/companies/${orgStatus.activeCompanyId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ name: editedCompanyName.trim() }),
      });

      if (res.ok) {
        const data = await res.json();
        setOrgStatus(prev => prev ? { ...prev, companyName: data.companyName } : null);
        setIsEditingCompanyName(false);
        setShowCompanyNameConfirm(false);
        setEditedCompanyName("");
      } else {
        const err = await res.json();
        alert(err.error || "Failed to update company name");
      }
    } catch (err) {
      alert("An unexpected error occurred");
    } finally {
      setIsSavingCompanyName(false);
    }
  };

  const mainTabs = [
    { id: "profile",      label: "Profile",       icon: User },
    { id: "organization", label: "Organization",  icon: Building },
    { id: "billing",      label: "Billing",       icon: CreditCard },
    { id: "api-keys",     label: "API Keys",      icon: Key },
    { id: "notifications",label: "Notifications", icon: BellRing },
  ];

  const orgSubTabs = [
    { id: "general",     label: "General",     icon: Building, gated: false },
    { id: "departments", label: "Departments", icon: Network,  gated: true },
    { id: "team",        label: "Team & Roles",icon: Users,    gated: true },
    { id: "security",    label: "Security",    icon: Shield,   gated: true },
  ];

  // If we land on a gated org inner tab but have no org, reset to general
  useEffect(() => {
    if (!orgLoading && !hasOrg) {
      const currentSubTab = orgSubTabs.find(t => t.id === orgActiveTab);
      if (currentSubTab?.gated) {
        setOrgActiveTab("general");
      }
    }
  }, [orgLoading, hasOrg, orgActiveTab]);

  /** Rendered when the user tries to access an org-gated inner tab without an org */
  const OrgRequiredBanner = ({ tabLabel }: { tabLabel: string }) => (
    <div className="animate-in fade-in duration-300 flex flex-col items-center justify-center py-20 text-center gap-4">
      <div className="w-16 h-16 bg-amber-500/10 rounded-full flex items-center justify-center border border-amber-500/20">
        <Lock className="w-7 h-7 text-amber-400" />
      </div>
      <div>
        <h3 className="text-lg font-semibold text-white mb-1">{tabLabel} Unavailable</h3>
        <p className="text-sm text-muted max-w-xs">
          You must belong to an organisation before you can manage {tabLabel.toLowerCase()}.
          Create or join an organisation first.
        </p>
      </div>
      <button
        onClick={() => setOrgActiveTab("general")}
        className="mt-2 px-4 py-2 text-sm font-medium bg-primary/10 text-primary border border-primary/20 rounded-lg hover:bg-primary/20 transition-colors"
      >
        Go to General Settings
      </button>
    </div>
  );

  return (
    <div className="flex flex-col gap-6 max-w-5xl h-full">
      
      <div>
        <h1 className="text-2xl font-semibold mb-1">Settings</h1>
        <p className="text-sm text-muted">Manage your account preferences and tenant configurations.</p>
      </div>

      <div className="flex flex-col md:flex-row gap-8 mt-2 h-[600px]">
        
        {/* Vertical Tabs Navigation (Main) */}
        <div className="w-full md:w-56 flex flex-col gap-1 shrink-0">
          {mainTabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;

            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all text-left ${
                  isActive
                    ? "bg-primary text-white shadow-[0_2px_10px_var(--accent-glow)]"
                    : "text-muted hover:text-white hover:bg-surface"
                }`}
              >
                <Icon className={`w-4 h-4 ${isActive ? "text-white" : "text-muted"}`} />
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Tab Content Area */}
        <div className="flex-1 bg-surface border border-white/5 rounded-xl px-0 py-8 relative overflow-hidden shadow-lg h-full overflow-y-auto">
          
          <div className="px-8 h-full">
            {activeTab === "profile" && (
              <div className="animate-in fade-in duration-300">
                <h2 className="text-lg font-semibold mb-6">Personal Information</h2>
                
                <div className="flex flex-col gap-6">
                  <div className="flex items-center gap-6">
                    <div className="w-20 h-20 rounded-full bg-gradient-to-tr from-primary to-danger flex items-center justify-center text-3xl font-bold shadow-lg">
                      {session?.user?.name?.charAt(0) || "U"}
                    </div>
                    <button className="px-4 py-2 border border-white/10 hover:bg-white/5 rounded-lg text-sm font-medium transition-colors">
                      Upload Avatar
                    </button>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-medium text-muted">Full Name</label>
                      <input 
                        type="text" 
                        value={displayName}
                        onChange={(e) => setDisplayName(e.target.value)}
                        className="bg-background border border-white/10 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-medium text-muted">Email Address</label>
                      <input 
                        type="email" 
                        defaultValue={session?.user?.email || ""}
                        disabled
                        className="bg-white/5 border border-white/5 rounded-lg px-3 py-2 text-sm text-muted cursor-not-allowed"
                      />
                    </div>
                  </div>

                  <div className="flex flex-col gap-1.5 pt-4">
                    <label className="text-xs font-medium text-muted">Global Role</label>
                    <div className="bg-white/5 border border-white/5 rounded-lg px-3 py-2 text-sm text-white w-fit font-semibold">
                      {(session?.user as any)?.role || "Operator"}
                    </div>
                  </div>

                  <div className="border-t border-white/5 pt-6 mt-4">
                    {profileError && (
                      <div className="mb-4 px-4 py-2 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm">
                        {profileError}
                      </div>
                    )}
                    {profileSuccess && (
                      <div className="mb-4 px-4 py-2 bg-success/10 border border-success/20 rounded-lg text-success text-sm">
                        Profile updated successfully!
                      </div>
                    )}
                    <button 
                      onClick={handleSaveProfile}
                      disabled={savingProfile}
                      className="px-5 py-2.5 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all disabled:opacity-50"
                    >
                      {savingProfile ? "Saving..." : "Save Changes"}
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === "api-keys" && (
              <div className="animate-in fade-in duration-300">
                <h2 className="text-lg font-semibold mb-1">API Keys</h2>
                <p className="text-sm text-muted mb-6">Use these keys to authenticate your webhooks and n8n nodes.</p>
                
                <div className="bg-background border border-white/5 rounded-lg p-5 mb-6">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-sm font-medium">Production Key</span>
                    <span className="text-xs text-muted">Created Oct 10, 2026</span>
                  </div>
                  <div className="flex gap-3">
                    <input 
                      type="password" 
                      value="sk_live_12345abcdefghijklmnopqrstuvwxyz" 
                      readOnly
                      className="flex-1 bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm text-muted font-mono"
                    />
                    <button className="px-4 py-2 border border-white/10 hover:bg-white/5 rounded-lg text-sm font-medium transition-colors">
                      Reveal
                    </button>
                  </div>
                </div>

                <button className="px-4 py-2 bg-white/5 border border-white/10 hover:bg-white/10 text-white font-medium rounded-lg text-sm transition-all">
                  Generate New Key
                </button>
              </div>
            )}

            {activeTab === "organization" && (
              <div className="animate-in fade-in duration-300 h-full flex flex-col">
                <h2 className="text-lg font-semibold mb-1">Organization</h2>
                <p className="text-sm text-muted mb-5">Manage your organization's settings, teams, and security.</p>
                
                {/* Horizontal Sub-Tabs Navigation */}
                <div className="flex items-center gap-1 border-b border-white/10 mb-6 pb-1">
                  {orgSubTabs.map((tab) => {
                    const Icon = tab.icon;
                    const isActive = orgActiveTab === tab.id;
                    const isLocked = tab.gated && !hasOrg;

                    return (
                      <button
                        key={tab.id}
                        onClick={() => {
                          if (isLocked) return;
                          setOrgActiveTab(tab.id);
                        }}
                        title={isLocked ? "Create an organisation to unlock this section" : undefined}
                        disabled={isLocked}
                        className={`flex items-center gap-2 px-3 py-2 rounded-t-lg text-sm font-medium transition-all ${
                          isLocked
                            ? "text-white/20 cursor-not-allowed"
                            : isActive
                            ? "text-primary border-b-2 border-primary -mb-[5px] bg-primary/10"
                            : "text-muted hover:text-white"
                        }`}
                      >
                        {isLocked ? <Lock className="w-3.5 h-3.5 text-white/20" /> : <Icon className="w-3.5 h-3.5" />}
                        {tab.label}
                      </button>
                    );
                  })}
                </div>

                {/* Sub-Tab Content Area */}
                <div className="flex-1 overflow-y-auto pr-2">
                  {orgActiveTab === "general" && (
                    <div className="animate-in fade-in duration-300">
                      {!hasOrg && !orgLoading && (
                        <div className="flex items-center gap-3 mb-6 px-4 py-3 bg-amber-500/10 border border-amber-500/20 rounded-xl text-sm text-amber-300">
                          <AlertTriangle className="w-4 h-4 shrink-0" />
                          <div>
                            <span className="block font-medium mb-0.5">You are not part of any organisation.</span>
                            <span className="text-amber-300/80">Register a new organisation or receive an invitation to unlock advanced features.</span>
                          </div>
                        </div>
                      )}

                      {hasOrg && orgStatus?.companyName && (
                        <div className="mb-6">
                          <h3 className="text-sm font-medium mb-3">Organization Name</h3>
                          <div className="bg-background border border-white/5 rounded-lg p-4">
                            {isEditingCompanyName ? (
                              <div className="flex items-center gap-3">
                                <input
                                  type="text"
                                  value={editedCompanyName}
                                  onChange={(e) => setEditedCompanyName(e.target.value)}
                                  className="flex-1 bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
                                  autoFocus
                                />
                                <button
                                  onClick={handleSaveCompanyNameClick}
                                  disabled={isSavingCompanyName || !editedCompanyName.trim()}
                                  className="px-3 py-2 bg-primary hover:bg-primary/90 text-white text-sm font-medium rounded-lg transition-colors disabled:opacity-50"
                                >
                                  {isSavingCompanyName ? "Saving..." : "Save"}
                                </button>
                                <button
                                  onClick={handleCancelEditCompanyName}
                                  disabled={isSavingCompanyName}
                                  className="px-3 py-2 border border-white/10 hover:bg-white/5 text-white text-sm font-medium rounded-lg transition-colors"
                                >
                                  Cancel
                                </button>
                              </div>
                            ) : (
                              <div className="flex items-center justify-between">
                                <div className="flex items-center gap-3">
                                  <Building className="w-5 h-5 text-primary" />
                                  <span className="text-sm font-medium">{orgStatus.companyName}</span>
                                  {orgStatus?.role === "CompanyOwner" && (
                                    <button
                                      onClick={handleStartEditCompanyName}
                                      className="text-xs text-muted hover:text-white transition-colors"
                                    >
                                      Edit
                                    </button>
                                  )}
                                </div>
                              </div>
                            )}
                          </div>
                        </div>
                      )}

                      <div className="mb-6">
                        <h3 className="text-sm font-medium mb-3">Your Companies</h3>
                        {companies.length === 0 ? (
                          <div className="bg-background border border-white/5 rounded-lg p-6 text-center">
                            <Building className="w-8 h-8 text-white/20 mx-auto mb-3" />
                            <p className="text-sm text-muted mb-4">No active company memberships found.</p>
                            <button 
                              onClick={() => setShowCreateOrgModal(true)}
                              className="px-4 py-2 bg-primary/10 text-primary hover:bg-primary/20 border border-primary/20 font-medium rounded-lg text-sm transition-all"
                            >
                              Create New Organization
                            </button>
                          </div>
                        ) : (
                          <div className="grid gap-3">
                            {companies.map((company) => (
                              <div key={company.companyId} className="flex items-center justify-between bg-background border border-white/5 hover:border-white/10 transition-colors rounded-lg p-4">
                                <div>
                                  <div className="text-sm font-medium flex items-center gap-2">
                                    {company.companyName}
                                    {company.companyId === orgStatus?.activeCompanyId && (
                                      <span className="px-1.5 py-0.5 rounded text-[10px] font-bold bg-primary text-white uppercase tracking-wider">Active</span>
                                    )}
                                  </div>
                                  <div className="text-xs text-muted mt-1">{company.role} • {company.plan} Plan</div>
                                </div>
                                <button 
                                  onClick={() => handleSwitchCompany(company.companyId)} 
                                  disabled={company.companyId === orgStatus?.activeCompanyId}
                                  className={`px-3 py-1.5 text-xs border rounded-md transition-colors ${
                                    company.companyId === orgStatus?.activeCompanyId 
                                      ? "border-white/5 text-white/30 cursor-not-allowed bg-white/5" 
                                      : "border-white/10 hover:bg-white/10 text-white"
                                  }`}
                                >
                                  {company.companyId === orgStatus?.activeCompanyId ? "Current" : "Switch"}
                                </button>
                              </div>
                            ))}
                          </div>
                        )}
                      </div>

                      {hasOrg && (
                        <div className="mb-6">
                          <h3 className="text-sm font-medium mb-3">Your Department Memberships</h3>
                          <div className="flex flex-wrap gap-2">
                            {departments.length === 0 ? (
                              <p className="text-sm text-muted">You are not assigned to any specific departments.</p>
                            ) : (
                              departments.map((department) => (
                                <span key={department.departmentId} className="px-3 py-1.5 text-xs bg-surface border border-white/10 rounded-md flex items-center gap-1.5">
                                  <Network className="w-3.5 h-3.5 text-muted" />
                                  <span className="font-medium text-white">{department.name}</span>
                                  <span className="text-muted ml-1">({department.role})</span>
                                </span>
                              ))
                            )}
                          </div>
                        </div>
                      )}
                    </div>
                  )}

                  {orgActiveTab === "departments" && (
                    hasOrg ? <div className="-mx-8"><DepartmentsTab /></div> : <OrgRequiredBanner tabLabel="Departments" />
                  )}
                  {orgActiveTab === "team" && (
                    hasOrg ? <div className="-mx-8"><TeamTab /></div> : <OrgRequiredBanner tabLabel="Team & Roles" />
                  )}
                  {orgActiveTab === "security" && (
                    hasOrg ? <div className="-mx-8"><AuditLogTab /></div> : <OrgRequiredBanner tabLabel="Security" />
                  )}
                </div>
              </div>
            )}

            {activeTab === "billing" && (
              <SettingsBillingPage />
            )}

            {/* Placeholder for unimplemented tabs */}
            {activeTab === "notifications" && (
              <div className="animate-in fade-in duration-300">
                <div className="h-6 w-48 bg-white/5 rounded animate-pulse mb-6" />
                <div className="flex flex-col gap-4">
                  <div className="h-10 w-full bg-white/5 rounded animate-pulse" />
                  <div className="h-10 w-3/4 bg-white/5 rounded animate-pulse" />
                  <div className="h-10 w-full bg-white/5 rounded animate-pulse" />
                </div>
              </div>
            )}
            
          </div>
        </div>
      </div>

      {/* Create Organization Modal */}
      {showCreateOrgModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
          <div className="bg-surface border border-white/10 rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-white/10 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-white">Let's set up your new Organization</h2>
              <button 
                onClick={() => setShowCreateOrgModal(false)}
                className="text-muted hover:text-white transition-colors"
              >
                &times;
              </button>
            </div>
            <form onSubmit={handleCreateOrganization} className="p-6">
              <p className="text-sm text-muted mb-5">
                Organizations separate billing, users, and resources. You will automatically be assigned as the Company Owner.
              </p>
              
              <div className="flex flex-col gap-2 mb-6">
                <label className="text-xs font-semibold text-muted uppercase tracking-wider">
                  Organization Name
                </label>
                <input
                  type="text"
                  required
                  autoFocus
                  placeholder="e.g. Acme Corp"
                  value={newOrgName}
                  onChange={(e) => setNewOrgName(e.target.value)}
                  disabled={isCreatingOrg}
                  className="bg-background border border-white/10 rounded-lg px-4 py-2.5 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 text-white placeholder:text-white/20 transition-all"
                />
              </div>

              {createOrgError && (
                <div className="mb-6 px-4 py-3 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  {createOrgError}
                </div>
              )}

              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setShowCreateOrgModal(false)}
                  disabled={isCreatingOrg}
                  className="px-5 py-2.5 text-sm font-medium border border-white/10 hover:bg-white/5 rounded-lg transition-all"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isCreatingOrg || !newOrgName.trim()}
                  className="px-5 py-2.5 text-sm font-medium bg-primary text-white hover:bg-primary/90 focus:ring-4 focus:ring-primary/30 rounded-lg transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isCreatingOrg ? (
                    <>
                      <div className="w-4 h-4 rounded-full border-2 border-white/20 border-t-white animate-spin" />
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

      {/* Confirm Company Name Change Modal */}
      {showCompanyNameConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
          <div className="bg-surface border border-white/10 rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-white/10 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-white">Confirm Company Name Change</h2>
              <button 
                onClick={() => setShowCompanyNameConfirm(false)}
                className="text-muted hover:text-white transition-colors"
              >
                &times;
              </button>
            </div>
            <div className="p-6">
              <p className="text-sm text-muted mb-5">
                Are you sure you want to change your company name? This action will update your organization across the platform.
              </p>
              
              <div className="bg-background border border-white/5 rounded-lg p-4 mb-6">
                <div className="text-xs text-muted mb-1">Current Name</div>
                <div className="text-sm text-white line-through">{orgStatus?.companyName}</div>
              </div>
              
              <div className="bg-primary/10 border border-primary/20 rounded-lg p-4 mb-6">
                <div className="text-xs text-primary mb-1">New Name</div>
                <div className="text-sm font-medium text-white">{editedCompanyName}</div>
              </div>

              <div className="flex gap-3 justify-end pt-2">
                <button
                  onClick={() => setShowCompanyNameConfirm(false)}
                  disabled={isSavingCompanyName}
                  className="px-5 py-2.5 text-sm font-medium border border-white/10 hover:bg-white/5 rounded-lg transition-all"
                >
                  Cancel
                </button>
                <button
                  onClick={handleConfirmCompanyNameChange}
                  disabled={isSavingCompanyName}
                  className="px-5 py-2.5 text-sm font-medium bg-danger text-white hover:bg-danger/90 focus:ring-4 focus:ring-danger/30 rounded-lg transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isSavingCompanyName ? (
                    <>
                      <div className="w-4 h-4 rounded-full border-2 border-white/20 border-t-white animate-spin" />
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
