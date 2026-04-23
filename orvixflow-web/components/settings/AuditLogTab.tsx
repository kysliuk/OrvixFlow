/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { ShieldCheck, Calendar, Activity, DatabaseBackup } from "lucide-react";

type AuditLog = {
  id: string;
  action: string;
  details: string;
  timestamp: string;
};

export function AuditLogTab() {
  const { data: session } = useSession();
  const [logs, setLogs] = useState<AuditLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchLogs = async () => {
      if (!(session as any)?.apiToken) return;
      try {
        const headers = { Authorization: `Bearer ${(session as any).apiToken}` };
        const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';
        
        const res = await fetch(`${apiUrl}/api/audit`, { headers });
        if (res.ok) {
          setLogs(await res.json());
        } else if (res.status === 403 || res.status === 401) {
          setError("You do not have the required Company Admin permissions to view the security audit trail.");
        } else {
          setError("Failed to retrieve audit logs.");
        }
      } catch (e) {
        console.error("Failed to load audit logs", e);
        setError("A network error occurred.");
      } finally {
        setLoading(false);
      }
    };

    fetchLogs();
  }, [session]);

  if (loading) {
    return (
      <div className="animate-in fade-in duration-300">
        <div className="h-6 w-48 bg-white/5 rounded animate-pulse mb-6" />
        <div className="flex flex-col gap-4">
          <div className="h-12 w-full bg-white/5 rounded animate-pulse" />
          <div className="h-12 w-full bg-white/5 rounded animate-pulse" />
          <div className="h-12 w-full bg-white/5 rounded animate-pulse" />
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="animate-in fade-in duration-300 flex flex-col items-center justify-center py-20 text-center">
        <div className="w-16 h-16 bg-danger/10 rounded-full flex items-center justify-center mb-4">
          <ShieldCheck className="w-8 h-8 text-danger" />
        </div>
        <h3 className="text-lg font-semibold text-white mb-2">Access Restricted</h3>
        <p className="text-muted text-sm max-w-sm">{error}</p>
      </div>
    );
  }

  return (
    <div className="animate-in fade-in duration-300 flex flex-col gap-6">
      <div className="flex items-start justify-between mb-4">
        <div>
          <h2 className="text-lg font-semibold mb-1 flex items-center gap-2">
            <DatabaseBackup className="w-5 h-5 text-emerald-400" />
            Security & Audit Trail
          </h2>
          <p className="text-sm text-muted">Immutable ledger of autonomous AI actions and critical system modifications.</p>
        </div>
        <div className="flex items-center gap-2 px-3 py-1.5 bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 rounded-md text-xs font-semibold tracking-wider uppercase">
          <Activity className="w-3.5 h-3.5" />
          Logging Active
        </div>
      </div>

      <div className="bg-surface border border-white/5 rounded-xl overflow-hidden shadow-lg relative">
        <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-emerald-500/30 to-transparent" />
        
        {logs.length === 0 ? (
          <div className="px-6 py-12 text-center text-muted">
            No audit records found for your tenant.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead className="bg-white/5 text-muted text-[10px] uppercase tracking-widest font-bold border-b border-white/5">
                <tr>
                  <th className="px-6 py-4 whitespace-nowrap">Timestamp (UTC)</th>
                  <th className="px-6 py-4">Action Event</th>
                  <th className="px-6 py-4">Immutable Details</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {logs.map((log) => {
                  const date = new Date(log.timestamp);
                  return (
                    <tr key={log.id} className="hover:bg-white/[0.02] transition-colors group">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="flex items-center gap-2 text-muted font-mono text-xs">
                          <Calendar className="w-3.5 h-3.5 text-white/30" />
                          {date.toLocaleString()}
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-primary tracking-wide font-medium">
                          {log.action}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <div className="text-white/80 font-mono text-[11px] leading-relaxed max-w-xl break-words">
                          {log.details || "No heuristic trace provided."}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

    </div>
  );
}
