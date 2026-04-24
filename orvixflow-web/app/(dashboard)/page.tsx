"use client";

import { useSession } from "next-auth/react";
import { Activity, ShieldCheck, Zap, ArrowRight, MousePointerClick, Cpu } from "lucide-react";
import Link from "next/link";

import { getNoOrgState, hasActiveCompanyScope } from "@/lib/dashboard-access";

export default function Dashboard() {
  const { data: session } = useSession();

  const isPro = session?.user?.plan === "Starter" || session?.user?.plan === "Pro" || session?.user?.plan === "Enterprise";
  const hasCompanyScope = hasActiveCompanyScope(session?.user?.activeCompanyId);
  const noOrgState = getNoOrgState("dashboard");

  return (
    <div className="flex flex-col gap-8 max-w-7xl">
      
      {/* Header */}
      <div>
        <h1 className="text-3xl font-semibold mb-2">Welcome back, {session?.user?.name?.split(" ")[0] || "Agent"}</h1>
        <p className="text-muted text-sm max-w-2xl">
          Here is an overview of your AI orchestration workflow and active modules.
        </p>
      </div>

      {/* Telemetry Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-surface border border-white/5 rounded-xl p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-success/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-success/20 transition-all" />
          <div className="flex justify-between items-start mb-4">
            <span className="text-sm font-medium text-muted">System Status</span>
            <div className="w-8 h-8 rounded-full bg-success/10 flex items-center justify-center">
              <ShieldCheck className="w-4 h-4 text-success" />
            </div>
          </div>
          <div className="text-2xl font-bold tracking-tight">Operational</div>
          <div className="mt-1 flex items-center gap-2 text-xs text-muted">
            <span className="w-1.5 h-1.5 rounded-full bg-success animate-pulse" />
            All regions healthy
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-primary/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-primary/20 transition-all" />
          <div className="flex justify-between items-start mb-4">
            <span className="text-sm font-medium text-muted">Active Plan</span>
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center">
              <Zap className="w-4 h-4 text-primary" />
            </div>
          </div>
          <div className="text-2xl font-bold tracking-tight">{session?.user?.plan || "Free"}</div>
          <div className="mt-1 text-xs text-muted">
            {isPro ? "Premium features unlocked" : "Upgrade for more power"}
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-blue-500/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-blue-500/20 transition-all" />
          <div className="flex justify-between items-start mb-4">
            <span className="text-sm font-medium text-muted">Agent Inferences</span>
            <div className="w-8 h-8 rounded-full bg-blue-500/10 flex items-center justify-center">
              <Cpu className="w-4 h-4 text-blue-400" />
            </div>
          </div>
          <div className="text-2xl font-bold tracking-tight">12,491</div>
          <div className="mt-1 text-xs text-success flex items-center gap-1 font-medium">
            &uarr; 14% this week
          </div>
        </div>

        <div className="bg-surface border border-white/5 rounded-xl p-5 relative overflow-hidden group">
          <div className="absolute top-0 right-0 w-24 h-24 bg-orange-500/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-orange-500/20 transition-all" />
          <div className="flex justify-between items-start mb-4">
            <span className="text-sm font-medium text-muted">Automations Triggered</span>
            <div className="w-8 h-8 rounded-full bg-orange-500/10 flex items-center justify-center">
              <Activity className="w-4 h-4 text-orange-400" />
            </div>
          </div>
          <div className="text-2xl font-bold tracking-tight">842</div>
          <div className="mt-1 text-xs text-muted">
            Via n8n webhooks
          </div>
        </div>
      </div>

      {/* Quick Actions split layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Interactive primary zone */}
        <div className="lg:col-span-2 bg-gradient-to-br from-surface to-background border border-primary/20 rounded-2xl p-8 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-[500px] h-[500px] bg-primary/5 blur-[100px] rounded-full pointer-events-none -translate-y-1/4 translate-x-1/4" />
          
          <h2 className="text-xl font-semibold mb-6 flex items-center gap-2">
            <MousePointerClick className="w-5 h-5 text-primary" />
            Quick Actions
          </h2>
          
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 relative z-10">
            {hasCompanyScope ? (
              <>
                <Link 
                  href="/inbox" 
                  className="flex flex-col p-6 rounded-xl border border-white/10 bg-white/5 hover:bg-primary/10 hover:border-primary/50 transition-all group"
                >
                  <h3 className="font-semibold text-white group-hover:text-primary transition-colors flex items-center gap-2 mb-2">
                    Launch Inbox Guardian <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                  </h3>
                  <p className="text-sm text-muted">Monitor real-time email triage, automated replies, and human escalations from the AI engine.</p>
                </Link>

                <Link 
                  href="/knowledge" 
                  className="flex flex-col p-6 rounded-xl border border-white/10 bg-white/5 hover:bg-primary/10 hover:border-primary/50 transition-all group"
                >
                  <h3 className="font-semibold text-white group-hover:text-primary transition-colors flex items-center gap-2 mb-2">
                    Update Knowledge Base <ArrowRight className="w-4 h-4 group-hover:translate-x-1 transition-transform" />
                  </h3>
                  <p className="text-sm text-muted">Upload new policies and facts to improve agent accuracy and autonomy rates.</p>
                </Link>
              </>
            ) : (
              <div className="sm:col-span-2 rounded-xl border border-white/10 bg-white/5 p-6">
                <h3 className="font-semibold text-white mb-2">{noOrgState.title}</h3>
                <p className="text-sm text-muted mb-4">{noOrgState.description}</p>
                <Link
                  href={noOrgState.ctaHref}
                  className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2.5 text-sm font-medium text-white transition-all hover:bg-primary/90"
                >
                  {noOrgState.ctaLabel}
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
            )}
          </div>
        </div>

        {/* Passive monitoring zone */}
        <div className="lg:col-span-1 bg-surface border border-white/5 rounded-2xl p-6 flex flex-col">
          <h2 className="text-lg font-semibold mb-4 text-white">Recent Activity</h2>
          
          <div className="flex flex-col gap-4 flex-1">
            <div className="flex items-start gap-3">
              <div className="w-2 h-2 rounded-full bg-success mt-1.5 shrink-0 shadow-[0_0_8px_var(--accent-lime)]" />
              <div>
                <p className="text-sm font-medium">Ticket #492 Auto-Resolved</p>
                <p className="text-xs text-muted">Agent successfully refunded order via Stripe intent.</p>
                <p className="text-[10px] text-white/30 mt-1">2 mins ago</p>
              </div>
            </div>
            
            <div className="flex items-start gap-3">
              <div className="w-2 h-2 rounded-full bg-danger mt-1.5 shrink-0 shadow-[0_0_8px_var(--accent-rose)]" />
              <div>
                <p className="text-sm font-medium">Escalation Required</p>
                <p className="text-xs text-muted">Sentiment analysis flagged angry user. Routed to human queue.</p>
                <p className="text-[10px] text-white/30 mt-1">15 mins ago</p>
              </div>
            </div>

            <div className="flex items-start gap-3 opacity-60">
              <div className="w-2 h-2 rounded-full bg-primary mt-1.5 shrink-0" />
              <div>
                <p className="text-sm font-medium">Document Vectorized</p>
                <p className="text-xs text-muted">&quot;Company Holiday Policy 2026&quot; ingrained into agent memory.</p>
                <p className="text-[10px] text-white/30 mt-1">1 hour ago</p>
              </div>
            </div>
          </div>
          
          <button className="text-xs font-semibold text-primary hover:text-white transition-colors mt-4 self-start">
            View full audit log &rarr;
          </button>
        </div>

      </div>
    </div>
  );
}
