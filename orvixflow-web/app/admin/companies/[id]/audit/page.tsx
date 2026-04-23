/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, FileText, Clock, ChevronLeft, ChevronRight } from "lucide-react";

interface AuditEntry {
  id: string;
  action: string;
  actor: string;
  entityId: string;
  previousState: string;
  newState: string;
  decisionDetails: string;
  timestamp: string;
}

export default function CompanyAuditPage() {
  const { data: session } = useSession();
  const params = useParams();
  const router = useRouter();
  const companyId = params.id as string;

  const [entries, setEntries] = useState<AuditEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(0);
  const [total, setTotal] = useState(0);
  const limit = 50;

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken && companyId) loadAudit();
  }, [apiToken, companyId, page]);

  const loadAudit = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL}/api/admin/companies/${companyId}/audit?limit=${limit}&offset=${page * limit}`,
        { headers: { "Authorization": `Bearer ${apiToken}` } }
      );
      if (!res.ok) throw new Error(`Failed to load: ${res.status}`);
      const data = await res.json();
      setEntries(data);
      setTotal(data.length);
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const formatDate = (date: string) => {
    return new Date(date).toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit"
    });
  };

  const getActionColor = (action: string) => {
    if (action.includes("Plan")) return "text-primary bg-primary/10";
    if (action.includes("Suspend")) return "text-danger bg-danger/10";
    if (action.includes("Reactivate")) return "text-success bg-success/10";
    if (action.includes("Trial")) return "text-warning bg-warning/10";
    if (action.includes("Override")) return "text-purple-400 bg-purple-500/10";
    return "text-white/70 bg-white/10";
  };

  if (loading) return <div className="text-muted text-sm">Loading audit log...</div>;
  if (error) return <div className="text-danger text-sm">{error}</div>;

  return (
    <div>
      <div className="flex items-center gap-4 mb-6">
        <Link href={`/admin/companies/${companyId}`} className="text-muted hover:text-white transition-colors">
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
            <FileText className="w-6 h-6 text-danger" /> Audit Log
          </h1>
          <p className="text-sm text-muted">History of all actions performed on this company.</p>
        </div>
      </div>

      <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
        {entries.length === 0 ? (
          <div className="px-4 py-12 text-center text-muted">
            <Clock className="w-12 h-12 mx-auto mb-3 text-muted/50" />
            <p>No audit entries found</p>
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-surface-hover/50 border-b border-white/10">
              <tr>
                <th className="text-left px-4 py-3 text-muted font-medium">Timestamp</th>
                <th className="text-left px-4 py-3 text-muted font-medium">Action</th>
                <th className="text-left px-4 py-3 text-muted font-medium">Actor</th>
                <th className="text-left px-4 py-3 text-muted font-medium">Details</th>
              </tr>
            </thead>
            <tbody>
              {entries.map(entry => (
                <tr key={entry.id} className="border-b border-white/5 hover:bg-white/5">
                  <td className="px-4 py-3 text-muted whitespace-nowrap">
                    {formatDate(entry.timestamp)}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`text-xs px-2 py-0.5 rounded ${getActionColor(entry.action)}`}>
                      {entry.action}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-white/70">{entry.actor}</td>
                  <td className="px-4 py-3 text-muted max-w-md truncate">
                    {entry.decisionDetails}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {entries.length >= limit && (
        <div className="flex items-center justify-between mt-4">
          <span className="text-sm text-muted">
            Showing {page * limit + 1}–{(page + 1) * limit}
          </span>
          <div className="flex gap-2">
            <button
              onClick={() => setPage(p => Math.max(0, p - 1))}
              disabled={page === 0}
              className="px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <button
              onClick={() => setPage(p => p + 1)}
              disabled={entries.length < limit}
              className="px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
