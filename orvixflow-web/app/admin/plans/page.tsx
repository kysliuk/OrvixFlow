"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { Plus, Edit, Archive, CreditCard, Users, Database, Cpu } from "lucide-react";

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
  entitlements: {
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
  } | null;
}

export default function PlansPage() {
  const { data: session } = useSession();
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  useEffect(() => {
    if ((session as any)?.apiToken) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/plans?includeInactive=true`, {
        headers: { "Authorization": `Bearer ${(session as any).apiToken}` }
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
    }
  }, [session]);

  const handleArchive = async (planId: string) => {
    if (!confirm("Are you sure you want to archive this plan?")) return;
    
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/plans/${planId}/archive`, {
        method: "POST",
        headers: { "Authorization": `Bearer ${(session as any).apiToken}` }
      });
      if (!res.ok) throw new Error("Failed to archive plan");
      
      setPlans(plans.map(p => p.id === planId ? { ...p, isActive: false, archivedAt: new Date().toISOString() } : p));
    } catch (e) {
      alert("Failed to archive plan");
    }
  };

  const formatPrice = (cents: number) => {
    return `$${(cents / 100).toFixed(2)}`;
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
          onClick={() => setShowCreateModal(true)}
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

            <div className="flex gap-2 pt-4 border-t border-white/10">
              <button className="flex-1 px-3 py-2 bg-danger/10 text-danger text-xs font-medium rounded hover:bg-danger/20 transition-colors flex items-center justify-center gap-1">
                <Edit className="w-3 h-3" /> Edit
              </button>
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
    </div>
  );
}
