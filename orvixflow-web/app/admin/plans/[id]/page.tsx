"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, CreditCard, Users, Database, Cpu, Check, X as XIcon, Plus, Trash2 } from "lucide-react";

interface PlanDetail {
  id: string;
  name: string;
  slug: string;
  description: string;
  monthlyPriceCents: number;
  yearlyPriceCents: number;
  currency: string;
  billingInterval: string;
  maxSeats: number | null;
  isActive: boolean;
  isFree: boolean;
  isTrialAllowed: boolean;
  trialDays: number;
  legacyLocked: boolean;
  createdAt: string;
  archivedAt: string | null;
  moduleIds: string[];
  entitlements: {
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
  } | null;
}

interface ModuleDef {
  id: string;
  key: string;
  displayName: string;
  description: string;
  category: string;
  isActive: boolean;
  isPremium: boolean;
}

interface Company {
  id: string;
  name: string;
  planId: string;
  planName: string;
  status: string;
  seatCount: number;
}

type Tab = "overview" | "modules" | "companies";

export default function PlanDetailPage() {
  const { id } = useParams();
  const router = useRouter();
  const { data: session } = useSession();
  const [plan, setPlan] = useState<PlanDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<Tab>("overview");
  const [availableModules, setAvailableModules] = useState<ModuleDef[]>([]);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [addingModule, setAddingModule] = useState<string>("");
  const [saving, setSaving] = useState(false);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  useEffect(() => {
    if (!apiToken || !id) return;

    const planId = id as string;

    fetch(`${apiUrl}/api/plans/${planId}`, {
      headers: { Authorization: `Bearer ${apiToken}` }
    })
      .then(res => {
        if (!res.ok) throw new Error("Failed to load plan");
        return res.json();
      })
      .then(data => {
        setPlan(data);
        setLoading(false);
      })
      .catch(e => {
        setError(e.message);
        setLoading(false);
      });

    fetch(`${apiUrl}/api/admin/modules`, {
      headers: { Authorization: `Bearer ${apiToken}` }
    })
      .then(res => res.json())
      .then(data => {
        setAvailableModules((data.modules || []).filter((m: ModuleDef) => m.isActive));
      })
      .catch(() => {});

    fetch(`${apiUrl}/api/admin/companies`, {
      headers: { Authorization: `Bearer ${apiToken}` }
    })
      .then(res => res.json())
      .then(data => {
        setCompanies(data.companies || []);
      })
      .catch(() => {});
  }, [apiToken, id]);

  const handleAddModule = async () => {
    if (!addingModule || !plan || !apiToken) return;
    setSaving(true);
    try {
      const res = await fetch(`${apiUrl}/api/plans/${plan.id}/modules/${addingModule}`, {
        method: "POST",
        headers: { Authorization: `Bearer ${apiToken}` }
      });
      if (!res.ok) throw new Error("Failed to add module");
      const updated = await fetch(`${apiUrl}/api/plans/${plan.id}`, {
        headers: { Authorization: `Bearer ${apiToken}` }
      }).then(r => r.json());
      setPlan(updated);
      setAddingModule("");
    } catch (e: any) {
      alert(e.message);
    } finally {
      setSaving(false);
    }
  };

  const handleRemoveModule = async (moduleId: string) => {
    if (!plan || !apiToken) return;
    try {
      const res = await fetch(`${apiUrl}/api/plans/${plan.id}/modules/${moduleId}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${apiToken}` }
      });
      if (!res.ok) throw new Error("Failed to remove module");
      setPlan({ ...plan, moduleIds: plan.moduleIds.filter((m: string) => m !== moduleId) });
    } catch (e: any) {
      alert(e.message);
    }
  };

  const formatPrice = (cents: number, interval?: string) => {
    if (cents === 0 && interval === "Custom") return "Custom";
    return `$${(cents / 100).toFixed(0)}`;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-danger animate-pulse font-mono">LOADING_PLAN...</div>
      </div>
    );
  }

  if (!plan) {
    return (
      <div className="flex flex-col items-center justify-center h-64">
        <p className="text-white/60 mb-4">Plan not found</p>
        <button onClick={() => router.push("/admin/plans")} className="px-4 py-2 bg-danger text-white rounded-lg text-sm">
          Back to Plans
        </button>
      </div>
    );
  }

  const planCompanies = companies.filter((c: Company) => c.planId === plan.id);
  const availableToAdd = availableModules.filter(m => !plan.moduleIds.includes(m.id));

  const tabs: { key: Tab; label: string; icon: React.ReactNode }[] = [
    { key: "overview", label: "Overview", icon: <CreditCard className="w-4 h-4" /> },
    { key: "modules", label: `Modules (${plan.moduleIds.length})`, icon: <Database className="w-4 h-4" /> },
    { key: "companies", label: `Companies (${planCompanies.length})`, icon: <Users className="w-4 h-4" /> },
  ];

  return (
    <div className="flex flex-col gap-6 max-w-4xl h-full">
      <div className="flex items-center gap-4">
        <button
          onClick={() => router.push("/admin/plans")}
          className="p-2 hover:bg-white/5 rounded-lg transition-colors"
        >
          <ArrowLeft className="w-5 h-5 text-white/60" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold flex items-center gap-2">
            <CreditCard className="w-6 h-6 text-danger" /> {plan.name}
          </h1>
          <p className="text-sm text-white/60 font-mono">{plan.slug}</p>
        </div>
      </div>

      {error && (
        <div className="bg-danger/10 border border-danger/30 text-danger p-4 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div className="flex gap-2 border-b border-white/10">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.key
                ? "border-danger text-danger"
                : "border-transparent text-white/50 hover:text-white"
            }`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === "overview" && (
        <div className="space-y-6">
          <div className="bg-black/40 border border-white/10 rounded-xl p-6">
            <h3 className="text-sm font-medium text-white/70 mb-4">Plan Details</h3>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <div className="text-xs text-white/40 mb-1">Description</div>
                <div className="text-sm text-white">{plan.description || "—"}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Status</div>
                <div className={`text-sm font-medium ${plan.isActive ? "text-success" : "text-white/40"}`}>
                  {plan.isActive ? "Active" : "Archived"}
                </div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Monthly Price</div>
                <div className="text-sm text-white">{formatPrice(plan.monthlyPriceCents, plan.billingInterval)}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Yearly Price</div>
                <div className="text-sm text-white">{formatPrice(plan.yearlyPriceCents, plan.billingInterval)}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Max Seats</div>
                <div className="text-sm text-white">{plan.maxSeats ? `${plan.maxSeats}` : "Unlimited"}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Billing Interval</div>
                <div className="text-sm text-white">{plan.billingInterval}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Trial</div>
                <div className="text-sm text-white">{plan.isTrialAllowed ? `${plan.trialDays} days` : "No trial"}</div>
              </div>
              <div>
                <div className="text-xs text-white/40 mb-1">Legacy Locked</div>
                <div className="text-sm text-white">{plan.legacyLocked ? "Yes" : "No"}</div>
              </div>
            </div>
          </div>

          {plan.entitlements && (
            <div className="bg-black/40 border border-white/10 rounded-xl p-6">
              <h3 className="text-sm font-medium text-white/70 mb-4">Entitlements</h3>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <div className="text-xs text-white/40 mb-1">Max Monthly Tokens</div>
                  <div className="text-sm text-white">{plan.entitlements.maxMonthlyTokens.toLocaleString()}</div>
                </div>
                <div>
                  <div className="text-xs text-white/40 mb-1">Max API Requests/Day</div>
                  <div className="text-sm text-white">{plan.entitlements.maxApiRequestsPerDay.toLocaleString()}</div>
                </div>
                <div>
                  <div className="text-xs text-white/40 mb-1">Max Storage</div>
                  <div className="text-sm text-white">{plan.entitlements.maxStorageMb} MB</div>
                </div>
                <div>
                  <div className="text-xs text-white/40 mb-1">Max Knowledge Bases</div>
                  <div className="text-sm text-white">{plan.entitlements.maxKnowledgeBases}</div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {activeTab === "modules" && (
        <div className="space-y-4">
          <div className="bg-black/40 border border-white/10 rounded-xl p-6">
            <h3 className="text-sm font-medium text-white/70 mb-4">Included Modules</h3>
            {plan.moduleIds.length === 0 ? (
              <div className="text-sm text-white/40">No modules assigned to this plan</div>
            ) : (
              <div className="space-y-2">
                {plan.moduleIds.map((moduleId: string) => {
                  const mod = availableModules.find(m => m.id === moduleId);
                  return (
                    <div key={moduleId} className="flex items-center justify-between p-3 bg-black/20 border border-white/5 rounded-lg">
                      <div className="flex items-center gap-3">
                        <Check className="w-4 h-4 text-success" />
                        <div>
                          <div className="text-sm text-white font-medium">{mod?.displayName || moduleId}</div>
                          <div className="text-xs text-white/40">{mod?.description}</div>
                        </div>
                      </div>
                      <button
                        onClick={() => handleRemoveModule(moduleId)}
                        className="p-1.5 text-white/40 hover:text-danger transition-colors"
                        title="Remove module"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {availableToAdd.length > 0 && (
            <div className="bg-black/40 border border-white/10 rounded-xl p-6">
              <h3 className="text-sm font-medium text-white/70 mb-4">Add Module</h3>
              <div className="flex gap-2">
                <select
                  value={addingModule}
                  onChange={e => setAddingModule(e.target.value)}
                  className="flex-1 px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                >
                  <option value="">Select a module...</option>
                  {availableToAdd.map(mod => (
                    <option key={mod.id} value={mod.id}>{mod.displayName}</option>
                  ))}
                </select>
                <button
                  onClick={handleAddModule}
                  disabled={!addingModule || saving}
                  className="px-4 py-2 bg-danger text-white rounded-lg text-sm font-medium flex items-center gap-2 hover:bg-danger/90 transition-colors disabled:opacity-50"
                >
                  <Plus className="w-4 h-4" /> Add
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {activeTab === "companies" && (
        <div className="bg-black/40 border border-white/10 rounded-xl p-6">
          <h3 className="text-sm font-medium text-white/70 mb-4">Companies on This Plan</h3>
          {planCompanies.length === 0 ? (
            <div className="text-sm text-white/40">No companies are currently on this plan</div>
          ) : (
            <div className="space-y-2">
              {planCompanies.map((company: Company) => (
                <div key={company.id} className="flex items-center justify-between p-3 bg-black/20 border border-white/5 rounded-lg">
                  <div>
                    <div className="text-sm text-white font-medium">{company.name}</div>
                    <div className="text-xs text-white/40">{company.seatCount} seats</div>
                  </div>
                  <span className={`px-2 py-1 text-xs rounded font-bold ${
                    company.status === "Active" ? "bg-success/20 text-success" :
                    company.status === "Trialing" ? "bg-primary/20 text-primary" :
                    "bg-white/10 text-white/50"
                  }`}>
                    {company.status}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
