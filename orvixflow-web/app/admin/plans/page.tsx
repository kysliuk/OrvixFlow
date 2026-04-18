"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { Plus, Edit, Archive, CreditCard, Users, Database, Cpu, X, RotateCcw } from "lucide-react";

interface Plan {
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
  isPubliclyVisible: boolean;
  sortOrder: number;
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

interface PlanModuleEntry {
  moduleId: string;
  maxUsagePerMonth: string;
  maxItemsTotal: string;
  limitDescription: string;
}

interface PlanFormData {
  name: string;
  slug: string;
  description: string;
  monthlyPriceCents: number;
  yearlyPriceCents: number;
  maxSeats: string;
  isTrialAllowed: boolean;
  trialDays: number;
  isActive: boolean;
  billingInterval: string;
  legacyLocked: boolean;
  isPubliclyVisible: boolean;
  sortOrder: number;
  entitlements: {
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
  };
  moduleIds: PlanModuleEntry[];
}

const initialFormData: PlanFormData = {
  name: "",
  slug: "",
  description: "",
  monthlyPriceCents: 0,
  yearlyPriceCents: 0,
  maxSeats: "",
  isTrialAllowed: false,
  trialDays: 14,
  isActive: true,
  billingInterval: "Monthly",
  legacyLocked: false,
  isPubliclyVisible: true,
  sortOrder: 0,
  entitlements: {
    maxMonthlyTokens: 50000,
    maxApiRequestsPerDay: 500,
    maxStorageMb: 100,
    maxKnowledgeBases: 1,
  },
  moduleIds: [],
};

export default function PlansPage() {
  const { data: session } = useSession();
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [editingPlan, setEditingPlan] = useState<Plan | null>(null);
  const [formData, setFormData] = useState<PlanFormData>(initialFormData);
  const [saving, setSaving] = useState(false);
  const [availableModules, setAvailableModules] = useState<ModuleDef[]>([]);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  const fetchPlans = () => {
    if (!apiToken) return;
    
    fetch(`${apiUrl}/api/plans?includeInactive=true`, {
      headers: { Authorization: `Bearer ${apiToken}` }
    })
      .then(res => {
        if (!res.ok) throw new Error("Failed to load plans");
        return res.json();
      })
      .then(data => {
        setPlans(data);
        setLoading(false);
      })
      .catch(e => {
        setError(e.message);
        setLoading(false);
      });
  };

  const fetchModules = () => {
    if (!apiToken) return;
    
    fetch(`${apiUrl}/api/admin/modules`, {
      headers: { Authorization: `Bearer ${apiToken}` }
    })
      .then(res => res.json())
      .then(data => {
        setAvailableModules((Array.isArray(data) ? data : []).filter((m: ModuleDef) => m.isActive));
      })
      .catch(() => {});
  };

  useEffect(() => {
    fetchPlans();
    fetchModules();
  }, [apiToken]);

  const handleCreateClick = () => {
    setEditingPlan(null);
    setFormData(initialFormData);
    setShowModal(true);
  };

  const handleEditClick = (plan: Plan) => {
    setEditingPlan(plan);
    const moduleEntries: PlanModuleEntry[] = plan.moduleIds.map((id: string) => ({
      moduleId: id,
      maxUsagePerMonth: "",
      maxItemsTotal: "",
      limitDescription: "",
    }));
    setFormData({
      name: plan.name,
      slug: plan.slug,
      description: plan.description,
      monthlyPriceCents: plan.monthlyPriceCents,
      yearlyPriceCents: plan.yearlyPriceCents,
      maxSeats: plan.maxSeats?.toString() || "",
      isTrialAllowed: plan.isTrialAllowed,
      trialDays: plan.trialDays,
      isActive: plan.isActive,
      billingInterval: plan.billingInterval || "Monthly",
      legacyLocked: plan.legacyLocked,
      isPubliclyVisible: plan.isPubliclyVisible !== undefined ? plan.isPubliclyVisible : true,
      sortOrder: plan.sortOrder || 0,
      entitlements: plan.entitlements || {
        maxMonthlyTokens: 50000,
        maxApiRequestsPerDay: 500,
        maxStorageMb: 100,
        maxKnowledgeBases: 1,
      },
      moduleIds: moduleEntries,
    });
    setShowModal(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!apiToken) return;

    setSaving(true);
    setError(null);

    const payload = {
      name: formData.name,
      slug: formData.slug,
      description: formData.description,
      monthlyPriceCents: formData.monthlyPriceCents,
      yearlyPriceCents: formData.yearlyPriceCents,
      maxSeats: formData.maxSeats ? parseInt(formData.maxSeats) : null,
      isTrialAllowed: formData.isTrialAllowed,
      trialDays: formData.trialDays,
      isFree: formData.monthlyPriceCents === 0,
      isActive: formData.isActive,
      billingInterval: formData.billingInterval,
      legacyLocked: formData.legacyLocked,
      isPubliclyVisible: formData.isPubliclyVisible,
      sortOrder: formData.sortOrder,
      entitlements: formData.entitlements,
      moduleIds: formData.moduleIds.map(m => m.moduleId),
    };

    try {
      const url = editingPlan 
        ? `${apiUrl}/api/plans/${editingPlan.id}`
        : `${apiUrl}/api/plans`;
      
      const res = await fetch(url, {
        method: editingPlan ? "PUT" : "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${apiToken}`,
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const err = await res.json();
        throw new Error(err.error || "Failed to save plan");
      }

      setShowModal(false);
      fetchPlans();
    } catch (err: any) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const toggleModule = (moduleId: string) => {
    const exists = formData.moduleIds.find(m => m.moduleId === moduleId);
    if (exists) {
      setFormData({
        ...formData,
        moduleIds: formData.moduleIds.filter(m => m.moduleId !== moduleId),
      });
    } else {
      setFormData({
        ...formData,
        moduleIds: [...formData.moduleIds, { moduleId, maxUsagePerMonth: "", maxItemsTotal: "", limitDescription: "" }],
      });
    }
  };

  const updateModuleLimit = (moduleId: string, field: keyof Omit<PlanModuleEntry, 'moduleId'>, value: string) => {
    setFormData({
      ...formData,
      moduleIds: formData.moduleIds.map(m =>
        m.moduleId === moduleId ? { ...m, [field]: value } : m
      ),
    });
  };

  const handleArchive = async (planId: string) => {
    if (!confirm("Are you sure you want to archive this plan?")) return;
    
    try {
      const res = await fetch(`${apiUrl}/api/plans/${planId}/archive`, {
        method: "POST",
        headers: { Authorization: `Bearer ${apiToken}` },
      });
      if (!res.ok) throw new Error("Failed to archive plan");
      
      setPlans(plans.map(p => p.id === planId ? { ...p, isActive: false, archivedAt: new Date().toISOString() } : p));
    } catch (e) {
      alert("Failed to archive plan");
    }
  };

  const handleReactivate = async (planId: string) => {
    try {
      const res = await fetch(`${apiUrl}/api/plans/${planId}/reactivate`, {
        method: "POST",
        headers: { Authorization: `Bearer ${apiToken}` },
      });
      if (!res.ok) throw new Error("Failed to reactivate plan");
      
      setPlans(plans.map(p => p.id === planId ? { ...p, isActive: true, archivedAt: null } : p));
    } catch (e) {
      alert("Failed to reactivate plan");
    }
  };

  const formatPrice = (cents: number, interval?: string) => {
    if (cents === 0 && interval === "Custom") return "Custom";
    return `$${(cents / 100).toFixed(0)}`;
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-danger animate-pulse font-mono">LOADING_PLANS...</div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 max-w-6xl h-full">
      <div className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
            <CreditCard className="w-6 h-6 text-danger" /> Plan Templates
          </h1>
          <p className="text-sm text-white/60 font-mono">
            MANAGE_SUBSCRIPTION_TIERS // SUPER_ADMIN_ONLY
          </p>
        </div>
        <button
          onClick={handleCreateClick}
          className="px-4 py-2 bg-danger text-white rounded-lg font-medium text-sm flex items-center gap-2 hover:bg-danger/90 transition-colors"
        >
          <Plus className="w-4 h-4" /> Create Plan
        </button>
      </div>

      {error && (
        <div className="bg-danger/10 border border-danger/30 text-danger p-4 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {plans.map(plan => (
          <div 
            key={plan.id} 
            className={`bg-black/40 border rounded-xl p-5 relative overflow-hidden ${
              plan.isActive 
                ? "border-danger/20 hover:border-danger/40" 
                : "border-white/10 opacity-60"
            }`}
          >
            {!plan.isActive && (
              <div className="absolute top-3 right-3">
                <span className="px-2 py-1 bg-white/10 text-white/50 text-xs rounded uppercase font-bold">
                  Archived
                </span>
              </div>
            )}
            
            <div className="flex justify-between items-start mb-4">
              <div>
                <h3 className="text-lg font-semibold text-white">{plan.name}</h3>
                <p className="text-xs text-white/50 font-mono">{plan.slug}</p>
              </div>
              {plan.isFree ? (
                <span className="px-2 py-1 bg-success/20 text-success text-xs rounded font-bold">Free</span>
              ) : plan.billingInterval === "Custom" ? (
                <span className="text-lg font-bold text-white">Custom</span>
              ) : (
                <span className="text-lg font-bold text-white">
                  {formatPrice(plan.monthlyPriceCents)}
                  <span className="text-xs text-white/50 font-normal">/mo</span>
                </span>
              )}
            </div>

            <p className="text-sm text-white/60 mb-4 line-clamp-2">{plan.description}</p>

            <div className="grid grid-cols-2 gap-2 mb-4">
              <div className="flex items-center gap-2 text-xs text-white/50">
                <Users className="w-3 h-3" />
                {plan.maxSeats ? `${plan.maxSeats} seats` : "Unlimited"}
              </div>
              <div className="flex items-center gap-2 text-xs text-white/50">
                <Cpu className="w-3 h-3" />
                {plan.entitlements ? `${(plan.entitlements.maxMonthlyTokens / 1000).toLocaleString()}k tokens` : "N/A"}
              </div>
              <div className="flex items-center gap-2 text-xs text-white/50">
                <Database className="w-3 h-3" />
                {plan.entitlements ? `${plan.entitlements.maxStorageMb}MB` : "N/A"}
              </div>
              <div className="flex items-center gap-2 text-xs text-white/50">
                {plan.isTrialAllowed ? `Trial: ${plan.trialDays}d` : "No trial"}
              </div>
            </div>

            {plan.moduleIds.length > 0 && (
              <div className="mb-4">
                <div className="text-xs text-white/50 mb-1">{plan.moduleIds.length} module(s) included</div>
                <div className="flex flex-wrap gap-1">
                  {plan.moduleIds.slice(0, 3).map((id: string) => {
                    const mod = availableModules.find(m => m.id === id);
                    return (
                      <span key={id} className="px-2 py-0.5 bg-white/5 text-white/60 text-[10px] rounded font-mono">
                        {mod ? mod.displayName : id.slice(0, 8)}
                      </span>
                    );
                  })}
                  {plan.moduleIds.length > 3 && (
                    <span className="px-2 py-0.5 bg-white/5 text-white/40 text-[10px] rounded font-mono">
                      +{plan.moduleIds.length - 3}
                    </span>
                  )}
                </div>
              </div>
            )}

            <div className="flex gap-2 pt-4 border-t border-white/10">
              <button 
                onClick={() => handleEditClick(plan)}
                className="flex-1 px-3 py-2 bg-danger/10 text-danger text-xs font-medium rounded hover:bg-danger/20 transition-colors flex items-center justify-center gap-1"
              >
                <Edit className="w-3 h-3" /> Edit
              </button>
              {!plan.isActive && !plan.legacyLocked && (
                <button 
                  onClick={() => handleReactivate(plan.id)}
                  className="px-3 py-2 bg-success/10 text-success text-xs font-medium rounded hover:bg-success/20 transition-colors"
                  title="Reactivate plan"
                >
                  <RotateCcw className="w-3 h-3" />
                </button>
              )}
              {plan.isActive && !plan.legacyLocked && (
                <button 
                  onClick={() => handleArchive(plan.id)}
                  className="px-3 py-2 bg-white/5 text-white/50 text-xs font-medium rounded hover:bg-white/10 transition-colors"
                >
                  <Archive className="w-3 h-3" />
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-surface border border-white/10 rounded-2xl w-full max-w-2xl shadow-2xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-white/10 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-white">
                {editingPlan ? "Edit Plan" : "Create New Plan"}
              </h2>
              <button 
                onClick={() => setShowModal(false)}
                className="text-muted hover:text-white transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <form onSubmit={handleSubmit} className="flex flex-col max-h-[75vh]">
              <div className="flex-1 overflow-y-auto p-6 space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Plan Name</label>
                    <input
                      type="text"
                      value={formData.name}
                      onChange={e => setFormData({ ...formData, name: e.target.value })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Slug</label>
                    <input
                      type="text"
                      value={formData.slug}
                      onChange={e => setFormData({ ...formData, slug: e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, "-") })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      required
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-medium text-white/70 mb-1">Description</label>
                  <textarea
                    value={formData.description}
                    onChange={e => setFormData({ ...formData, description: e.target.value })}
                    className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                    rows={2}
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Monthly Price ($)</label>
                    <input
                      type="number"
                      min="0"
                      step="1"
                      value={formData.monthlyPriceCents === 0 ? "" : formData.monthlyPriceCents / 100}
                      onChange={e => setFormData({ ...formData, monthlyPriceCents: Math.round((parseFloat(e.target.value) || 0) * 100) })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      placeholder="0"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Yearly Price ($)</label>
                    <input
                      type="number"
                      min="0"
                      step="1"
                      value={formData.yearlyPriceCents === 0 ? "" : formData.yearlyPriceCents / 100}
                      onChange={e => setFormData({ ...formData, yearlyPriceCents: Math.round((parseFloat(e.target.value) || 0) * 100) })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      placeholder="0"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Max Seats (empty = unlimited)</label>
                    <input
                      type="number"
                      min="1"
                      value={formData.maxSeats}
                      onChange={e => setFormData({ ...formData, maxSeats: e.target.value })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      placeholder="Unlimited"
                    />
                  </div>
                  <div className="flex items-center gap-4 pt-6">
                    <label className="flex items-center gap-2 text-sm text-white">
                      <input
                        type="checkbox"
                        checked={formData.isTrialAllowed}
                        onChange={e => setFormData({ ...formData, isTrialAllowed: e.target.checked })}
                        className="w-4 h-4 rounded border-white/20 bg-background text-danger focus:ring-danger"
                      />
                      Allow Trial
                    </label>
                    {formData.isTrialAllowed && (
                      <input
                        type="number"
                        min="1"
                        max="90"
                        value={formData.trialDays}
                        onChange={e => setFormData({ ...formData, trialDays: parseInt(e.target.value) || 14 })}
                        className="w-20 px-2 py-1 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      />
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Billing Interval</label>
                    <select
                      value={formData.billingInterval}
                      onChange={e => setFormData({ ...formData, billingInterval: e.target.value })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                    >
                      <option value="Monthly">Monthly</option>
                      <option value="Yearly">Yearly</option>
                      <option value="Custom">Custom</option>
                    </select>
                  </div>
                  <div className="flex items-center gap-4 pt-6">
                    <label className="flex items-center gap-2 text-sm text-white">
                      <input
                        type="checkbox"
                        checked={formData.legacyLocked}
                        onChange={e => setFormData({ ...formData, legacyLocked: e.target.checked })}
                        className="w-4 h-4 rounded border-white/20 bg-background text-danger focus:ring-danger"
                      />
                      Legacy Locked
                    </label>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-white/70 mb-1">Sort Order</label>
                    <input
                      type="number"
                      value={formData.sortOrder}
                      onChange={e => setFormData({ ...formData, sortOrder: parseInt(e.target.value) || 0 })}
                      className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                    />
                  </div>
                  <div className="flex items-center gap-4 pt-6">
                    <label className="flex items-center gap-2 text-sm text-white">
                      <input
                        type="checkbox"
                        checked={formData.isPubliclyVisible}
                        onChange={e => setFormData({ ...formData, isPubliclyVisible: e.target.checked })}
                        className="w-4 h-4 rounded border-white/20 bg-background text-danger focus:ring-danger"
                      />
                      Publicly Visible (show on billing page)
                    </label>
                  </div>
                </div>

                <div className="border-t border-white/10 pt-4">
                  <label className="block text-xs font-medium text-white/70 mb-3">Entitlements</label>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs text-white/50 mb-1">Max Monthly Tokens</label>
                      <input
                        type="number"
                        min="0"
                        value={formData.entitlements.maxMonthlyTokens}
                        onChange={e => setFormData({ ...formData, entitlements: { ...formData.entitlements, maxMonthlyTokens: parseInt(e.target.value) || 0 } })}
                        className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      />
                    </div>
                    <div>
                      <label className="block text-xs text-white/50 mb-1">Max API Requests/Day</label>
                      <input
                        type="number"
                        min="0"
                        value={formData.entitlements.maxApiRequestsPerDay}
                        onChange={e => setFormData({ ...formData, entitlements: { ...formData.entitlements, maxApiRequestsPerDay: parseInt(e.target.value) || 0 } })}
                        className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      />
                    </div>
                    <div>
                      <label className="block text-xs text-white/50 mb-1">Max Storage (MB)</label>
                      <input
                        type="number"
                        min="0"
                        value={formData.entitlements.maxStorageMb}
                        onChange={e => setFormData({ ...formData, entitlements: { ...formData.entitlements, maxStorageMb: parseInt(e.target.value) || 0 } })}
                        className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      />
                    </div>
                    <div>
                      <label className="block text-xs text-white/50 mb-1">Max Knowledge Bases</label>
                      <input
                        type="number"
                        min="0"
                        value={formData.entitlements.maxKnowledgeBases}
                        onChange={e => setFormData({ ...formData, entitlements: { ...formData.entitlements, maxKnowledgeBases: parseInt(e.target.value) || 0 } })}
                        className="w-full px-3 py-2 bg-background border border-white/10 rounded-lg text-white text-sm focus:outline-none focus:border-danger"
                      />
                    </div>
                  </div>
                </div>

                <div className="border-t border-white/10 pt-4">
                  <label className="block text-xs font-medium text-white/70 mb-3">Modules Included in This Plan</label>
                  <div className="space-y-2">
                    {availableModules.length === 0 && (
                      <div className="text-xs text-white/40">No active modules available</div>
                    )}
                    {availableModules.map(mod => {
                      const entry = formData.moduleIds.find(m => m.moduleId === mod.id);
                      const isChecked = !!entry;
                      return (
                        <div key={mod.id} className={`p-3 rounded-lg border transition-colors ${isChecked ? "border-danger/30 bg-danger/5" : "border-white/5 bg-black/20"}`}>
                          <label className="flex items-center gap-3 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={isChecked}
                              onChange={() => toggleModule(mod.id)}
                              className="w-4 h-4 rounded border-white/20 bg-background text-danger focus:ring-danger"
                            />
                            <div className="flex-1 min-w-0">
                              <div className="text-sm text-white font-medium">{mod.displayName}</div>
                              <div className="text-xs text-white/40">{mod.description}</div>
                            </div>
                            {mod.isPremium && (
                              <span className="px-2 py-0.5 bg-primary/20 text-primary text-[10px] rounded font-bold uppercase">Premium</span>
                            )}
                          </label>
                          {isChecked && (
                            <div className="mt-3 grid grid-cols-3 gap-2 pl-7">
                              <div>
                                <label className="block text-[10px] text-white/40 mb-1">Max Usage/Month</label>
                                <input
                                  type="number"
                                  min="0"
                                  value={entry?.maxUsagePerMonth || ""}
                                  onChange={e => updateModuleLimit(mod.id, "maxUsagePerMonth", e.target.value)}
                                  placeholder="Unlimited"
                                  className="w-full px-2 py-1.5 bg-background border border-white/10 rounded text-white text-xs focus:outline-none focus:border-danger"
                                />
                              </div>
                              <div>
                                <label className="block text-[10px] text-white/40 mb-1">Max Total Items</label>
                                <input
                                  type="number"
                                  min="0"
                                  value={entry?.maxItemsTotal || ""}
                                  onChange={e => updateModuleLimit(mod.id, "maxItemsTotal", e.target.value)}
                                  placeholder="Unlimited"
                                  className="w-full px-2 py-1.5 bg-background border border-white/10 rounded text-white text-xs focus:outline-none focus:border-danger"
                                />
                              </div>
                              <div>
                                <label className="block text-[10px] text-white/40 mb-1">Limit Description</label>
                                <input
                                  type="text"
                                  value={entry?.limitDescription || ""}
                                  onChange={e => updateModuleLimit(mod.id, "limitDescription", e.target.value)}
                                  placeholder="e.g., 100 docs/day"
                                  className="w-full px-2 py-1.5 bg-background border border-white/10 rounded text-white text-xs focus:outline-none focus:border-danger"
                                />
                              </div>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              </div>

              {error && (
                <div className="px-6 py-2 text-danger text-sm">{error}</div>
              )}

              <div className="px-6 py-4 border-t border-white/10 flex gap-3 justify-end">
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  className="px-5 py-2.5 text-sm font-medium border border-white/10 hover:bg-white/5 rounded-lg transition-all"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={saving}
                  className="px-5 py-2.5 text-sm font-medium bg-danger text-white hover:bg-danger/90 rounded-lg transition-all flex items-center gap-2 disabled:opacity-50"
                >
                  {saving ? "Saving..." : editingPlan ? "Update Plan" : "Create Plan"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

