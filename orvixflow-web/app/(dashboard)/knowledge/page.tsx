"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { Database, FileText, UploadCloud, Search, Trash2, MoreVertical, Plus, Check, RotateCw, AlertCircle } from "lucide-react";

interface KnowledgeItem {
  id: string;
  rawContent: string;
  metadata: string;
  createdAt: string;
}

interface ApiResponse {
  items: KnowledgeItem[];
  total: number;
  limit: number;
  offset: number;
}

export default function KnowledgeBasePage() {
  const { data: session } = useSession();
  const [prompt, setPrompt] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [ingestLog, setIngestLog] = useState<string | null>(null);

  // List state
  const [items, setItems] = useState<KnowledgeItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    fetchKnowledge();
  }, [searchQuery]);

  const fetchKnowledge = async () => {
    setLoading(true);
    setListError(null);
    
    try {
      const apiToken = (session as any)?.apiToken;
      if (!apiToken) {
        setListError("Not authenticated");
        return;
      }

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      let url = `${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge?limit=100`;
      if (searchQuery) {
        url += `&search=${encodeURIComponent(searchQuery)}`;
      }

      const res = await fetch(url, { headers });

      if (!res.ok) {
        throw new Error(`Failed to fetch: ${res.status}`);
      }

      const data: ApiResponse = await res.json();
      setItems(data.items || []);
    } catch (e: any) {
      setListError(e.message);
    } finally {
      setLoading(false);
    }
  };

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
        fetchKnowledge();
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

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this knowledge entry?")) return;

    try {
      const apiToken = (session as any)?.apiToken;
      if (!apiToken) return;

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/${id}`, {
        method: "DELETE",
        headers,
      });

      if (res.ok) {
        setItems(items.filter(item => item.id !== id));
        setSelectedIds(prev => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      } else {
        alert("Failed to delete entry");
      }
    } catch (e: any) {
      alert(`Error: ${e.message}`);
    }
  };

  const handleBulkDelete = async () => {
    if (selectedIds.size === 0) return;
    if (!confirm(`Are you sure you want to delete ${selectedIds.size} entries?`)) return;

    const idsToDelete = Array.from(selectedIds);
    let successCount = 0;
    let errorCount = 0;

    for (const id of idsToDelete) {
      try {
        const apiToken = (session as any)?.apiToken;
        if (!apiToken) continue;

        const imp = localStorage.getItem("impersonateTenantId");
        const headers: Record<string, string> = {
          "Authorization": `Bearer ${apiToken}`
        };
        if (imp) headers["X-Impersonate-Tenant"] = imp;

        const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/${id}`, {
          method: "DELETE",
          headers,
        });

        if (res.ok) {
          successCount++;
        } else {
          errorCount++;
        }
      } catch {
        errorCount++;
      }
    }

    if (successCount > 0) {
      setItems(items.filter(item => !selectedIds.has(item.id)));
      setSelectedIds(new Set());
    }
    
    if (errorCount > 0) {
      alert(`Deleted ${successCount} entries. ${errorCount} failed.`);
    }
  };

  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const toggleSelectAll = () => {
    if (selectedIds.size === items.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(items.map(item => item.id)));
    }
  };

  const formatSize = (content: string) => {
    const bytes = new Blob([content]).size;
    if (bytes < 1024) return `${bytes}B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
  };

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleDateString("en-US", { 
      month: "short", 
      day: "numeric", 
      year: "numeric" 
    });
  };

  const extractTitle = (content: string, id: string) => {
    const firstLine = content.split("\n")[0].trim();
    if (firstLine.length > 60) {
      return firstLine.substring(0, 60) + "...";
    }
    return firstLine || `Entry ${id.substring(0, 8)}...`;
  };

  return (
    <div className="flex flex-col gap-6 max-w-7xl h-full">
      
      {/* Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Knowledge Base</h1>
          <p className="text-sm text-muted">Manage the underlying memory chunks available to your autonomous agents.</p>
        </div>
        <button 
          onClick={handleBulkDelete}
          disabled={selectedIds.size === 0}
          className="flex items-center gap-2 px-4 py-2 bg-danger/10 text-danger hover:bg-danger/20 border border-danger/20 rounded-lg text-sm transition-all disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Trash2 className="w-4 h-4" /> 
          Delete Selected ({selectedIds.size})
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Table View */}
        <div className="lg:col-span-2 bg-surface border border-white/5 rounded-xl overflow-hidden shadow-lg flex flex-col h-[500px]">
          {/* Table Toolbar */}
          <div className="p-3 border-b border-white/5 bg-surface-hover flex items-center justify-between">
            <div className="relative w-64">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
              <input 
                type="text" 
                placeholder="Search documents..." 
                className="w-full bg-background border border-white/10 rounded-md py-1.5 pl-9 pr-4 text-xs text-white focus:outline-none focus:border-primary/50"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <div className="flex items-center gap-2">
              <button 
                onClick={fetchKnowledge}
                className="p-1.5 text-muted hover:text-white hover:bg-white/10 rounded-md transition-colors"
              >
                <RotateCw className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Loading/Error States */}
          {loading && (
            <div className="flex-1 flex items-center justify-center">
              <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
            </div>
          )}
          
          {listError && (
            <div className="flex-1 flex items-center justify-center text-danger">
              <AlertCircle className="w-5 h-5 mr-2" />
              {listError}
            </div>
          )}

          {!loading && !listError && items.length === 0 && (
            <div className="flex-1 flex flex-col items-center justify-center text-muted">
              <Database className="w-12 h-12 mb-3 opacity-50" />
              <p className="text-sm">No knowledge entries found</p>
              <p className="text-xs mt-1">Use Direct Ingestion to add content</p>
            </div>
          )}

          {/* Actual Table */}
          {!loading && !listError && items.length > 0 && (
            <div className="flex-1 overflow-auto custom-scrollbar">
              <table className="w-full text-left text-sm whitespace-nowrap">
                <thead className="bg-[#0a0710] text-muted text-xs uppercase tracking-wider sticky top-0 z-10">
                  <tr>
                    <th className="px-4 py-3 border-b border-white/5 w-10">
                      <input 
                        type="checkbox" 
                        className="rounded-sm bg-white/5 border-white/10 text-primary focus:ring-primary/50 cursor-pointer"
                        checked={selectedIds.size === items.length}
                        onChange={toggleSelectAll}
                      />
                    </th>
                    <th className="px-4 py-3 font-medium border-b border-white/5">Content Preview</th>
                    <th className="px-4 py-3 font-medium border-b border-white/5">Size</th>
                    <th className="px-4 py-3 font-medium border-b border-white/5">Date Added</th>
                    <th className="px-4 py-3 border-b border-white/5 w-10"></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-white/5">
                  {items.map((item) => (
                    <tr key={item.id} className="hover:bg-white/5 transition-colors group">
                      <td className="px-4 py-2.5">
                        <input 
                          type="checkbox" 
                          className="rounded-sm bg-background border-white/10 text-primary focus:ring-primary/50 cursor-pointer"
                          checked={selectedIds.has(item.id)}
                          onChange={() => toggleSelect(item.id)}
                        />
                      </td>
                      <td className="px-4 py-2.5">
                        <div className="flex items-center gap-3">
                          <FileText className="w-4 h-4 text-muted shrink-0" />
                          <span className="font-medium text-white/90 truncate max-w-[300px]" title={item.rawContent}>
                            {extractTitle(item.rawContent, item.id)}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-muted">{formatSize(item.rawContent)}</td>
                      <td className="px-4 py-2.5 text-xs text-muted">{formatDate(item.createdAt)}</td>
                      <td className="px-4 py-2.5 text-right">
                        <button 
                          onClick={() => handleDelete(item.id)}
                          className="p-1 text-muted hover:text-danger transition-colors opacity-0 group-hover:opacity-100"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
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
