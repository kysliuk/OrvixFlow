"use client";

import { useSession } from "next-auth/react";
import { CheckCircle2, Zap, Rocket, Building2 } from "lucide-react";
import { useEffect, useState } from "react";

export default function BillingPage() {
  const { data: session } = useSession();
  
  const [tokenUsage, setTokenUsage] = useState<{ used: number; limit: number; plan: string; renewalDate: string } | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if ((session as any)?.apiToken) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/billing/usage`, {
        headers: { Authorization: `Bearer ${(session as any).apiToken}` },
      })
        .then((res) => (res.ok ? res.json() : null))
        .then((data) => {
          setTokenUsage(data);
          setLoading(false);
        })
        .catch(() => {
          setTokenUsage(null);
          setLoading(false);
        });
    } else {
      setLoading(false);
    }
  }, [session]);

  const currentPlan = tokenUsage?.plan || (session?.user as any)?.plan || "Free";
  const used = tokenUsage?.used || 0;
  const limit = tokenUsage?.limit || 50000;
  const percentageNum = Math.min((used / limit) * 100, 100);
  const percentageStr = percentageNum.toFixed(1);
  const renewalStr = tokenUsage?.renewalDate ? new Date(tokenUsage.renewalDate).toLocaleDateString() : "Next Month";

  const plans = [
    {
      name: "Free",
      icon: Zap,
      price: "$0",
      description: "For individuals testing AI workflows.",
      features: ["1 Autonomous Agent", "50,000 AI Tokens / month", "Basic Webhooks", "Community Support"],
      active: currentPlan.toLowerCase() === "free"
    },
    {
      name: "Starter",
      icon: Rocket,
      price: "$49",
      description: "For small teams automating support.",
      features: ["5 Autonomous Agents", "1,000,000 AI Tokens / month", "Inbox Guardian Module", "Vector Knowledge Base"],
      active: currentPlan.toLowerCase() === "starter",
      popular: true
    },
    {
      name: "Enterprise",
      icon: Building2,
      price: "Custom",
      description: "For large organizations with strict SLA.",
      features: ["Unlimited Agents", "10,000,000 AI Tokens / month", "On-Prem Database Option", "24/7 Phone Support"],
      active: currentPlan.toLowerCase() === "enterprise"
    }
  ];

  return (
    <div className="flex flex-col gap-8 max-w-6xl animate-in fade-in duration-300">
      
      <div className="text-center max-w-2xl mx-auto mt-6 mb-2">
        <h1 className="text-3xl font-semibold mb-3 tracking-tight">Manage Subscription</h1>
        <p className="text-muted">Currently using the <span className="text-white font-medium capitalize">{currentPlan}</span> plan. Upgrading unlocks powerful new orchestration modules and token allowances.</p>
      </div>

      {loading ? (
        <div className="h-32 bg-surface animate-pulse rounded-2xl border border-white/5" />
      ) : (
        <div className="bg-gradient-to-br from-surface to-background border border-primary/20 rounded-2xl p-8 relative overflow-hidden shadow-[0_0_40px_rgba(var(--color-primary),0.05)] text-center sm:text-left">
          <div className="absolute top-0 right-0 w-64 h-64 bg-primary/10 rounded-full blur-[80px] -translate-y-1/2 translate-x-1/2" />
          
          <div className="relative z-10 flex flex-col md:flex-row items-center justify-between gap-8 w-full">
            <div className="flex-1 w-full flex flex-col items-center sm:items-start">
              <h2 className="text-lg font-semibold text-white mb-1">AI Token Quota</h2>
              <p className="text-sm text-muted mb-6 text-center sm:text-left">Your generative intelligence consumption for the current cycle. Resets on <strong>{renewalStr}</strong>.</p>
              
              <div className="w-full flex items-center justify-between mb-2 text-sm font-medium">
                <span className="text-primary tracking-wider uppercase text-[10px] sm:text-xs font-bold bg-primary/10 px-2 py-0.5 rounded border border-primary/20">
                  {used.toLocaleString()} Tokens
                </span>
                <span className="text-muted text-xs tracking-wider uppercase">
                  {limit.toLocaleString()} Limit
                </span>
              </div>
              
              <div className="h-2.5 w-full bg-black/40 rounded-full overflow-hidden shadow-inner border border-white/5 mx-auto sm:mx-0">
                <div 
                  className={`h-full rounded-full transition-all duration-1000 ease-in-out relative ${percentageNum > 90 ? 'bg-danger' : percentageNum > 75 ? 'bg-amber-500' : 'bg-primary shadow-[0_0_10px_var(--accent-primary)]'}`}
                  style={{ width: `${percentageStr}%` }}
                >
                  <div className="absolute inset-0 bg-gradient-to-r from-transparent to-white/20" />
                </div>
              </div>
              <div className="mt-2 text-xs text-muted/70 text-right w-full font-mono">{percentageStr}% Utilized</div>
            </div>
            
            <div className="shrink-0 flex items-center justify-center pt-4 md:pt-0 border-t md:border-t-0 md:border-l border-white/10 w-full md:w-auto md:pl-10">
              <div className="flex flex-col items-center">
                <div className="text-[10px] uppercase tracking-widest text-muted mb-1 font-bold">Billing Status</div>
                <div className="flex items-center gap-2 px-3 py-1.5 rounded-md bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 text-sm font-medium">
                  <CheckCircle2 className="w-4 h-4" /> Active
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-4 relative z-10">
        {plans.map((plan) => {
          const Icon = plan.icon;
          return (
            <div 
              key={plan.name}
              className={`relative flex flex-col bg-surface border rounded-2xl p-6 transition-all duration-300 ${
                plan.active 
                  ? "border-primary shadow-[0_0_30px_var(--accent-glow)] scale-[1.02]" 
                  : "border-white/10 hover:border-white/20 hover:bg-white/[0.02]"
              }`}
            >
              {plan.popular && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2 bg-gradient-to-r from-primary to-danger px-3 py-1 rounded-full text-[10px] uppercase tracking-wider font-bold shadow-lg">
                  Most Popular
                </div>
              )}
              
              {plan.active && (
                <div className="absolute top-4 right-4 text-[10px] uppercase font-bold text-primary bg-primary/10 px-2 py-1 rounded-md border border-primary/20">
                  Current
                </div>
              )}

              <div className="mb-6 mt-2">
                <div className={`w-12 h-12 rounded-xl flex items-center justify-center mb-4 ${plan.active ? "bg-primary/20 text-primary" : "bg-white/5 text-muted"}`}>
                  <Icon className="w-6 h-6" />
                </div>
                <h3 className="text-xl font-bold mb-1">{plan.name}</h3>
                <p className="text-sm text-muted">{plan.description}</p>
              </div>

              <div className="mb-6">
                <span className="text-3xl font-bold">{plan.price}</span>
                {plan.price !== "Custom" && <span className="text-muted text-sm border-l border-white/10 ml-2 pl-2">per month</span>}
              </div>

              <ul className="flex flex-col gap-3 flex-1 mb-8">
                {plan.features.map(f => (
                  <li key={f} className="flex items-start gap-2 text-sm text-white/80">
                    <CheckCircle2 className={`w-4 h-4 mt-0.5 shrink-0 ${plan.active ? "text-primary" : "text-muted"}`} />
                    {f}
                  </li>
                ))}
              </ul>

              <button 
                className={`w-full py-3 rounded-lg text-sm font-medium transition-all ${
                  plan.active 
                    ? "bg-white/5 text-muted border border-white/10 cursor-default" 
                    : plan.popular
                      ? "bg-primary hover:bg-primary/90 text-white shadow-[0_4px_14px_var(--accent-glow)]"
                      : "bg-surface-hover hover:bg-white/10 border border-white/10 text-white"
                }`}
              >
                {plan.active ? "Current Plan" : plan.price === "Custom" ? "Contact Support" : "Upgrade Plan"}
              </button>
            </div>
          )
        })}
      </div>
      
    </div>
  );
}
