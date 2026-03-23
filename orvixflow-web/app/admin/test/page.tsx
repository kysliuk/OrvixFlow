"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { ShieldAlert, Send, Activity, Users, RotateCw, CheckCircle2 } from "lucide-react";

interface Tenant {
  id: string;
  name: string;
  plan: string;
}

export default function AdminTestPage() {
  const { data: session } = useSession();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [selectedTenant, setSelectedTenant] = useState<string>("");
  const [emailBody, setEmailBody] = useState("");
  const [result, setResult] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken) {
      fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/tenants`, {
        headers: { "Authorization": `Bearer ${apiToken}` }
      })
      .then(async res => {
        const text = await res.text();
        if (!text) throw new Error(`Empty response: ${res.status}`);
        return JSON.parse(text);
      })
      .then(data => {
        setTenants(data);
        if (data.length > 0) setSelectedTenant(data[0].id);
      })
      .catch(console.error);
    }
  }, [apiToken]);

  const handleRun = async () => {
    if (!emailBody || !selectedTenant) return;
    setIsRunning(true);
    setResult(null);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/inbox/process`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${apiToken}`,
          "X-Impersonate-Tenant": selectedTenant,
        },
        body: JSON.stringify({ prompt: emailBody }),
      });
      const text = await res.text();
      try {
        const json = JSON.parse(text);
        setResult(JSON.stringify(json, null, 2));
      } catch {
        setResult(text || `HTTP ${res.status} ${res.statusText}`);
      }
    } catch (e: any) {
      setResult(`Error: ${e.message}`);
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 max-w-5xl">
      <div>
        <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
          <ShieldAlert className="w-6 h-6 text-danger" /> Inbox Guardian Simulator
        </h1>
        <p className="text-sm text-white/60 font-mono">
          IMPERSONATE_TENANT // TRIGGER_AI_TRIAGE // INSPECT_RESULT
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Config Panel */}
        <div className="flex flex-col gap-4 bg-black/40 border border-danger/20 rounded-xl p-6">
          <h2 className="text-sm font-bold uppercase tracking-widest text-danger/80 flex items-center gap-2">
            <Users className="w-4 h-4" /> Target Workspace
          </h2>

          <div className="flex flex-col gap-2">
            <label className="text-xs text-white/50 uppercase tracking-wider font-medium">Select Tenant</label>
            <select
              value={selectedTenant}
              onChange={e => setSelectedTenant(e.target.value)}
              className="w-full bg-black/60 border border-danger/20 text-white rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:border-danger/60 transition-colors"
            >
              {tenants.map(t => (
                <option key={t.id} value={t.id}>{t.name} ({t.plan})</option>
              ))}
            </select>
            {selectedTenant && (
              <p className="text-[10px] font-mono text-white/30 mt-1 truncate">ID: {selectedTenant}</p>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-xs text-white/50 uppercase tracking-wider font-medium">Simulated Incoming Email</label>
            <textarea
              className="w-full h-48 bg-black/60 border border-danger/20 rounded-lg p-3 text-sm text-white focus:outline-none focus:border-danger/60 transition-colors font-mono resize-none"
              placeholder={`Hi,\n\nI'd like to request a refund for my recent purchase.\n\nBest regards`}
              value={emailBody}
              onChange={e => setEmailBody(e.target.value)}
            />
          </div>

          <button
            onClick={handleRun}
            disabled={isRunning || !emailBody || !selectedTenant}
            className="flex items-center justify-center gap-2 bg-danger hover:bg-danger/80 disabled:opacity-40 text-white font-semibold rounded-lg px-6 py-3 text-sm shadow-[0_4px_20px_rgba(244,63,94,0.3)] transition-all"
          >
            {isRunning
              ? <><RotateCw className="w-4 h-4 animate-spin" /> Agent is thinking...</>
              : <><Send className="w-4 h-4" /> Trigger Autonomous Triage</>
            }
          </button>
        </div>

        {/* Right: Result Panel */}
        <div className="flex flex-col gap-4 bg-black/40 border border-danger/20 rounded-xl p-6 min-h-[300px]">
          <h2 className="text-sm font-bold uppercase tracking-widest text-danger/80 flex items-center gap-2">
            <Activity className="w-4 h-4" /> Agent Orchestration Result
          </h2>

          {!result && !isRunning && (
            <div className="flex-1 flex items-center justify-center text-white/20 text-sm font-mono text-center">
              Run a simulation to inspect<br/>the AI agent's decision path
            </div>
          )}

          {isRunning && (
            <div className="flex-1 flex items-center justify-center">
              <div className="flex flex-col items-center gap-3 text-danger/70">
                <RotateCw className="w-6 h-6 animate-spin" />
                <p className="text-xs font-mono">KERNEL PROCESSING...</p>
              </div>
            </div>
          )}

          {result && !isRunning && (
            <div className="flex flex-col gap-3 animate-in fade-in duration-300">
              <div className="flex items-center gap-2 text-xs text-success font-medium">
                <CheckCircle2 className="w-4 h-4" /> Response received
              </div>
              <div className="bg-[#050202] rounded-lg border border-danger/15 p-4 overflow-auto max-h-96">
                <pre className="text-xs text-white/80 font-mono whitespace-pre-wrap leading-relaxed">
                  {result}
                </pre>
              </div>
              <button
                onClick={() => { setResult(null); setEmailBody(""); }}
                className="self-start text-xs text-danger/60 hover:text-danger transition-colors font-mono"
              >
                CLEAR // NEW TEST
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
