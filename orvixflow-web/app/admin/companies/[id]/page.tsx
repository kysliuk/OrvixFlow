/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Building, Users, CreditCard, AlertTriangle, CheckCircle, PauseCircle, Shield, Key, Settings, Plus, Trash2, Save, X } from "lucide-react";

interface CompanyData {
  company: {
    id: string;
    name: string;
    plan: string;
    subscriptionStatus: string;
    createdAt: string;
    userCount: number;
    members: Array<{
      id: string;
      email: string;
      role: string;
      createdAt: string;
    }>;
  };
  subscription: {
    id: string;
    status: string;
    billingInterval: string;
    currentPeriodStart: string;
    currentPeriodEnd: string;
    trialEndsAt: string | null;
    plan: {
      id: string;
      name: string;
      slug: string;
      monthlyPriceCents: number;
      maxSeats: number | null;
    };
  } | null;
  entitlements: {
    maxSeats: number | null;
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
    tokensUsedThisPeriod: number;
    apiRequestsUsedToday: number;
    storageUsedMb: number;
    knowledgeBasesCount: number;
  };
}

interface EntitlementOverride {
  hasOverride: boolean;
  id?: string;
  maxSeats?: number | null;
  maxMonthlyTokens?: number | null;
  maxApiRequestsPerDay?: number | null;
  maxStorageMb?: number | null;
  maxKnowledgeBases?: number | null;
  maxInboxMessages?: number | null;
  maxMailboxConnections?: number | null;
  note?: string;
  createdAt?: string;
  updatedAt?: string;
}

interface ModuleOverride {
  id: string;
  moduleDefinitionId: string;
  moduleKey: string;
  moduleName: string;
  isEnabled: boolean;
  note: string;
  createdAt: string;
}

interface ModuleDefinition {
  id: string;
  key: string;
  displayName: string;
  category: string;
  isActive: boolean;
}

type Tab = "overview" | "entitlements" | "modules";

export default function CompanyDetailPage() {
  const { data: session } = useSession();
  const params = useParams();
  const router = useRouter();
  const [company, setCompany] = useState<CompanyData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<Tab>("overview");

  // Override state
  const [entitlementOverride, setEntitlementOverride] = useState<EntitlementOverride | null>(null);
  const [moduleOverrides, setModuleOverrides] = useState<ModuleOverride[]>([]);
  const [availableModules, setAvailableModules] = useState<ModuleDefinition[]>([]);
  const [saving, setSaving] = useState(false);
  const [overrideForm, setOverrideForm] = useState({
    maxSeats: "", maxMonthlyTokens: "", maxApiRequestsPerDay: "",
    maxStorageMb: "", maxKnowledgeBases: "", maxInboxMessages: "",
    maxMailboxConnections: "", note: ""
  });
  const [newModuleOverride, setNewModuleOverride] = useState({ moduleId: "", isEnabled: true, note: "" });

  // Action state
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [showPlanChange, setShowPlanChange] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState("");
  const [availablePlans, setAvailablePlans] = useState<Array<{ id: string; name: string; slug: string }>>([]);

  const apiToken = (session as any)?.apiToken;
  const companyId = params.id as string;

  useEffect(() => {
    if (apiToken && companyId) {
      loadCompany();
      loadOverrides();
      loadModules();
    }
  }, [apiToken, companyId]);

  const getHeaders = () => ({
    "Authorization": `Bearer ${apiToken}`,
    "Content-Type": "application/json"
  });

  const loadCompany = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}`, {
        headers: { "Authorization": `Bearer ${apiToken}` }
      });
      if (!res.ok) throw new Error("Failed to load company");
      const data = await res.json();
      setCompany(data);
      setLoading(false);
    } catch (e: any) {
      setError(e.message);
      setLoading(false);
    }
  };

  const loadOverrides = async () => {
    try {
      const [entRes, modRes] = await Promise.all([
        fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/entitlements`, { headers: getHeaders() }),
        fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/modules`, { headers: getHeaders() })
      ]);
      if (entRes.ok) {
        const data = await entRes.json();
        setEntitlementOverride(data);
        if (data.hasOverride) {
          setOverrideForm({
            maxSeats: data.maxSeats?.toString() ?? "",
            maxMonthlyTokens: data.maxMonthlyTokens?.toString() ?? "",
            maxApiRequestsPerDay: data.maxApiRequestsPerDay?.toString() ?? "",
            maxStorageMb: data.maxStorageMb?.toString() ?? "",
            maxKnowledgeBases: data.maxKnowledgeBases?.toString() ?? "",
            maxInboxMessages: data.maxInboxMessages?.toString() ?? "",
            maxMailboxConnections: data.maxMailboxConnections?.toString() ?? "",
            note: data.note ?? ""
          });
        }
      }
      if (modRes.ok) {
        setModuleOverrides(await modRes.json());
      }
    } catch { /* ignore */ }
  };

  const loadModules = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/modules`, { headers: getHeaders() });
      if (res.ok) setAvailableModules(await res.json());
    } catch { /* ignore */ }
  };

  const handleSaveOverrides = async () => {
    setSaving(true);
    const body: Record<string, any> = { note: overrideForm.note };
    if (overrideForm.maxSeats) body.maxSeats = parseInt(overrideForm.maxSeats);
    if (overrideForm.maxMonthlyTokens) body.maxMonthlyTokens = parseInt(overrideForm.maxMonthlyTokens);
    if (overrideForm.maxApiRequestsPerDay) body.maxApiRequestsPerDay = parseInt(overrideForm.maxApiRequestsPerDay);
    if (overrideForm.maxStorageMb) body.maxStorageMb = parseInt(overrideForm.maxStorageMb);
    if (overrideForm.maxKnowledgeBases) body.maxKnowledgeBases = parseInt(overrideForm.maxKnowledgeBases);
    if (overrideForm.maxInboxMessages) body.maxInboxMessages = parseInt(overrideForm.maxInboxMessages);
    if (overrideForm.maxMailboxConnections) body.maxMailboxConnections = parseInt(overrideForm.maxMailboxConnections);

    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/entitlements`, {
      method: "PUT",
      headers: getHeaders(),
      body: JSON.stringify(body)
    });
    setSaving(false);
    loadOverrides();
  };

  const handleDeleteOverrides = async () => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/entitlements`, {
      method: "DELETE",
      headers: getHeaders()
    });
    setOverrideForm({ maxSeats: "", maxMonthlyTokens: "", maxApiRequestsPerDay: "", maxStorageMb: "", maxKnowledgeBases: "", maxInboxMessages: "", maxMailboxConnections: "", note: "" });
    loadOverrides();
  };

  const handleAddModuleOverride = async () => {
    if (!newModuleOverride.moduleId) return;
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/modules`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify({
        moduleDefinitionId: newModuleOverride.moduleId,
        isEnabled: newModuleOverride.isEnabled,
        note: newModuleOverride.note
      })
    });
    setNewModuleOverride({ moduleId: "", isEnabled: true, note: "" });
    loadOverrides();
  };

  const handleRemoveModuleOverride = async (moduleId: string) => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/modules/${moduleId}`, {
      method: "DELETE",
      headers: getHeaders()
    });
    loadOverrides();
  };

  const handleCancelSubscription = async () => {
    if (!confirm("Are you sure you want to cancel this company's subscription? This will mark it as cancelled.")) return;
    setActionLoading("cancel");
    setActionError(null);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/cancel`, {
      method: "POST",
      headers: getHeaders()
    });
    setActionLoading(null);
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      setActionError(data.error || "Failed to cancel subscription");
    } else {
      loadCompany();
    }
  };

  const handleReactivate = async () => {
    setActionLoading("reactivate");
    setActionError(null);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/reactivate`, {
      method: "POST",
      headers: getHeaders()
    });
    setActionLoading(null);
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      setActionError(data.error || "Failed to reactivate subscription");
    } else {
      loadCompany();
    }
  };

  const handleChangePlan = async () => {
    if (!selectedPlanId) return;
    setActionLoading("change-plan");
    setActionError(null);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/plan`, {
      method: "PUT",
      headers: getHeaders(),
      body: JSON.stringify({ planTemplateId: selectedPlanId })
    });
    setActionLoading(null);
    if (!res.ok) {
      const data = await res.json().catch(() => ({}));
      setActionError(data.error || "Failed to assign plan");
    } else {
      setShowPlanChange(false);
      setSelectedPlanId("");
      loadCompany();
    }
  };

  const loadPlans = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/plans`, { headers: getHeaders() });
      if (res.ok) {
        const data = await res.json();
        setAvailablePlans((data || []).filter((p: any) => p.isActive));
      }
    } catch { /* ignore */ }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "Active": return "text-success bg-success/10";
      case "Trialing": return "text-primary bg-primary/10";
      case "PastDue": return "text-warning bg-warning/10";
      case "Suspended": return "text-danger bg-danger/10";
      default: return "text-white/50 bg-white/10";
    }
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" });
  };

  const getUsagePercentage = (used: number, max: number) => {
    if (max === 0) return 0;
    return Math.min(100, (used / max) * 100);
  };

  const tabs: { key: Tab; label: string; icon: React.ReactNode }[] = [
    { key: "overview", label: "Overview", icon: <Building className="w-4 h-4" /> },
    { key: "entitlements", label: "Entitlements", icon: <Shield className="w-4 h-4" /> },
    { key: "modules", label: "Modules", icon: <Key className="w-4 h-4" /> },
  ];

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-danger animate-pulse font-mono">LOADING_COMPANY_DATA...</div>
      </div>
    );
  }

  if (error || !company) {
    return (
      <div className="flex flex-col items-center justify-center h-64 gap-4">
        <div className="text-danger">{error || "Company not found"}</div>
        <button onClick={() => router.back()} className="px-4 py-2 bg-white/10 text-white rounded-lg text-sm">
          Go Back
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 max-w-6xl h-full">
      <button onClick={() => router.back()} className="flex items-center gap-2 text-white/60 hover:text-white text-sm transition-colors w-fit">
        <ArrowLeft className="w-4 h-4" /> Back to Companies
      </button>

      <div className="flex justify-between items-start">
        <div>
          <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
            <Building className="w-6 h-6 text-danger" /> {company.company.name}
          </h1>
          <p className="text-sm text-white/60 font-mono">ID: {company.company.id}</p>
        </div>
        <span className={`px-3 py-1 rounded-full text-xs font-bold flex items-center gap-1 ${getStatusColor(company.subscription?.status || "None")}`}>
          {company.subscription?.status === "Active" && <CheckCircle className="w-3 h-3" />}
          {company.subscription?.status === "Trialing" && <AlertTriangle className="w-3 h-3" />}
          {company.subscription?.status === "Suspended" && <PauseCircle className="w-3 h-3" />}
          {company.subscription?.status || "No Subscription"}
        </span>
      </div>

      {company.subscription?.status === "Trialing" && company.subscription.trialEndsAt && (
        <div className="bg-primary/10 border border-primary/30 p-4 rounded-xl flex items-center gap-3">
          <AlertTriangle className="w-5 h-5 text-primary" />
          <div>
            <h3 className="font-medium text-primary">Trial Period</h3>
            <p className="text-sm text-white/70">Trial ends on {formatDate(company.subscription.trialEndsAt)}</p>
          </div>
        </div>
      )}

      <div className="flex gap-1 mb-2 border-b border-white/10">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.key ? "border-danger text-white" : "border-transparent text-white/50 hover:text-white"
            }`}
          >
            {tab.icon} {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "overview" && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
              <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
                <CreditCard className="w-5 h-5 text-danger" /> Subscription
              </h2>
              {company.subscription ? (
                <div className="space-y-4">
                  <div className="flex justify-between items-center p-4 bg-white/5 rounded-lg">
                    <div>
                      <p className="text-sm text-white/50">Current Plan</p>
                      <p className="text-lg font-semibold text-white">{company.subscription.plan.name}</p>
                    </div>
                    <div className="text-right">
                      <p className="text-sm text-white/50">Billing</p>
                      <p className="text-white capitalize">{company.subscription.billingInterval}</p>
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="p-4 bg-white/5 rounded-lg">
                      <p className="text-sm text-white/50">Period Start</p>
                      <p className="text-white">{formatDate(company.subscription.currentPeriodStart)}</p>
                    </div>
                    <div className="p-4 bg-white/5 rounded-lg">
                      <p className="text-sm text-white/50">Period End</p>
                      <p className="text-white">{formatDate(company.subscription.currentPeriodEnd)}</p>
                    </div>
                  </div>
                </div>
              ) : (
                <p className="text-white/50">No active subscription</p>
              )}
            </div>

            <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
              <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
                <Users className="w-5 h-5 text-danger" /> Members ({company.company.userCount})
              </h2>
              <div className="space-y-2">
                {company.company.members.map(member => (
                  <div key={member.id} className="flex justify-between items-center p-3 bg-white/5 rounded-lg">
                    <div>
                      <p className="text-white">{member.email}</p>
                      <p className="text-xs text-white/50">{member.id}</p>
                    </div>
                    <span className="px-2 py-1 bg-white/10 text-white/70 text-xs rounded">{member.role}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-6">
            <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
              <h2 className="text-lg font-semibold mb-4">Usage & Limits</h2>
              <div className="space-y-4">
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-white/50">Seats</span>
                    <span className="text-white">{company.company.userCount} / {company.entitlements.maxSeats || "∞"}</span>
                  </div>
                  <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                    <div className="h-full bg-danger transition-all" style={{ width: `${getUsagePercentage(company.company.userCount, company.entitlements.maxSeats || 100)}%` }} />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-white/50">Tokens (Monthly)</span>
                    <span className="text-white">{(company.entitlements.tokensUsedThisPeriod / 1000).toFixed(1)}k / {(company.entitlements.maxMonthlyTokens / 1000).toFixed(0)}k</span>
                  </div>
                  <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                    <div className="h-full bg-primary transition-all" style={{ width: `${getUsagePercentage(company.entitlements.tokensUsedThisPeriod, company.entitlements.maxMonthlyTokens)}%` }} />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-white/50">API Requests (Today)</span>
                    <span className="text-white">{company.entitlements.apiRequestsUsedToday} / {company.entitlements.maxApiRequestsPerDay}</span>
                  </div>
                  <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                    <div className="h-full bg-success transition-all" style={{ width: `${getUsagePercentage(company.entitlements.apiRequestsUsedToday, company.entitlements.maxApiRequestsPerDay)}%` }} />
                  </div>
                </div>
                <div>
                  <div className="flex justify-between text-sm mb-1">
                    <span className="text-white/50">Knowledge Bases</span>
                    <span className="text-white">{company.entitlements.knowledgeBasesCount} / {company.entitlements.maxKnowledgeBases}</span>
                  </div>
                  <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                    <div className="h-full bg-warning transition-all" style={{ width: `${getUsagePercentage(company.entitlements.knowledgeBasesCount, company.entitlements.maxKnowledgeBases)}%` }} />
                  </div>
                </div>
              </div>
            </div>

            <div className="bg-black/40 border border-danger/20 rounded-xl p-5">
              <h2 className="text-lg font-semibold mb-4">Actions</h2>
              {actionError && (
                <div className="mb-3 p-3 bg-danger/10 border border-danger/30 rounded-lg text-sm text-danger">
                  {actionError}
                </div>
              )}
              <div className="space-y-2">
                <button
                  onClick={() => { loadPlans(); setShowPlanChange(true); setActionError(null); }}
                  className="w-full px-4 py-2 bg-danger/10 text-danger text-sm font-medium rounded-lg hover:bg-danger/20 transition-colors"
                >
                  Change Plan
                </button>
                <a
                  href={`/admin/companies/${companyId}/audit`}
                  className="block w-full px-4 py-2 bg-white/5 text-white/70 text-sm font-medium rounded-lg hover:bg-white/10 transition-colors text-center"
                >
                  View Audit Log
                </a>
                {company.subscription?.status === "Active" && (
                  <button
                    onClick={handleCancelSubscription}
                    disabled={actionLoading === "cancel"}
                    className="w-full px-4 py-2 bg-white/5 text-white/70 text-sm font-medium rounded-lg hover:bg-white/10 transition-colors disabled:opacity-50"
                  >
                    {actionLoading === "cancel" ? "Cancelling..." : "Cancel Subscription"}
                  </button>
                )}
                {company.subscription?.status === "Suspended" && (
                  <button
                    onClick={handleReactivate}
                    disabled={actionLoading === "reactivate"}
                    className="w-full px-4 py-2 bg-success/10 text-success text-sm font-medium rounded-lg hover:bg-success/20 transition-colors disabled:opacity-50"
                  >
                    {actionLoading === "reactivate" ? "Reactivating..." : "Reactivate"}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {activeTab === "overview" && showPlanChange && (
        <div className="bg-black/40 border border-danger/20 rounded-xl p-6">
          <h2 className="text-lg font-semibold mb-4">Change Plan</h2>
          <div className="flex gap-3">
            <select
              value={selectedPlanId}
              onChange={e => setSelectedPlanId(e.target.value)}
              className="flex-1 bg-white/5 border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            >
              <option value="">Select new plan...</option>
              {availablePlans.map(p => (
                <option key={p.id} value={p.id}>{p.name} ({p.slug})</option>
              ))}
            </select>
            <button
              onClick={handleChangePlan}
              disabled={actionLoading === "change-plan" || !selectedPlanId}
              className="px-4 py-2 bg-danger/20 text-danger text-sm font-medium rounded-lg hover:bg-danger/30 transition-colors disabled:opacity-50"
            >
              {actionLoading === "change-plan" ? "Changing..." : "Apply"}
            </button>
            <button
              onClick={() => { setShowPlanChange(false); setSelectedPlanId(""); }}
              className="px-4 py-2 bg-white/5 text-white/70 text-sm rounded-lg hover:bg-white/10 transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      {activeTab === "entitlements" && (
        <div className="bg-black/40 border border-danger/20 rounded-xl p-6 space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold flex items-center gap-2">
                <Shield className="w-5 h-5 text-danger" /> Entitlement Overrides
              </h2>
              <p className="text-sm text-white/50">Custom limits that supersede plan defaults. Leave blank to use plan default.</p>
            </div>
            <div className="flex gap-2">
              <button onClick={handleSaveOverrides} disabled={saving} className="flex items-center gap-2 px-4 py-2 bg-danger/20 text-danger text-sm font-medium rounded-lg hover:bg-danger/30 transition-colors disabled:opacity-50">
                <Save className="w-4 h-4" /> {saving ? "Saving..." : "Save"}
              </button>
              {entitlementOverride?.hasOverride && (
                <button onClick={handleDeleteOverrides} className="flex items-center gap-2 px-4 py-2 bg-white/5 text-white/70 text-sm font-medium rounded-lg hover:bg-white/10 transition-colors">
                  <Trash2 className="w-4 h-4" /> Remove Override
                </button>
              )}
            </div>
          </div>

          {entitlementOverride?.hasOverride && (
            <div className="bg-primary/10 border border-primary/30 p-3 rounded-lg text-sm text-primary">
              Override active since {entitlementOverride.updatedAt ? formatDate(entitlementOverride.updatedAt) : "unknown"}
              {entitlementOverride.note && (
                <span className="block text-white/70 mt-1">Note: {entitlementOverride.note}</span>
              )}
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {[
              { key: "maxSeats", label: "Max Seats" },
              { key: "maxMonthlyTokens", label: "Monthly Tokens" },
              { key: "maxApiRequestsPerDay", label: "API Requests/Day" },
              { key: "maxStorageMb", label: "Storage (MB)" },
              { key: "maxKnowledgeBases", label: "Knowledge Bases" },
              { key: "maxInboxMessages", label: "Inbox Messages/Month" },
              { key: "maxMailboxConnections", label: "Mailbox Connections" },
            ].map(field => (
              <div key={field.key}>
                <label className="text-xs font-bold uppercase tracking-widest text-white/50 mb-1 block">{field.label}</label>
                <input
                  type="number"
                  value={(overrideForm as any)[field.key]}
                  onChange={e => setOverrideForm(f => ({ ...f, [field.key]: e.target.value }))}
                  placeholder="Plan default"
                  className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
                />
              </div>
            ))}
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-widest text-white/50 mb-1 block">Note (reason for override)</label>
            <textarea
              value={overrideForm.note}
              onChange={e => setOverrideForm(f => ({ ...f, note: e.target.value }))}
              placeholder="e.g., Custom deal for enterprise customer"
              rows={2}
              className="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50 resize-none"
            />
          </div>
        </div>
      )}

      {activeTab === "modules" && (
        <div className="space-y-6">
          <div className="bg-black/40 border border-danger/20 rounded-xl p-6">
            <h2 className="text-lg font-semibold mb-4 flex items-center gap-2">
              <Key className="w-5 h-5 text-danger" /> Module Overrides
            </h2>
            <p className="text-sm text-white/50 mb-4">Grant or suppress modules for this company outside of their plan.</p>

            <div className="flex gap-3 mb-6">
              <select
                value={newModuleOverride.moduleId}
                onChange={e => setNewModuleOverride(m => ({ ...m, moduleId: e.target.value }))}
                className="flex-1 bg-white/5 border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
              >
                <option value="">Select module...</option>
                {availableModules.filter(m => !moduleOverrides.some(o => o.moduleDefinitionId === m.id)).map(m => (
                  <option key={m.id} value={m.id}>{m.displayName} ({m.key})</option>
                ))}
              </select>
              <label className="flex items-center gap-2 text-sm text-white/70">
                <input
                  type="checkbox"
                  checked={newModuleOverride.isEnabled}
                  onChange={e => setNewModuleOverride(m => ({ ...m, isEnabled: e.target.checked }))}
                  className="rounded border-white/20"
                />
                {newModuleOverride.isEnabled ? "Grant" : "Suppress"}
              </label>
              <input
                value={newModuleOverride.note}
                onChange={e => setNewModuleOverride(m => ({ ...m, note: e.target.value }))}
                placeholder="Note"
                className="bg-white/5 border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50 w-40"
              />
              <button
                onClick={handleAddModuleOverride}
                disabled={!newModuleOverride.moduleId}
                className="flex items-center gap-2 px-4 py-2 bg-danger/20 text-danger text-sm font-medium rounded-lg hover:bg-danger/30 transition-colors disabled:opacity-50"
              >
                <Plus className="w-4 h-4" /> Add
              </button>
            </div>

            <div className="space-y-2">
              {moduleOverrides.length === 0 && (
                <p className="text-white/50 text-sm text-center py-4">No module overrides configured</p>
              )}
              {moduleOverrides.map(o => (
                <div key={o.id} className="flex items-center justify-between p-3 bg-white/5 rounded-lg">
                  <div>
                    <span className="text-white font-medium">{o.moduleName}</span>
                    <span className="text-xs text-white/50 ml-2">({o.moduleKey})</span>
                    {o.note && <span className="text-xs text-white/40 ml-2">— {o.note}</span>}
                  </div>
                  <div className="flex items-center gap-3">
                    <span className={`text-xs px-2 py-0.5 rounded ${o.isEnabled ? "bg-success/10 text-success" : "bg-danger/10 text-danger"}`}>
                      {o.isEnabled ? "Granted" : "Suppressed"}
                    </span>
                    <button onClick={() => handleRemoveModuleOverride(o.moduleDefinitionId)} className="text-white/40 hover:text-danger transition-colors">
                      <X className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
