"use client";

import { useSession } from "next-auth/react";
import { CheckCircle2, Zap, Rocket, Building2, Users, FileText, HardDrive, Activity } from "lucide-react";
import { useEffect, useState } from "react";

type BillingData = {
  plan: {
    name: string;
    price: number;
    interval: string;
  } | null;
  status: string;
  currentPeriodEnd: string;
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
  billingHistory: Array<{
    id: string;
    amount: number;
    date: string;
    description: string;
    status: string;
  }>;
};

export default function SettingsBillingPage() {
  const { data: session } = useSession();
  const [billingData, setBillingData] = useState<BillingData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  useEffect(() => {
    if (!apiToken) {
      setLoading(false);
      return;
    }

    fetch(`${apiUrl}/api/billing/subscription`, {
      headers: { Authorization: `Bearer ${apiToken}` },
    })
      .then((res) => {
        if (!res.ok) throw new Error("Failed to load billing data");
        return res.json();
      })
      .then((data) => {
        setBillingData(data);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, [apiToken]);

  const planName = billingData?.plan?.name || "Free";
  const status = billingData?.status || "Active";
  const currentPeriodEnd = billingData?.currentPeriodEnd 
    ? new Date(billingData.currentPeriodEnd).toLocaleDateString() 
    : "N/A";

  const formatStorage = (mb: number) => {
    if (mb >= 1024) return `${(mb / 1024).toFixed(1)} GB`;
    return `${mb} MB`;
  };

  const formatTokens = (tokens: number) => {
    if (tokens >= 1000000) return `${(tokens / 1000000).toFixed(1)}M`;
    if (tokens >= 1000) return `${(tokens / 1000).toFixed(0)}K`;
    return tokens.toString();
  };

  const getUsagePercentage = (used: number, max: number | null) => {
    if (max === null) return 0;
    return Math.min((used / max) * 100, 100);
  };

  const getUsageColor = (percentage: number) => {
    if (percentage > 90) return "bg-danger";
    if (percentage > 75) return "bg-amber-500";
    return "bg-primary";
  };

  if (loading) {
    return (
      <div className="flex flex-col gap-6 max-w-4xl animate-in fade-in duration-300">
        <div className="h-8 w-48 bg-surface animate-pulse rounded" />
        <div className="h-64 bg-surface animate-pulse rounded-2xl border border-white/5" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex flex-col gap-6 max-w-4xl">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Plan & Billing</h1>
          <p className="text-sm text-muted">Manage your organization's subscription and usage.</p>
        </div>
        <div className="p-6 bg-danger/10 border border-danger/20 rounded-xl text-danger">
          Unable to load billing data. Please try again later.
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6 max-w-4xl animate-in fade-in duration-300">
      <div>
        <h1 className="text-2xl font-semibold mb-1">Plan & Billing</h1>
        <p className="text-sm text-muted">Manage your organization's subscription and usage.</p>
      </div>

      <div className="bg-gradient-to-br from-surface to-background border border-primary/20 rounded-2xl p-6 relative overflow-hidden">
        <div className="absolute top-0 right-0 w-32 h-32 bg-primary/10 rounded-full blur-[60px] -translate-y-1/2 translate-x-1/2" />
        
        <div className="relative z-10 flex flex-col md:flex-row items-start justify-between gap-6">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <h2 className="text-xl font-semibold text-white">{planName} Plan</h2>
              <span className={`px-2 py-0.5 rounded text-xs font-medium uppercase ${
                status === "Active" ? "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20" :
                status === "Trialing" ? "bg-amber-500/10 text-amber-400 border border-amber-500/20" :
                status === "Suspended" ? "bg-danger/10 text-danger border border-danger/20" :
                "bg-white/10 text-muted border border-white/10"
              }`}>
                {status}
              </span>
            </div>
            <p className="text-sm text-muted mb-4">
              {billingData?.plan?.price === 0 
                ? "Free plan" 
                : billingData?.plan?.price 
                  ? `$${(billingData.plan.price / 100).toFixed(2)} / ${billingData.plan.interval.toLowerCase()}`
                  : "Custom pricing"
              }
            </p>
            <p className="text-xs text-muted">
              Current period ends: <span className="text-white">{currentPeriodEnd}</span>
            </p>
          </div>

          <a 
            href="/billing"
            className="px-5 py-2.5 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all"
          >
            Upgrade Plan
          </a>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-surface border border-white/5 rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
              <Users className="w-5 h-5 text-primary" />
            </div>
            <div>
              <h3 className="text-sm font-medium text-white">Seats</h3>
              <p className="text-xs text-muted">Team members</p>
            </div>
          </div>
          <div className="mb-2">
            <div className="flex justify-between text-sm mb-1">
              <span className="text-white">{billingData?.entitlements.usedSeats || 0}</span>
              <span className="text-muted">{billingData?.entitlements.maxSeats || "Unlimited"}</span>
            </div>
            <div className="h-2 bg-black/40 rounded-full overflow-hidden border border-white/5">
              <div 
                className={`h-full rounded-full transition-all ${getUsageColor(getUsagePercentage(billingData?.entitlements.usedSeats || 0, billingData?.entitlements.maxSeats || null))}`}
                style={{ width: `${getUsagePercentage(billingData?.entitlements.usedSeats || 0, billingData?.entitlements.maxSeats || null)}%` }}
              />
            </div>
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
              <Zap className="w-5 h-5 text-primary" />
            </div>
            <div>
              <h3 className="text-sm font-medium text-white">AI Tokens</h3>
              <p className="text-xs text-muted">Monthly allowance</p>
            </div>
          </div>
          <div className="mb-2">
            <div className="flex justify-between text-sm mb-1">
              <span className="text-white">{formatTokens(billingData?.entitlements.usedTokens || 0)}</span>
              <span className="text-muted">{formatTokens(billingData?.entitlements.maxMonthlyTokens || 0)}</span>
            </div>
            <div className="h-2 bg-black/40 rounded-full overflow-hidden border border-white/5">
              <div 
                className={`h-full rounded-full transition-all ${getUsageColor(getUsagePercentage(billingData?.entitlements.usedTokens || 0, billingData?.entitlements.maxMonthlyTokens || 1))}`}
                style={{ width: `${getUsagePercentage(billingData?.entitlements.usedTokens || 0, billingData?.entitlements.maxMonthlyTokens || 1)}%` }}
              />
            </div>
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
              <HardDrive className="w-5 h-5 text-primary" />
            </div>
            <div>
              <h3 className="text-sm font-medium text-white">Storage</h3>
              <p className="text-xs text-muted">Knowledge base files</p>
            </div>
          </div>
          <div className="mb-2">
            <div className="flex justify-between text-sm mb-1">
              <span className="text-white">{formatStorage(billingData?.entitlements.usedStorageMb || 0)}</span>
              <span className="text-muted">{billingData?.entitlements.maxStorageMb ? formatStorage(billingData.entitlements.maxStorageMb) : "Unlimited"}</span>
            </div>
            <div className="h-2 bg-black/40 rounded-full overflow-hidden border border-white/5">
              <div 
                className={`h-full rounded-full transition-all ${getUsageColor(getUsagePercentage(billingData?.entitlements.usedStorageMb || 0, billingData?.entitlements.maxStorageMb || 1))}`}
                style={{ width: `${getUsagePercentage(billingData?.entitlements.usedStorageMb || 0, billingData?.entitlements.maxStorageMb || 1)}%` }}
              />
            </div>
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
              <FileText className="w-5 h-5 text-primary" />
            </div>
            <div>
              <h3 className="text-sm font-medium text-white">Knowledge Bases</h3>
              <p className="text-xs text-muted">Vector collections</p>
            </div>
          </div>
          <div className="mb-2">
            <div className="flex justify-between text-sm mb-1">
              <span className="text-white">{billingData?.entitlements.usedKnowledgeBases || 0}</span>
              <span className="text-muted">{billingData?.entitlements.maxKnowledgeBases || "Unlimited"}</span>
            </div>
            <div className="h-2 bg-black/40 rounded-full overflow-hidden border border-white/5">
              <div 
                className={`h-full rounded-full transition-all ${getUsageColor(getUsagePercentage(billingData?.entitlements.usedKnowledgeBases || 0, billingData?.entitlements.maxKnowledgeBases || null))}`}
                style={{ width: `${getUsagePercentage(billingData?.entitlements.usedKnowledgeBases || 0, billingData?.entitlements.maxKnowledgeBases || null)}%` }}
              />
            </div>
          </div>
        </div>
      </div>

      <div className="bg-surface border border-white/5 rounded-xl p-6">
        <h3 className="text-lg font-semibold mb-4">Billing History</h3>
        {billingData?.billingHistory && billingData.billingHistory.length > 0 ? (
          <div className="space-y-3">
            {billingData.billingHistory.map((item) => (
              <div key={item.id} className="flex items-center justify-between py-3 border-b border-white/5 last:border-0">
                <div>
                  <p className="text-sm font-medium text-white">{item.description}</p>
                  <p className="text-xs text-muted">{new Date(item.date).toLocaleDateString()}</p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-medium text-white">${(item.amount / 100).toFixed(2)}</p>
                  <span className={`text-xs ${
                    item.status === "Paid" ? "text-emerald-400" : 
                    item.status === "Pending" ? "text-amber-400" : "text-danger"
                  }`}>
                    {item.status}
                  </span>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted text-center py-8">
            No billing history yet. Your subscription is currently on the free plan.
          </p>
        )}
      </div>
    </div>
  );
}
