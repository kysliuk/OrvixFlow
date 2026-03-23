"use client";

import { useState } from "react";
import { useSession } from "next-auth/react";
import { ModuleGate } from "@/components/module-gate";
import { Mail, CheckCircle2, AlertTriangle, Send, History, Filter, Search, RotateCw, Database, Activity } from "lucide-react";

export default function InboxGuardianPage() {
  const { data: session } = useSession();
  const [testEmail, setTestEmail] = useState("");
  const [responseLog, setResponseLog] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);

  // Mock list for the Master pane
  const items = [
    { id: 1, subject: "Refund Policy Inquiry", status: "replied", exactTime: "2 mins ago", excerpt: "Autonomous reply drafted with information from docs/refund-policy.", active: true },
    { id: 2, subject: "Angry Enterprise Client", status: "escalated", exactTime: "14 mins ago", excerpt: "Sentiment analysis indicated high frustration. Custom plan constraints found. Escalating to human queue...", active: false },
    { id: 3, subject: "Reset password request", status: "pending", exactTime: "1 hr ago", excerpt: "Standard template reply queued for sending.", active: false },
    { id: 4, subject: "BUG: Database unreachable", status: "escalated", exactTime: "2 hrs ago", excerpt: "Agent logic skipped. Direct human routing triggered via webhook.", active: false },
  ];

  const handleTestEngine = async () => {
    if (!testEmail) return;
    setIsProcessing(true);
    setResponseLog(null);
    
    if (!(session as any)?.apiToken) {
      setResponseLog("CRITICAL ERROR: API Token is missing from the NextAuth session. Ensure Next.js is restarted if .env.local changed.");
      setIsProcessing(false);
      return;
    }

    try {
      const imp = localStorage.getItem("impersonateTenantId");
      const headers: any = {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${(session as any)?.apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/inbox/process`, {
        method: "POST",
        headers,
        body: JSON.stringify({ prompt: testEmail }),
      });
      const text = await res.text();
      try {
        const data = JSON.parse(text);
        setResponseLog(JSON.stringify(data, null, 2));
      } catch {
        setResponseLog(`Request failed: ${res.status} ${res.statusText}`);
      }
    } catch (e: any) {
      setResponseLog(`Error: ${e.message}`);
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <ModuleGate requiredPlan="Starter" fallbackMessage="Inbox Guardian is only available on the Starter plan or higher.">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Inbox Guardian</h1>
          <p className="text-sm text-muted">Real-time autonomous email triage and response routing.</p>
        </div>
        <div className="flex items-center gap-3">
          <button className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
            <Filter className="w-4 h-4" /> Filter
          </button>
          <button className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
            <RotateCw className="w-4 h-4" /> Refresh
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 h-[calc(100vh-12rem)] min-h-[600px]">
        
        {/* Left Pane: Master List (30%) */}
        <div className="xl:col-span-4 lg:col-span-5 flex flex-col bg-surface border border-white/10 rounded-xl overflow-hidden shadow-lg h-full">
          <div className="p-3 border-b border-white/10 bg-surface-hover/50">
            <div className="relative">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
              <input 
                type="text" 
                placeholder="Search history..." 
                className="w-full bg-background border border-white/10 rounded-md py-1.5 pl-9 pr-4 text-sm text-white focus:outline-none focus:border-primary/50"
              />
            </div>
          </div>
          <div className="flex-1 overflow-y-auto custom-scrollbar">
            {items.map((item) => (
              <div 
                key={item.id} 
                className={`p-4 border-b border-white/5 cursor-pointer transition-all ${
                  item.active 
                    ? "bg-primary/10 border-l-[3px] border-l-primary border-b-primary/20" 
                    : "hover:bg-white/5 border-l-[3px] border-l-transparent"
                }`}
              >
                <div className="flex justify-between items-start mb-1.5">
                  <h3 className={`font-medium text-sm truncate pr-4 ${item.active ? "text-primary" : "text-white"}`}>
                    {item.subject}
                  </h3>
                  <span className="text-[10px] text-muted whitespace-nowrap pt-1">
                    {item.exactTime}
                  </span>
                </div>
                <p className={`line-clamp-2 text-xs leading-snug ${item.active ? "text-white/80" : "text-muted"}`}>
                  {item.excerpt}
                </p>
                <div className="mt-3 flex items-center justify-between">
                  {item.status === "replied" && (
                    <div className="flex items-center gap-1.5 text-[10px] uppercase font-bold text-success tracking-wider bg-success/10 px-2 py-0.5 rounded-sm w-fit border border-success/20">
                      <CheckCircle2 className="w-3 h-3" /> Replied
                    </div>
                  )}
                  {item.status === "escalated" && (
                    <div className="flex items-center gap-1.5 text-[10px] uppercase font-bold text-danger tracking-wider bg-danger/10 px-2 py-0.5 rounded-sm w-fit border border-danger/20">
                      <AlertTriangle className="w-3 h-3" /> Escalated
                    </div>
                  )}
                  {item.status === "pending" && (
                    <div className="flex items-center gap-1.5 text-[10px] uppercase font-bold text-muted tracking-wider bg-white/5 px-2 py-0.5 rounded-sm w-fit border border-white/10">
                      <History className="w-3 h-3" /> Pending
                    </div>
                  )}
                  
                  {item.active && (
                    <div className="text-[10px] font-medium text-primary">Viewing</div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right Pane: Detail View + Test Console (70%) */}
        <div className="xl:col-span-8 lg:col-span-7 flex flex-col gap-6 h-full overflow-y-auto">
          
          <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg relative overflow-hidden flex flex-col h-full">
            <div className="absolute top-0 right-0 w-[400px] h-[400px] bg-primary/5 blur-[100px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
            
            {/* Header of Detail View */}
            <div className="flex items-center justify-between border-b border-white/10 pb-4 mb-4 relative z-10">
              <div>
                <h2 className="text-xl font-semibold text-white mb-1">Refund Policy Inquiry</h2>
                <div className="flex items-center gap-3 text-xs text-muted">
                  <span>From: cus_9x8f@example.com</span>
                  <span>•</span>
                  <span>Received: Today at 10:42 AM</span>
                </div>
              </div>
              <div className="flex items-center gap-2">
                <button className="px-3 py-1.5 border border-white/10 rounded-md text-sm font-medium hover:bg-white/5 transition-all">
                  View Source
                </button>
                <button className="px-3 py-1.5 bg-danger/10 text-danger border border-danger/20 rounded-md text-sm font-medium hover:bg-danger/20 transition-all">
                  Take Over
                </button>
              </div>
            </div>

            {/* Content of Detail View / Interactive Console */}
            <div className="flex-1 flex flex-col gap-4 relative z-10 overflow-y-auto pr-2 custom-scrollbar">
              
              <div className="flex flex-col gap-2">
                <label className="text-xs font-bold uppercase tracking-widest text-muted">Incoming Sideloaded Email</label>
                <textarea
                  className="w-full h-32 bg-background border border-white/10 rounded-lg p-3 text-sm text-white focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-mono resize-none"
                  placeholder="e.g., Hi, I am evaluating OrvixFlow. Can I deploy my internal documents securely?"
                  value={testEmail}
                  onChange={(e) => setTestEmail(e.target.value)}
                />
                <button 
                  onClick={handleTestEngine}
                  disabled={isProcessing || !testEmail}
                  className="mt-2 self-start flex items-center justify-center gap-2 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-6 py-2.5 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all disabled:opacity-50"
                >
                  {isProcessing ? "Agent is thinking..." : <><Send className="w-4 h-4" /> Trigger Autonomous Triage</>}
                </button>
              </div>

              {responseLog && (
                <div className="flex flex-col gap-3 mt-4 animate-in fade-in slide-in-from-bottom-2 duration-300">
                  <h3 className="text-xs font-bold uppercase tracking-widest text-primary flex items-center gap-2">
                    <Activity className="w-4 h-4" />
                    Agent Orchestration Result
                  </h3>
                  
                  <div className="bg-[#0a0710] rounded-lg border border-primary/20 p-4 overflow-x-auto shadow-[0_0_15px_rgba(139,92,246,0.1)]">
                    <pre className="text-xs text-white/90 font-mono whitespace-pre-wrap leading-relaxed">
                      {responseLog}
                    </pre>
                  </div>
                </div>
              )}

            </div>
          </div>

        </div>
      </div>
    </ModuleGate>
  );
}
