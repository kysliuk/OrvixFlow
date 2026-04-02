"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { ModuleGate } from "@/components/module-gate";
import { Clock, RefreshCw, CheckCircle2, AlertTriangle, XCircle, ArrowUpRight, ArrowDownRight } from "lucide-react";

interface HistoryEvent {
  eventId: string;
  messageId: string;
  subject: string;
  senderEmail: string;
  senderName: string;
  status: string;
  receivedAtUtc: string;
  category?: string;
  confidenceScore?: number;
}

export default function InboxHistoryPage() {
  const { data: session } = useSession();
  const [events, setEvents] = useState<HistoryEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>("");
  const [page, setPage] = useState(0);
  const [total, setTotal] = useState(0);
  const limit = 20;

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken) loadEvents();
  }, [apiToken, statusFilter, page]);

  const getHeaders = () => {
    const headers: Record<string, string> = { "Authorization": `Bearer ${apiToken}` };
    const imp = localStorage.getItem("impersonateTenantId");
    if (imp) headers["X-Impersonate-Tenant"] = imp;
    return headers;
  };

  const loadEvents = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({ limit: String(limit), offset: String(page * limit) });
      if (statusFilter) params.set("status", statusFilter);

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/events?${params}`, { headers: getHeaders() });
      if (!res.ok) throw new Error(`Failed to fetch: ${res.status}`);

      const data = await res.json();
      setEvents(data.items || []);
      setTotal(data.total || 0);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "Completed":
      case "Auto_Approved":
      case "Human_Approved":
        return <CheckCircle2 className="w-4 h-4 text-success" />;
      case "Failed":
        return <XCircle className="w-4 h-4 text-danger" />;
      case "Action_Required":
        return <AlertTriangle className="w-4 h-4 text-warning" />;
      default:
        return <Clock className="w-4 h-4 text-muted" />;
    }
  };

  const totalPages = Math.ceil(total / limit);

  return (
    <ModuleGate moduleKey="inbox-guardian" fallbackMessage="Inbox Guardian is only available on the Starter plan or higher.">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Inbox History</h1>
          <p className="text-sm text-muted">Log of all processed messages and their outcomes.</p>
        </div>
        <button onClick={loadEvents} className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
          <RefreshCw className="w-4 h-4" /> Refresh
        </button>
      </div>

      <div className="flex gap-3 mb-4">
        <select
          value={statusFilter}
          onChange={e => { setStatusFilter(e.target.value); setPage(0); }}
          className="bg-surface border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
        >
          <option value="">All Statuses</option>
          <option value="Completed">Completed</option>
          <option value="Auto_Approved">Auto-Approved</option>
          <option value="Human_Approved">Human-Approved</option>
          <option value="Action_Required">Action Required</option>
          <option value="Failed">Failed</option>
          <option value="Ingested">Ingested</option>
        </select>
      </div>

      <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-surface-hover/50 border-b border-white/10">
            <tr>
              <th className="text-left px-4 py-3 text-muted font-medium">Status</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Sender</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Subject</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Date</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <>
                {Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} className="border-b border-white/5">
                    <td className="px-4 py-3"><div className="h-4 bg-white/5 rounded w-20 animate-pulse" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-white/5 rounded w-32 animate-pulse" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-white/5 rounded w-48 animate-pulse" /></td>
                    <td className="px-4 py-3"><div className="h-4 bg-white/5 rounded w-24 animate-pulse" /></td>
                  </tr>
                ))}
              </>
            )}
            {!loading && error && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-danger">{error}</td></tr>
            )}
            {!loading && !error && events.length === 0 && (
              <tr><td colSpan={4} className="px-4 py-8 text-center text-muted">No events found</td></tr>
            )}
            {!loading && !error && events.map(e => (
              <tr key={e.eventId} className="border-b border-white/5 hover:bg-white/5">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {getStatusIcon(e.status)}
                    <span className="text-xs">{e.status.replace(/_/g, " ")}</span>
                  </div>
                </td>
                <td className="px-4 py-3">
                  <div className="text-white">{e.senderName || e.senderEmail}</div>
                  {e.senderName && <div className="text-xs text-muted">{e.senderEmail}</div>}
                </td>
                <td className="px-4 py-3 text-muted max-w-md truncate">{e.subject}</td>
                <td className="px-4 py-3 text-muted whitespace-nowrap">
                  {new Date(e.receivedAtUtc).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-4">
          <span className="text-sm text-muted">
            Showing {page * limit + 1}–{Math.min((page + 1) * limit, total)} of {total}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage(p => Math.max(0, p - 1))}
              disabled={page === 0}
              className="px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Previous
            </button>
            <button
              onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
              disabled={page >= totalPages - 1}
              className="px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </ModuleGate>
  );
}
