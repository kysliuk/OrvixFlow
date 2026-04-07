"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { ShieldAlert, Send, Activity, Users, RotateCw, CheckCircle2, Search, Database } from "lucide-react";

interface Tenant {
  id: string;
  name: string;
  plan: string;
}

interface DebugResult {
  id: string;
  title: string;
  similarityScore: number;
  documentId: string | null;
  chunkType: string;
  preview: string;
}

export default function AdminTestPage() {
  const { data: session } = useSession();
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [selectedTenant, setSelectedTenant] = useState<string>("");
  const [emailBody, setEmailBody] = useState("");
  const [result, setResult] = useState<string | null>(null);
  const [isRunning, setIsRunning] = useState(false);

  // KB Debug state
  const [kbQuery, setKbQuery] = useState("");
  const [kbResults, setKbResults] = useState<DebugResult[]>([]);
  const [kbLoading, setKbLoading] = useState(false);
  const [kbError, setKbError] = useState<string | null>(null);

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

  const handleKbSearch = async () => {
    if (!kbQuery || !selectedTenant) return;
    setKbLoading(true);
    setKbError(null);
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/debug-search?q=${encodeURIComponent(kbQuery)}&maxResults=10`,
        {
          headers: {
            "Authorization": `Bearer ${apiToken}`,
            "X-Impersonate-Tenant": selectedTenant,
          },
        }
      );
      const data = await res.json();
      if (res.ok) {
        setKbResults(data.results || []);
      } else {
        setKbError(data.detail || `Error: ${res.status}`);
      }
    } catch (e: any) {
      setKbError(e.message);
    } finally {
      setKbLoading(false);
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

        {/* Knowledge Base Debug Section */}
        <div className="bg-black/40 border border-primary/20 rounded-xl p-6">
          <h2 className="text-sm font-bold uppercase tracking-widest text-primary/80 flex items-center gap-2 mb-4">
            <Database className="w-4 h-4" /> Knowledge Base Retrieval Debug
          </h2>
          
          <div className="flex flex-col gap-4">
            <div className="flex gap-2">
              <input
                type="text"
                placeholder="Enter search query..."
                value={kbQuery}
                onChange={(e) => setKbQuery(e.target.value)}
                onKeyDown={(e) => e.key === "Enter" && handleKbSearch()}
                className="flex-1 bg-black/60 border border-primary/20 text-white rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/60"
              />
              <button
                onClick={handleKbSearch}
                disabled={kbLoading || !kbQuery || !selectedTenant}
                className="flex items-center gap-2 bg-primary hover:bg-primary/80 disabled:opacity-40 text-white font-semibold rounded-lg px-4 py-2 text-sm"
              >
                {kbLoading ? <RotateCw className="w-4 h-4 animate-spin" /> : <Search className="w-4 h-4" />}
                Search
              </button>
            </div>

            {kbError && (
              <div className="text-danger text-sm font-mono p-2 bg-danger/10 rounded">
                {kbError}
              </div>
            )}

            {kbResults.length > 0 && (
              <div className="space-y-3">
                <p className="text-xs text-white/50 font-mono">
                  Found {kbResults.length} results
                </p>
                {kbResults.map((item, idx) => (
                  <div key={idx} className="bg-[#050202] border border-primary/15 rounded-lg p-4">
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-white font-medium text-sm truncate flex-1" title={item.title}>
                        {item.title || "(No title)"}
                      </span>
                      <span className={`text-xs font-mono px-2 py-0.5 rounded ${
                        item.similarityScore > 0.7 ? "bg-success/20 text-success" :
                        item.similarityScore > 0.5 ? "bg-warning/20 text-warning" :
                        "bg-danger/20 text-danger"
                      }`}>
                        Score: {item.similarityScore.toFixed(2)}
                      </span>
                    </div>
                    <p className="text-xs text-white/60 font-mono whitespace-pre-wrap">
                      {item.preview}
                    </p>
                    <div className="text-[10px] text-white/30 font-mono mt-2">
                      ID: {item.id.slice(0, 8)}... | DocID: {item.documentId?.slice(0, 8) || "null"} | Type: {item.chunkType}
                    </div>
                  </div>
                ))}
              </div>
            )}

            {!kbLoading && !kbError && kbResults.length === 0 && kbQuery && (
              <p className="text-white/30 text-sm font-mono text-center py-4">
                No results found for this query
              </p>
            )}
          </div>
        </div>
    </div>
  );
}
