/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { ModuleGate } from "@/components/module-gate";
import { Mail, CheckCircle2, AlertTriangle, Send, History, Filter, Search, RotateCw, Database, Activity, Settings, ListChecks } from "lucide-react";

interface InboxEvent {
  eventId: string;
  messageId: string;
  subject: string;
  senderEmail: string;
  senderName: string;
  status: string;
  receivedAtUtc: string;
}

interface EventsApiResponse {
  items: InboxEvent[];
  total: number;
  limit: number;
  offset: number;
}

const statusConfig: Record<string, { icon: typeof CheckCircle2; color: string; bgColor: string; label: string }> = {
  "Ingested": { icon: Mail, color: "text-muted", bgColor: "bg-white/5", label: "Ingested" },
  "Processing": { icon: Activity, color: "text-primary", bgColor: "bg-primary/10", label: "Processing" },
  "Action_Required": { icon: AlertTriangle, color: "text-warning", bgColor: "bg-warning/10", label: "Escalated" },
  "Auto_Approved": { icon: CheckCircle2, color: "text-success", bgColor: "bg-success/10", label: "Replied" },
  "Human_Approved": { icon: CheckCircle2, color: "text-success", bgColor: "bg-success/10", label: "Approved" },
  "Human_Rejected": { icon: AlertTriangle, color: "text-danger", bgColor: "bg-danger/10", label: "Rejected" },
  "Completed": { icon: CheckCircle2, color: "text-success", bgColor: "bg-success/10", label: "Completed" },
  "Failed": { icon: AlertTriangle, color: "text-danger", bgColor: "bg-danger/10", label: "Failed" },
};

export default function InboxGuardianPage() {
  const { data: session } = useSession();
  const [testEmail, setTestEmail] = useState("");
  const [responseLog, setResponseLog] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  
  // Events list state
  const [events, setEvents] = useState<InboxEvent[]>([]);
  const [loadingEvents, setLoadingEvents] = useState(true);
  const [eventsError, setEventsError] = useState<string | null>(null);
  const [selectedEvent, setSelectedEvent] = useState<InboxEvent | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");

  useEffect(() => {
    fetchEvents();
  }, [statusFilter]);

  const fetchEvents = async () => {
    setLoadingEvents(true);
    setEventsError(null);
    
    try {
      const apiToken = (session as any)?.apiToken;
      if (!apiToken) {
        setEventsError("Not authenticated");
        return;
      }

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      let url = `${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/events?limit=100`;
      if (statusFilter) {
        url += `&status=${encodeURIComponent(statusFilter)}`;
      }

      const res = await fetch(url, { headers });

      if (!res.ok) {
        throw new Error(`Failed to fetch events: ${res.status}`);
      }

      const data: EventsApiResponse = await res.json();
      setEvents(data.items || []);
      
      if (data.items?.length > 0 && !selectedEvent) {
        setSelectedEvent(data.items[0]);
      }
    } catch (e: any) {
      setEventsError(e.message);
    } finally {
      setLoadingEvents(false);
    }
  };

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

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);
    const days = Math.floor(diff / 86400000);
    
    if (minutes < 1) return "Just now";
    if (minutes < 60) return `${minutes} mins ago`;
    if (hours < 24) return `${hours} hr${hours > 1 ? 's' : ''} ago`;
    if (days < 7) return `${days} day${days > 1 ? 's' : ''} ago`;
    return date.toLocaleDateString();
  };

  const getStatusBadge = (status: string) => {
    const config = statusConfig[status] || statusConfig["Ingested"];
    const Icon = config.icon;
    return (
      <div className={`flex items-center gap-1.5 text-[10px] uppercase font-bold ${config.color} tracking-wider ${config.bgColor} px-2 py-0.5 rounded-sm w-fit border border-current/10`}>
        <Icon className="w-3 h-3" /> {config.label}
      </div>
    );
  };

  const filteredEvents = events.filter(event => {
    if (!searchQuery) return true;
    const query = searchQuery.toLowerCase();
    return event.subject.toLowerCase().includes(query) || 
           event.senderEmail.toLowerCase().includes(query);
  });

  return (
    <ModuleGate moduleKey="inbox-guardian" fallbackMessage="Inbox Guardian is available but you cannot execute it in this scope.">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Inbox Guardian</h1>
          <p className="text-sm text-muted">Real-time autonomous email triage and response routing.</p>
        </div>
        <div className="flex items-center gap-3">
          <Link href="/inbox/pending" className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
            <ListChecks className="w-4 h-4" /> Pending
          </Link>
          <Link href="/inbox/history" className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
            <History className="w-4 h-4" /> History
          </Link>
          <Link href="/settings/inbox" className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
            <Settings className="w-4 h-4" /> Settings
          </Link>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="bg-surface border border-white/10 rounded-md px-3 py-1.5 text-sm text-muted hover:text-white transition-colors cursor-pointer"
          >
            <option value="">All Statuses</option>
            <option value="Action_Required">Escalated</option>
            <option value="Auto_Approved">Auto Replied</option>
            <option value="Completed">Completed</option>
            <option value="Failed">Failed</option>
          </select>
          <button 
            onClick={fetchEvents}
            className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors"
          >
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
                placeholder="Search emails..." 
                className="w-full bg-background border border-white/10 rounded-md py-1.5 pl-9 pr-4 text-sm text-white focus:outline-none focus:border-primary/50"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
          </div>
          <div className="flex-1 overflow-y-auto custom-scrollbar">
            {loadingEvents && (
              <div className="flex items-center justify-center h-32">
                <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
              </div>
            )}
            
            {eventsError && (
              <div className="p-4 text-danger text-sm">{eventsError}</div>
            )}
            
            {!loadingEvents && !eventsError && filteredEvents.length === 0 && (
              <div className="flex flex-col items-center justify-center h-32 text-muted">
                <Mail className="w-8 h-8 mb-2 opacity-50" />
                <p className="text-sm">No emails found</p>
              </div>
            )}
            
            {!loadingEvents && !eventsError && filteredEvents.map((event) => (
              <div 
                key={event.eventId} 
                onClick={() => setSelectedEvent(event)}
                className={`p-4 border-b border-white/5 cursor-pointer transition-all ${
                  selectedEvent?.eventId === event.eventId
                    ? "bg-primary/10 border-l-[3px] border-l-primary border-b-primary/20" 
                    : "hover:bg-white/5 border-l-[3px] border-l-transparent"
                }`}
              >
                <div className="flex justify-between items-start mb-1.5">
                  <h3 className={`font-medium text-sm truncate pr-4 ${selectedEvent?.eventId === event.eventId ? "text-primary" : "text-white"}`}>
                    {event.subject || "(No Subject)"}
                  </h3>
                  <span className="text-[10px] text-muted whitespace-nowrap pt-1">
                    {formatTime(event.receivedAtUtc)}
                  </span>
                </div>
                <p className={`line-clamp-2 text-xs leading-snug ${selectedEvent?.eventId === event.eventId ? "text-white/80" : "text-muted"}`}>
                  {event.senderEmail}
                </p>
                <div className="mt-3 flex items-center justify-between">
                  {getStatusBadge(event.status)}
                  
                  {selectedEvent?.eventId === event.eventId && (
                    <div className="text-[10px] font-medium text-primary">Viewing</div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right Pane: Detail View + Test Console (70%) */}
        <div className="xl:col-span-8 lg:col-span-7 flex flex-col gap-6 h-full overflow-y-auto">
          
          {selectedEvent && (
            <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg relative overflow-hidden">
              <div className="absolute top-0 right-0 w-[400px] h-[400px] bg-primary/5 blur-[100px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
              
              <div className="flex items-center justify-between border-b border-white/10 pb-4 mb-4 relative z-10">
                <div>
                  <h2 className="text-xl font-semibold text-white mb-1">{selectedEvent.subject || "(No Subject)"}</h2>
                  <div className="flex items-center gap-3 text-xs text-muted">
                    <span>From: {selectedEvent.senderEmail}</span>
                    <span>•</span>
                    <span>Received: {new Date(selectedEvent.receivedAtUtc).toLocaleString()}</span>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {getStatusBadge(selectedEvent.status)}
                </div>
              </div>

              <div className="text-sm text-white/60 mb-6">
                Message ID: <code className="text-white/40">{selectedEvent.messageId}</code>
              </div>
            </div>
          )}
          
          <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg relative overflow-hidden flex flex-col">
            <div className="absolute top-0 right-0 w-[400px] h-[400px] bg-primary/5 blur-[100px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
            
            <div className="flex items-center justify-between border-b border-white/10 pb-4 mb-4 relative z-10">
              <div>
                <h2 className="text-xl font-semibold text-white mb-1">Test Console</h2>
                <p className="text-xs text-muted">Test the AI triage engine with custom prompts</p>
              </div>
            </div>

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
