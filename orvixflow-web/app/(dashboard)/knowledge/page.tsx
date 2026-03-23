"use client";

import { useState } from "react";
import { useSession } from "next-auth/react";
import { Database, FileText, UploadCloud, Search, Trash2, MoreVertical, Plus, Check } from "lucide-react";

export default function KnowledgeBasePage() {
  const { data: session } = useSession();
  const [prompt, setPrompt] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [ingestLog, setIngestLog] = useState<string | null>(null);

  const handleIngest = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt) return;
    setIsProcessing(true);
    setIngestLog(null);
    
    if (!(session as any)?.apiToken) {
      setIngestLog("CRITICAL ERROR: API Token is missing from the NextAuth session. Your login failed to sync with the backend database.");
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

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/agent/ingest`, {
        method: "POST",
        headers,
        body: JSON.stringify({ prompt }),
      });
      if (res.ok) {
        setIngestLog("Successfully ingrained into agent memory!");
        setPrompt("");
      } else {
        const text = await res.text();
        try {
          const data = JSON.parse(text);
          setIngestLog(`Error: ${data.detail || data.title || "Ingestion failed"}`);
        } catch {
          setIngestLog(`Error: ${res.statusText || "Ingestion failed"} (${res.status})`);
        }
      }
    } catch (e: any) {
      setIngestLog(`Error: ${e.message}`);
    } finally {
      setIsProcessing(false);
    }
  };

  const mockupDocs = [
    { id: 1, title: "Q3 Refund Policy Updates 2026", status: "Active", size: "14KB", added: "Oct 12, 2026", type: "text/markdown" },
    { id: 2, title: "SLA Tier Constraints - Enterprise", status: "Active", size: "8KB", added: "Oct 10, 2026", type: "text/markdown" },
    { id: 3, title: "Customer Support Handling Guide", status: "Processing", size: "142KB", added: "Just now", type: "application/pdf" },
    { id: 4, title: "Stripe Dispute Escalation Path", status: "Active", size: "3KB", added: "Sep 28, 2026", type: "text/plain" },
    { id: 5, title: "OrvixFlow Agent System Prompt Base", status: "Active", size: "2KB", added: "Sep 15, 2026", type: "text/markdown" },
  ];

  return (
    <div className="flex flex-col gap-6 max-w-7xl h-full">
      
      {/* Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Knowledge Base</h1>
          <p className="text-sm text-muted">Manage the underlying memory chunks available to your autonomous agents.</p>
        </div>
        <button className="flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all">
          <UploadCloud className="w-4 h-4" /> Upload Document
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Table View (High Density) */}
        <div className="lg:col-span-2 bg-surface border border-white/5 rounded-xl overflow-hidden shadow-lg flex flex-col h-[500px]">
          {/* Table Toolbar */}
          <div className="p-3 border-b border-white/5 bg-surface-hover flex items-center justify-between">
            <div className="relative w-64">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
              <input 
                type="text" 
                placeholder="Search documents..." 
                className="w-full bg-background border border-white/10 rounded-md py-1.5 pl-9 pr-4 text-xs text-white focus:outline-none focus:border-primary/50"
              />
            </div>
            <div className="flex items-center gap-2">
              <button className="p-1.5 text-muted hover:text-danger hover:bg-danger/10 rounded-md transition-colors">
                <Trash2 className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Actual Table */}
          <div className="flex-1 overflow-auto custom-scrollbar">
            <table className="w-full text-left text-sm whitespace-nowrap">
              <thead className="bg-[#0a0710] text-muted text-xs uppercase tracking-wider sticky top-0 z-10">
                <tr>
                  <th className="px-4 py-3 border-b border-white/5 w-10">
                    <input type="checkbox" className="rounded-sm bg-white/5 border-white/10 text-primary focus:ring-primary/50 cursor-pointer" />
                  </th>
                  <th className="px-4 py-3 font-medium border-b border-white/5">Document Title</th>
                  <th className="px-4 py-3 font-medium border-b border-white/5">Status</th>
                  <th className="px-4 py-3 font-medium border-b border-white/5">Size</th>
                  <th className="px-4 py-3 font-medium border-b border-white/5">Date Added</th>
                  <th className="px-4 py-3 border-b border-white/5 w-10"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {mockupDocs.map((doc) => (
                  <tr key={doc.id} className="hover:bg-white/5 transition-colors group">
                    <td className="px-4 py-2.5">
                      <input type="checkbox" className="rounded-sm bg-background border-white/10 text-primary focus:ring-primary/50 cursor-pointer opacity-0 group-hover:opacity-100 transition-opacity" />
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center gap-3">
                        <FileText className="w-4 h-4 text-muted shrink-0" />
                        <span className="font-medium text-white/90 truncate max-w-[200px]">{doc.title}</span>
                      </div>
                    </td>
                    <td className="px-4 py-2.5">
                      <div className="flex items-center gap-1.5">
                        <span className={`w-1.5 h-1.5 rounded-full ${doc.status === "Active" ? "bg-success" : "bg-warning animate-pulse"}`} />
                        <span className={`text-xs ${doc.status === "Active" ? "text-success" : "text-warning"}`}>{doc.status}</span>
                      </div>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-muted">{doc.size}</td>
                    <td className="px-4 py-2.5 text-xs text-muted">{doc.added}</td>
                    <td className="px-4 py-2.5 text-right">
                      <button className="p-1 text-muted hover:text-white transition-colors">
                        <MoreVertical className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Quick Ingest Panel */}
        <div className="lg:col-span-1 bg-surface border border-white/5 rounded-xl p-6 shadow-lg flex flex-col h-fit relative overflow-hidden">
          <div className="absolute top-0 right-0 w-[200px] h-[200px] bg-success/5 blur-[80px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
          
          <h2 className="text-lg font-semibold mb-1 relative z-10 flex items-center gap-2">
            <Database className="w-5 h-5 text-primary" />
            Direct Ingestion
          </h2>
          <p className="text-sm text-muted mb-6 relative z-10">Paste raw text directly into the agent's vectorized memory store.</p>
          
          <form onSubmit={handleIngest} className="flex flex-col gap-4 relative z-10">
            <textarea
              className="w-full h-40 bg-background border border-white/10 rounded-lg p-3 text-sm text-white focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-mono resize-none"
              placeholder="e.g., The CEO of Acme Corp is Jane Doe. She prefers direct communication via Slack..."
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              required
            />
            
            <button 
              type="submit" 
              disabled={isProcessing}
              className="flex items-center justify-center gap-2 w-full bg-white/5 border border-white/10 hover:bg-white/10 hover:border-primary/30 text-white font-medium rounded-lg px-4 py-2.5 text-sm transition-all disabled:opacity-50"
            >
              {isProcessing ? "Vectorizing..." : <><Plus className="w-4 h-4" /> Ingest Text</>}
            </button>
          </form>

          {ingestLog && (
            <div className={`mt-4 p-3 rounded-lg text-sm border flex items-center gap-2 ${ingestLog.includes("Error") ? "bg-danger/10 text-danger border-danger/20" : "bg-success/10 text-success border-success/20"}`}>
              {ingestLog.includes("Error") ? <Trash2 className="w-4 h-4 shrink-0" /> : <Check className="w-4 h-4 shrink-0" />}
              {ingestLog}
            </div>
          )}
        </div>

      </div>
    </div>
  );
}
