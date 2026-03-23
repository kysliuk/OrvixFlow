"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { Activity, Database, Users, Building, ShieldAlert } from "lucide-react";

interface GlobalMetrics {
  totalTenants: number;
  totalUsers: number;
  totalMemoryChunks: number;
  premiumTenants: number;
}

export default function AdminDashboardPage() {
  const { data: session } = useSession();
  const [metrics, setMetrics] = useState<GlobalMetrics | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if ((session as any)?.apiToken) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/metrics`, {
        headers: { "Authorization": `Bearer ${(session as any).apiToken}` }
      })
      .then(res => {
        if (!res.ok) throw new Error("Failed to load metrics");
        return res.json();
      })
      .then(data => setMetrics(data))
      .catch(e => setError(e.message));
    }
  }, [session]);

  return (
    <div className="flex flex-col gap-8 max-w-6xl">
      <div>
        <h1 className="text-3xl font-semibold mb-2">Global Platform Health</h1>
        <p className="text-white/60 text-sm max-w-2xl font-mono">
          AGGREGATED_METRICS_READY // CROSS_TENANT_QUERY_ACTIVE
        </p>
      </div>

      {error && (
        <div className="bg-danger/10 border border-danger/30 text-danger p-4 rounded-lg text-sm flex items-center gap-2">
          <ShieldAlert className="w-5 h-5" />
          {error}
        </div>
      )}

      {!metrics && !error && (
        <div className="text-danger animate-pulse font-mono text-sm">FETCHING DIRECT TELEMETRY...</div>
      )}

      {metrics && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-black/40 border border-danger/20 rounded-xl p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-danger/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-danger/20 transition-all" />
            <div className="flex justify-between items-start mb-4">
              <span className="text-sm font-medium text-white/60">Registered Tenants</span>
              <div className="w-8 h-8 rounded-full bg-danger/10 flex items-center justify-center">
                <Building className="w-4 h-4 text-danger" />
              </div>
            </div>
            <div className="text-3xl font-bold tracking-tight text-white">{metrics.totalTenants}</div>
          </div>

          <div className="bg-black/40 border border-danger/20 rounded-xl p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-danger/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-danger/20 transition-all" />
            <div className="flex justify-between items-start mb-4">
              <span className="text-sm font-medium text-white/60">Active Users</span>
              <div className="w-8 h-8 rounded-full bg-danger/10 flex items-center justify-center">
                <Users className="w-4 h-4 text-danger" />
              </div>
            </div>
            <div className="text-3xl font-bold tracking-tight text-white">{metrics.totalUsers}</div>
          </div>

          <div className="bg-black/40 border border-danger/20 rounded-xl p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-danger/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-danger/20 transition-all" />
            <div className="flex justify-between items-start mb-4">
              <span className="text-sm font-medium text-white/60">Premium Subscriptions</span>
              <div className="w-8 h-8 rounded-full bg-danger/10 flex items-center justify-center">
                <Activity className="w-4 h-4 text-danger" />
              </div>
            </div>
            <div className="text-3xl font-bold tracking-tight text-white">{metrics.premiumTenants}</div>
            <div className="mt-1 text-xs text-danger flex items-center gap-1 font-medium">
              Generating active revenue
            </div>
          </div>

          <div className="bg-black/40 border border-danger/20 rounded-xl p-5 relative overflow-hidden group">
            <div className="absolute top-0 right-0 w-24 h-24 bg-danger/10 blur-2xl rounded-full -translate-y-1/2 translate-x-1/3 group-hover:bg-danger/20 transition-all" />
            <div className="flex justify-between items-start mb-4">
              <span className="text-sm font-medium text-white/60">pgvector Documents</span>
              <div className="w-8 h-8 rounded-full bg-danger/10 flex items-center justify-center">
                <Database className="w-4 h-4 text-danger" />
              </div>
            </div>
            <div className="text-3xl font-bold tracking-tight text-white">{metrics.totalMemoryChunks}</div>
          </div>
        </div>
      )}

      {/* Advanced Telemetry Panel */}
      <div className="bg-black/40 border border-danger/20 rounded-2xl p-6 mt-4">
        <h2 className="text-lg font-semibold mb-4 text-white flex items-center gap-2">
          <Activity className="w-5 h-5 text-danger" /> Live Triage Inference Stream
        </h2>
        <div className="flex flex-col gap-2">
          <div className="text-xs text-white/40 font-mono italic">WebSocket tracking disabled in MVP... Fetching static audit block...</div>
          <div className="p-4 bg-black/60 rounded-lg border border-danger/10 font-mono text-[10px] leading-relaxed text-white/60">
            [SYS] Kernel Online. Models synchronized: llama-3.3-70b-versatile<br/>
            [INF] Tenant 6b129x8f requested 'Refund_Policy' classification.<br/>
            [OK] Agent generated automated reply. Hook 'n8n_reply' triggered successfully.
          </div>
        </div>
      </div>
    </div>
  );
}
