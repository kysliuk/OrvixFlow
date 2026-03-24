"use client";

import { useSession } from "next-auth/react";
import { CheckCircle2, Zap, Rocket, Building2 } from "lucide-react";
import { useEffect, useState } from "react";

export default function BillingPage() {
  const { data: session } = useSession();
  const currentPlan = session?.user?.plan || "Free";
  const [usageSummary, setUsageSummary] = useState<any[]>([]);

  useEffect(() => {
    if (!session?.apiToken) return;
    fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/billing/summary`, {
      headers: { Authorization: `Bearer ${session.apiToken}` },
    })
      .then((res) => (res.ok ? res.json() : []))
      .then((data) => setUsageSummary(Array.isArray(data) ? data : []))
      .catch(() => setUsageSummary([]));
  }, [session]);

  const plans = [
    {
      name: "Free",
      icon: Zap,
      price: "$0",
      description: "For individuals testing AI workflows.",
      features: ["1 Autonomous Agent", "100 Actions / month", "Basic Webhooks", "Community Support"],
      active: currentPlan === "Free"
    },
    {
      name: "Starter",
      icon: Rocket,
      price: "$49",
      description: "For small teams automating support.",
      features: ["5 Autonomous Agents", "5,000 Actions / month", "Inbox Guardian Module", "Vector Knowledge Base"],
      active: currentPlan === "Starter",
      popular: true
    },
    {
      name: "Enterprise",
      icon: Building2,
      price: "Custom",
      description: "For large organizations with strict SLA.",
      features: ["Unlimited Agents", "Dedicated LLM Instance", "On-Prem Database Option", "24/7 Phone Support"],
      active: currentPlan === "Enterprise"
    }
  ];

  return (
    <div className="flex flex-col gap-8 max-w-6xl">
      
      <div className="text-center max-w-2xl mx-auto mt-6">
        <h1 className="text-3xl font-semibold mb-3 tracking-tight">Manage Subscription</h1>
        <p className="text-muted">Currently using the <span className="text-white font-medium">{currentPlan}</span> plan. Upgrading unlocks powerful new orchestration modules.</p>
      </div>

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

              <div className="mb-6">
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
                {plan.active ? "Current Plan" : plan.price === "Custom" ? "Contact Sales" : "Upgrade"}
              </button>
            </div>
          )
        })}
      </div>

      <div className="bg-surface border border-white/10 rounded-xl p-5">
        <h2 className="text-lg font-semibold mb-3">Metered Usage (Last 30 days)</h2>
        {usageSummary.length === 0 ? (
          <p className="text-sm text-muted">No usage records yet or billing visibility is restricted for your role.</p>
        ) : (
          <div className="space-y-2">
            {usageSummary.map((item, idx) => (
              <div key={`${item.moduleKey}-${item.metricType}-${idx}`} className="flex items-center justify-between text-sm border border-white/5 rounded-lg p-3">
                <span>{item.moduleKey} / {item.metricType}</span>
                <span className="font-medium">{item.quantity}</span>
              </div>
            ))}
          </div>
        )}
      </div>
      
    </div>
  );
}
