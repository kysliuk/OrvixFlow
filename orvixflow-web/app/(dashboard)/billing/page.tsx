/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useSession } from "next-auth/react";
import { CheckCircle2, Zap, Rocket, Building2, AlertTriangle, X } from "lucide-react";
import { useEffect, useState } from "react";

type Plan = {
  id: string;
  name: string;
  slug: string;
  description: string;
  monthlyPriceCents: number;
  yearlyPriceCents: number;
  billingInterval: string;
  maxSeats: number | null;
  isFree: boolean;
  isUpgrade: boolean;
  isDowngrade: boolean;
  isCurrentPlan: boolean;
  entitlements: {
    maxMonthlyTokens: number;
    maxApiRequestsPerDay: number;
    maxStorageMb: number;
    maxKnowledgeBases: number;
  };
};

type SubscriptionData = {
  plan: {
    name: string;
    price: number;
    interval: string;
  } | null;
  status: string;
  currentPeriodEnd: string | null;
  entitlements: {
    maxSeats: number | null;
    usedSeats: number;
    maxMonthlyTokens: number;
    usedTokens: number;
    maxStorageMb: number;
    usedStorageMb: number;
    maxKnowledgeBases: number | null;
    usedKnowledgeBases: number;
  };
};

export default function BillingPage() {
  const { data: session, update } = useSession();
  
  const [subscription, setSubscription] = useState<SubscriptionData | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [showUpgradeModal, setShowUpgradeModal] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<Plan | null>(null);
  const [isUpgrading, setIsUpgrading] = useState(false);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  const fetchData = async () => {
    if (!apiToken) {
      setLoading(false);
      return;
    }

    try {
      const [subRes, plansRes] = await Promise.all([
        fetch(`${apiUrl}/api/billing/subscription`, {
          headers: { Authorization: `Bearer ${apiToken}` },
        }),
        fetch(`${apiUrl}/api/billing/plans`, {
          headers: { Authorization: `Bearer ${apiToken}` },
        }),
      ]);

      if (subRes.ok) {
        const subData = await subRes.json();
        setSubscription(subData);
      }

      if (plansRes.ok) {
        const plansData = await plansRes.json();
        setPlans(plansData.plans || []);
      }
    } catch (err) {
      console.error("Failed to fetch billing data:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [apiToken]);

  const handleUpgradeClick = (plan: Plan) => {
    setSelectedPlan(plan);
    setShowUpgradeModal(true);
    setError(null);
  };

  const handleConfirmUpgrade = async () => {
    if (!selectedPlan || !apiToken) return;

    setIsUpgrading(true);
    setError(null);

    try {
      const res = await fetch(`${apiUrl}/api/billing/change-plan`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${apiToken}`,
        },
        body: JSON.stringify({
          planTemplateId: selectedPlan.id,
          immediate: true,
        }),
      });

      const data = await res.json();

      if (res.ok) {
        setSuccess(data.message);
        setShowUpgradeModal(false);
        fetchData();
      } else {
        setError(data.error === "SEAT_LIMIT_EXCEEDED" 
          ? `Cannot upgrade: ${data.message}`
          : data.error || "Failed to change plan");
      }
    } catch (err) {
      setError("An error occurred. Please try again.");
    } finally {
      setIsUpgrading(false);
    }
  };

  const getPlanIcon = (name: string) => {
    switch (name.toLowerCase()) {
      case "free":
        return Zap;
      case "starter":
        return Rocket;
      case "growth":
        return Rocket;
      case "business":
        return Building2;
      case "enterprise":
        return Building2;
      default:
        return Zap;
    }
  };

  const formatPrice = (cents: number, isFree?: boolean, billingInterval?: string) => {
    if (billingInterval === "Custom") return "Custom";
    if (cents === 0 || isFree) return "Free";
    return `$${(cents / 100).toFixed(0)}`;
  };

  const formatFeatures = (plan: Plan) => {
    const features = [];
    const ent = plan.entitlements;
    
    features.push(`${(ent.maxMonthlyTokens / 1000).toFixed(0)}K AI Tokens / month`);
    
    if (ent.maxKnowledgeBases > 100) {
      features.push("Unlimited Knowledge Bases");
    } else {
      features.push(`${ent.maxKnowledgeBases} Knowledge Base${ent.maxKnowledgeBases !== 1 ? "s" : ""}`);
    }
    
    if (ent.maxStorageMb >= 1024) {
      features.push(`${(ent.maxStorageMb / 1024).toFixed(0)} GB Storage`);
    } else {
      features.push(`${ent.maxStorageMb} MB Storage`);
    }
    
    if (plan.maxSeats === null) {
      features.push("Unlimited Seats");
    } else if (plan.maxSeats > 100) {
      features.push("100+ Seats");
    } else {
      features.push(`${plan.maxSeats} Seat${plan.maxSeats !== 1 ? "s" : ""}`);
    }

    return features;
  };

  const currentPlanName = subscription?.plan?.name || "Free";
  const currentPlanPrice = subscription?.plan?.price || 0;

  const usedTokens = subscription?.entitlements?.usedTokens || 0;
  const maxTokens = subscription?.entitlements?.maxMonthlyTokens || 50000;
  const tokenPercentage = Math.min((usedTokens / maxTokens) * 100, 100);

  const renewalStr = subscription?.currentPeriodEnd 
    ? new Date(subscription.currentPeriodEnd).toLocaleDateString() 
    : "N/A";

  return (
    <div className="flex flex-col gap-8 max-w-6xl animate-in fade-in duration-300">
      <div className="text-center max-w-2xl mx-auto mt-6 mb-2">
        <h1 className="text-3xl font-semibold mb-3 tracking-tight">Manage Subscription</h1>
        <p className="text-muted">
          Currently using the <span className="text-white font-medium capitalize">{currentPlanName}</span> plan.
          {subscription?.status && subscription.status !== "Active" && (
            <span className="ml-2 text-amber-400">({subscription.status})</span>
          )}
        </p>
      </div>

      {success && (
        <div className="flex items-center gap-3 px-4 py-3 bg-emerald-500/10 border border-emerald-500/20 rounded-xl text-emerald-400">
          <CheckCircle2 className="w-5 h-5" />
          {success}
        </div>
      )}

      {loading ? (
        <div className="h-32 bg-surface animate-pulse rounded-2xl border border-white/5" />
      ) : (
        <div className="bg-gradient-to-br from-surface to-background border border-primary/20 rounded-2xl p-8 relative overflow-hidden shadow-[0_0_40px_rgba(var(--color-primary),0.05)] text-center sm:text-left">
          <div className="absolute top-0 right-0 w-64 h-64 bg-primary/10 rounded-full blur-[80px] -translate-y-1/2 translate-x-1/2" />
          
          <div className="relative z-10 flex flex-col md:flex-row items-center justify-between gap-8 w-full">
            <div className="flex-1 w-full flex flex-col items-center sm:items-start">
              <h2 className="text-lg font-semibold text-white mb-1">AI Token Quota</h2>
              <p className="text-sm text-muted mb-6 text-center sm:text-left">
                Your generative intelligence consumption for the current cycle. Resets on <strong>{renewalStr}</strong>.
              </p>
              
              <div className="w-full flex items-center justify-between mb-2 text-sm font-medium">
                <span className="text-primary tracking-wider uppercase text-[10px] sm:text-xs font-bold bg-primary/10 px-2 py-0.5 rounded border border-primary/20">
                  {usedTokens.toLocaleString()} Tokens
                </span>
                <span className="text-muted text-xs tracking-wider uppercase">
                  {maxTokens.toLocaleString()} Limit
                </span>
              </div>
              
              <div className="h-2.5 w-full bg-black/40 rounded-full overflow-hidden shadow-inner border border-white/5 mx-auto sm:mx-0">
                <div 
                  className={`h-full rounded-full transition-all duration-1000 ease-in-out relative ${
                    tokenPercentage > 90 ? "bg-danger" : tokenPercentage > 75 ? "bg-amber-500" : "bg-primary shadow-[0_0_10px_var(--accent-primary)]"
                  }`}
                  style={{ width: `${tokenPercentage}%` }}
                >
                  <div className="absolute inset-0 bg-gradient-to-r from-transparent to-white/20" />
                </div>
              </div>
              <div className="mt-2 text-xs text-muted/70 text-right w-full font-mono">
                {tokenPercentage.toFixed(1)}% Utilized
              </div>
            </div>
            
            <div className="shrink-0 flex items-center justify-center pt-4 md:pt-0 border-t md:border-t-0 md:border-l border-white/10 w-full md:w-auto md:pl-10">
              <div className="flex flex-col items-center">
                <div className="text-[10px] uppercase tracking-widest text-muted mb-1 font-bold">Billing Status</div>
                <div className={`flex items-center gap-2 px-3 py-1.5 rounded-md text-sm font-medium ${
                  subscription?.status === "Active" 
                    ? "bg-emerald-500/10 border border-emerald-500/20 text-emerald-400"
                    : subscription?.status === "Trialing"
                    ? "bg-amber-500/10 border border-amber-500/20 text-amber-400"
                    : "bg-danger/10 border border-danger/20 text-danger"
                }`}>
                  <CheckCircle2 className="w-4 h-4" /> {subscription?.status || "Active"}
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-4 relative z-10">
        {plans.map((plan) => {
          const Icon = getPlanIcon(plan.name);
          const isCurrent = plan.isCurrentPlan;
          
          return (
            <div 
              key={plan.id}
              className={`relative flex flex-col bg-surface border rounded-2xl p-6 transition-all duration-300 ${
                isCurrent 
                  ? "border-primary shadow-[0_0_30px_var(--accent-glow)] scale-[1.02]" 
                  : "border-white/10 hover:border-white/20 hover:bg-white/[0.02]"
              }`}
            >
              {!isCurrent && plan.isUpgrade && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2 bg-gradient-to-r from-primary to-danger px-3 py-1 rounded-full text-[10px] uppercase tracking-wider font-bold shadow-lg">
                  Upgrade
                </div>
              )}
              
              {isCurrent && (
                <div className="absolute top-4 right-4 text-[10px] uppercase font-bold text-primary bg-primary/10 px-2 py-1 rounded-md border border-primary/20">
                  Current
                </div>
              )}

              <div className="mb-6 mt-2">
                <div className={`w-12 h-12 rounded-xl flex items-center justify-center mb-4 ${isCurrent ? "bg-primary/20 text-primary" : "bg-white/5 text-muted"}`}>
                  <Icon className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold mb-1">{plan.name}</h3>
                <p className="text-sm text-muted">{plan.description}</p>
              </div>

              <div className="mb-6">
                <span className="text-3xl font-bold">{formatPrice(plan.monthlyPriceCents, plan.isFree, plan.billingInterval)}</span>
                {formatPrice(plan.monthlyPriceCents, plan.isFree, plan.billingInterval) !== "Custom" && 
                 formatPrice(plan.monthlyPriceCents, plan.isFree, plan.billingInterval) !== "Free" && (
                  <span className="text-muted text-sm border-l border-white/10 ml-2 pl-2">per month</span>
                )}
              </div>

              <ul className="flex flex-col gap-3 flex-1 mb-8">
                {formatFeatures(plan).map((f, i) => (
                  <li key={i} className="flex items-start gap-2 text-sm text-white/80">
                    <CheckCircle2 className={`w-4 h-4 mt-0.5 shrink-0 ${isCurrent ? "text-primary" : "text-muted"}`} />
                    {f}
                  </li>
                ))}
              </ul>

              <button 
                onClick={() => !isCurrent && handleUpgradeClick(plan)}
                disabled={isCurrent}
                className={`w-full py-3 rounded-lg text-sm font-medium transition-all ${
                  isCurrent 
                    ? "bg-white/5 text-muted border border-white/10 cursor-default" 
                    : plan.isUpgrade
                      ? "bg-primary hover:bg-primary/90 text-white shadow-[0_4px_14px_var(--accent-glow)]"
                      : "bg-surface-hover hover:bg-white/10 border border-white/10 text-white"
                }`}
              >
                {isCurrent ? "Current Plan" : plan.billingInterval === "Custom" ? "Contact Sales" : plan.monthlyPriceCents === 0 || plan.isFree ? "Downgrade" : plan.isUpgrade ? "Upgrade Plan" : "Downgrade"}
              </button>
            </div>
          );
        })}
      </div>

      {showUpgradeModal && selectedPlan && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
          <div className="bg-surface border border-white/10 rounded-2xl w-full max-w-md shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="px-6 py-4 border-b border-white/10 flex items-center justify-between">
              <h2 className="text-lg font-semibold text-white">
                {selectedPlan.isUpgrade ? "Upgrade" : "Change"} Plan
              </h2>
              <button 
                onClick={() => setShowUpgradeModal(false)}
                className="text-muted hover:text-white transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6">
              {error && (
                <div className="mb-4 px-4 py-3 bg-danger/10 border border-danger/20 rounded-lg text-danger text-sm flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  {error}
                </div>
              )}

              <div className="mb-6">
                <p className="text-sm text-muted mb-4">
                  You are about to {selectedPlan.isUpgrade ? "upgrade" : "change"} from <strong className="text-white">{currentPlanName}</strong> to <strong className="text-white">{selectedPlan.name}</strong>.
                </p>

                <div className="bg-background rounded-lg p-4 space-y-2">
                  <div className="flex justify-between text-sm">
                    <span className="text-muted">Current Plan</span>
                    <span className="text-white">{currentPlanName} ({formatPrice(currentPlanPrice)})</span>
                  </div>
                  <div className="flex justify-between text-sm">
                    <span className="text-muted">New Plan</span>
                    <span className="text-white">{selectedPlan.name} ({formatPrice(selectedPlan.monthlyPriceCents)})</span>
                  </div>
                  <div className="border-t border-white/10 pt-2 mt-2 flex justify-between text-sm">
                    <span className="text-muted">Change</span>
                    <span className={selectedPlan.isUpgrade ? "text-emerald-400" : "text-amber-400"}>
                      {selectedPlan.isUpgrade ? "Immediate" : "At billing cycle end"}
                    </span>
                  </div>
                </div>

                <p className="text-xs text-muted mt-4">
                  {selectedPlan.isUpgrade 
                    ? "Your new plan will be active immediately. You'll be charged the prorated difference."
                    : "Your plan will be changed at the end of your current billing period."}
                </p>
              </div>

              <div className="flex gap-3 justify-end pt-2">
                <button
                  onClick={() => setShowUpgradeModal(false)}
                  disabled={isUpgrading}
                  className="px-5 py-2.5 text-sm font-medium border border-white/10 hover:bg-white/5 rounded-lg transition-all"
                >
                  Cancel
                </button>
                <button
                  onClick={handleConfirmUpgrade}
                  disabled={isUpgrading}
                  className="px-5 py-2.5 text-sm font-medium bg-primary text-white hover:bg-primary/90 focus:ring-4 focus:ring-primary/30 rounded-lg transition-all flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isUpgrading ? (
                    <>
                      <div className="w-4 h-4 rounded-full border-2 border-white/20 border-t-white animate-spin" />
                      Processing...
                    </>
                  ) : (
                    `Confirm ${selectedPlan.isUpgrade ? "Upgrade" : "Change"}`
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
