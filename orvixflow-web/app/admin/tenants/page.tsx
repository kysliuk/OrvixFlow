"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { ShieldAlert, Zap, Building, Crosshair, X, Play } from "lucide-react";

interface Tenant {
  id: string;
  name: string;
  plan: string;
  subscriptionStatus: string;
  createdAt: string;
  userCount: number;
}

export default function TenantDirectoryPage() {
  const { data: session } = useSession();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [impersonating, setImpersonating] = useState<string | null>(null);

  useEffect(() => {
    // Load local storage impersonation state
    const saved = localStorage.getItem("impersonateTenantId");
    if (saved) setImpersonating(saved);

    if ((session as any)?.apiToken) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/tenants`, {
        headers: { "Authorization": `Bearer ${(session as any).apiToken}` }
      })
      .then(async res => {
        const text = await res.text();
        if (!text) throw new Error(`Empty response: ${res.status}`);
        return JSON.parse(text);
      })
      .then(data => setTenants(data))
      .catch(console.error);
    }
  }, [session]);

  const handleImpersonate = (tenantId: string) => {
    localStorage.setItem("impersonateTenantId", tenantId);
    setImpersonating(tenantId);
  };

  const handleDropImpersonate = () => {
    localStorage.removeItem("impersonateTenantId");
    setImpersonating(null);
  };

  return (
    <div className="flex flex-col gap-6 max-w-6xl h-full">
      <div>
        <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
          <Building className="w-6 h-6 text-danger" /> Tenant Directory
        </h1>
        <p className="text-sm text-white/60 font-mono">
          MANAGE_SUBSCRIPTIONS // IMPERSONATE_CONTEXT
        </p>
      </div>

      {impersonating && (
        <div className="bg-danger border border-danger/50 text-white p-4 rounded-xl shadow-[0_0_20px_rgba(244,63,94,0.3)] flex justify-between items-center animate-in slide-in-from-top-4">
          <div className="flex items-center gap-3">
            <ShieldAlert className="w-6 h-6" />
            <div>
              <h2 className="font-bold tracking-tight">ACTIVE IMPERSONATION MODE</h2>
              <p className="text-xs text-white/80 font-mono">Target Tenant ID: {impersonating}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <a href="/inbox" target="_blank" className="px-4 py-2 bg-white/10 hover:bg-white/20 font-medium text-sm rounded-lg transition-colors flex items-center gap-2">
              <Play className="w-4 h-4" /> Open Standard UI
            </a>
            <button onClick={handleDropImpersonate} className="px-4 py-2 bg-black/40 hover:bg-black/60 text-white font-medium text-sm rounded-lg transition-colors flex items-center gap-2">
              <X className="w-4 h-4" /> Drop Context
            </button>
          </div>
        </div>
      )}

      <div className="bg-black/40 border border-danger/20 rounded-xl overflow-hidden shadow-lg flex flex-col flex-1 min-h-[400px]">
        <div className="p-3 border-b border-danger/20 bg-danger/5 flex items-center justify-between">
          <div className="text-xs font-bold text-white/70 uppercase tracking-widest pl-2">Platform Workspaces</div>
        </div>

        <div className="flex-1 overflow-auto custom-scrollbar">
          <table className="w-full text-left text-sm whitespace-nowrap">
            <thead className="bg-[#050202] text-white/50 text-[10px] uppercase tracking-wider sticky top-0 z-10 border-b border-danger/20">
              <tr>
                <th className="px-5 py-3 font-medium">Tenant ID</th>
                <th className="px-5 py-3 font-medium">Name</th>
                <th className="px-5 py-3 font-medium">Users</th>
                <th className="px-5 py-3 font-medium">Plan</th>
                <th className="px-5 py-3 font-medium">Status</th>
                <th className="px-5 py-3 font-medium text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-danger/10">
              {tenants.map(t => (
                <tr key={t.id} className="hover:bg-danger/5 transition-colors group">
                  <td className="px-5 py-3 font-mono text-xs text-white/60">{t.id}</td>
                  <td className="px-5 py-3 font-medium text-white/90">{t.name}</td>
                  <td className="px-5 py-3 text-white/70">{t.userCount}</td>
                  <td className="px-5 py-3">
                    <span className={`px-2 py-0.5 rounded-sm text-[10px] uppercase font-bold tracking-widest border ${
                      t.plan !== "Free" ? "bg-primary/20 text-primary border-primary/30" : "bg-white/5 text-white/50 border-white/10"
                    }`}>
                      {t.plan}
                    </span>
                  </td>
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-1.5">
                      <span className={`w-1.5 h-1.5 rounded-full ${t.subscriptionStatus === "Active" ? "bg-success" : "bg-warning"}`} />
                      <span className={`text-xs ${t.subscriptionStatus === "Active" ? "text-success" : "text-warning"}`}>{t.subscriptionStatus}</span>
                    </div>
                  </td>
                  <td className="px-5 py-3 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <a 
                        href={`/admin/companies/${t.id}/inbox`}
                        className="px-3 py-1.5 border border-white/10 rounded-md text-xs font-medium text-white/50 hover:text-white hover:bg-white/10 transition-all"
                      >
                        View Inbox
                      </a>
                      <button 
                        onClick={() => handleImpersonate(t.id)}
                        className={`px-3 py-1.5 border rounded-md text-xs font-medium transition-all flex items-center justify-center gap-1.5 ${
                          impersonating === t.id 
                            ? "bg-danger text-white border-danger shadow-[0_0_10px_rgba(244,63,94,0.3)]" 
                            : "bg-danger/10 text-danger border-danger/30 hover:bg-danger/20"
                        }`}
                      >
                        <Crosshair className="w-3 h-3" /> 
                        {impersonating === t.id ? "Impersonating" : "Assume Context"}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
