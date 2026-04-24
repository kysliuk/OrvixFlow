/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect, useRef } from "react";
import { useSession } from "next-auth/react";
import { Database, FileText, UploadCloud, Search, Trash2, MoreVertical, Plus, Check, RotateCw, AlertCircle, File, X, Upload, Files } from "lucide-react";

interface KnowledgeItem {
  id: string;
  rawContent: string;
  metadata: string;
  createdAt: string;
}

interface DocumentItem {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: string;
  createdAtUtc: string;
  indexedAtUtc: string | null;
  errorMessage: string | null;
}

interface ApiResponse {
  items: KnowledgeItem[];
  total: number;
  limit: number;
  offset: number;
}

interface DocumentsResponse {
  items: DocumentItem[];
  total: number;
  page: number;
  pageSize: number;
}

type TabType = "documents" | "text";

export default function KnowledgeBasePage() {
  const { data: session, status } = useSession();
  const [activeTab, setActiveTab] = useState<TabType>("documents");

  // Text ingestion state
  const [prompt, setPrompt] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const [ingestLog, setIngestLog] = useState<string | null>(null);

  // Text list state
  const [items, setItems] = useState<KnowledgeItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Document state
  const [documents, setDocuments] = useState<DocumentItem[]>([]);
  const [documentsLoading, setDocumentsLoading] = useState(true);
  const [documentsError, setDocumentsError] = useState<string | null>(null);
  const [selectedDocIds, setSelectedDocIds] = useState<Set<string>>(new Set());
  const [docPage, setDocPage] = useState(1);
  const [docTotal, setDocTotal] = useState(0);

  // File upload state
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragActive, setDragActive] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const apiToken = (session as any)?.apiToken;
  const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080";

  useEffect(() => {
    if (status === "loading") return;
    fetchKnowledge();
    fetchDocuments();
  }, [apiToken, docPage, searchQuery, status]);

  const getHeaders = () => {
    const apiToken = (session as any)?.apiToken;
    const imp = localStorage.getItem("impersonateTenantId");
    const headers: Record<string, string> = {
      "Authorization": `Bearer ${apiToken}`
    };
    if (imp) headers["X-Impersonate-Tenant"] = imp;
    return headers;
  };

  const fetchKnowledge = async () => {
    setLoading(true);
    setListError(null);
    
    try {
      if (!apiToken) {
        setListError("Not authenticated");
        return;
      }

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      let url = `${apiBaseUrl}/api/v1/knowledge?limit=100`;
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

  const fetchDocuments = async () => {
    setDocumentsLoading(true);
    setDocumentsError(null);
    
    try {
      if (!apiToken) {
        setDocumentsError("Not authenticated");
        return;
      }

      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      const imp = localStorage.getItem("impersonateTenantId");
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${apiBaseUrl}/api/v1/knowledge/documents?page=${docPage}&pageSize=20`, { headers });

      if (!res.ok) {
        throw new Error(`Failed to fetch: ${res.status}`);
      }

      const data: DocumentsResponse = await res.json();
      setDocuments(data.items || []);
      setDocTotal(data.total);
    } catch (e: any) {
      setDocumentsError(e.message);
    } finally {
      setDocumentsLoading(false);
    }
  };

  const handleIngest = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt) return;
    setIsProcessing(true);
    setIngestLog(null);
    
    if (!apiToken) {
      setIngestLog("CRITICAL ERROR: API Token is missing from the NextAuth session. Your login failed to sync with the backend database.");
      setIsProcessing(false);
      return;
    }

    try {
      const imp = localStorage.getItem("impersonateTenantId");
      const headers: any = {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${apiBaseUrl}/api/agent/ingest`, {
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
      if (!apiToken) return;

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${apiBaseUrl}/api/v1/knowledge/${id}`, {
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

  const handleDeleteDocument = async (id: string) => {
    if (!confirm("Are you sure you want to delete this document and all its chunks?")) return;

    try {
      const headers = getHeaders();

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/documents/${id}`, {
        method: "DELETE",
        headers,
      });

      if (res.ok) {
        setDocuments(documents.filter(doc => doc.id !== id));
        setSelectedDocIds(prev => {
          const next = new Set(prev);
          next.delete(id);
          return next;
        });
      } else {
        alert("Failed to delete document");
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
        if (!apiToken) continue;

        const imp = localStorage.getItem("impersonateTenantId");
        const headers: Record<string, string> = {
          "Authorization": `Bearer ${apiToken}`
        };
        if (imp) headers["X-Impersonate-Tenant"] = imp;

        const res = await fetch(`${apiBaseUrl}/api/v1/knowledge/${id}`, {
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

  const handleFileUpload = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    
    setIsUploading(true);
    setUploadProgress(0);

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      
      try {
        const formData = new FormData();
        formData.append("file", file);

        const imp = localStorage.getItem("impersonateTenantId");
        const headers: Record<string, string> = {
          "Authorization": `Bearer ${apiToken}`
        };
        if (imp) headers["X-Impersonate-Tenant"] = imp;

        const res = await fetch(`${apiBaseUrl}/api/v1/knowledge/upload`, {
          method: "POST",
          headers,
          body: formData,
        });

        if (!res.ok) {
          throw new Error(`Upload failed: ${res.status}`);
        }
        
        setUploadProgress(((i + 1) / files.length) * 100);
      } catch (e: any) {
        alert(`Failed to upload ${file.name}: ${e.message}`);
      }
    }

    setIsUploading(false);
    setUploadProgress(0);
    fetchDocuments();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      handleFileUpload(e.dataTransfer.files);
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

  const toggleDocSelect = (id: string) => {
    setSelectedDocIds(prev => {
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

  const formatSize = (bytes: number) => {
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

  const formatDateUtc = (dateStr: string | null) => {
    if (!dateStr) return "-";
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

  const getStatusBadge = (status: string) => {
    switch (status) {
      case "Indexed":
        return <span className="px-2 py-0.5 rounded-full text-xs bg-success/20 text-success">Indexed</span>;
      case "Processing":
        return <span className="px-2 py-0.5 rounded-full text-xs bg-warning/20 text-warning">Processing</span>;
      case "Failed":
        return <span className="px-2 py-0.5 rounded-full text-xs bg-danger/20 text-danger">Failed</span>;
      default:
        return <span className="px-2 py-0.5 rounded-full text-xs bg-muted/20 text-muted">{status}</span>;
    }
  };

  return (
    <div className="flex flex-col gap-6 max-w-7xl h-full">
      {/* Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Knowledge Base</h1>
          <p className="text-sm text-muted">Manage the underlying memory chunks available to your autonomous agents.</p>
        </div>
        {activeTab === "text" && (
          <button 
            onClick={handleBulkDelete}
            disabled={selectedIds.size === 0}
            className="flex items-center gap-2 px-4 py-2 bg-danger/10 text-danger hover:bg-danger/20 border border-danger/20 rounded-lg text-sm transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Trash2 className="w-4 h-4" /> 
            Delete Selected ({selectedIds.size})
          </button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-1 bg-surface border border-white/5 rounded-lg p-1 w-fit">
        <button
          onClick={() => setActiveTab("documents")}
          className={`flex items-center gap-2 px-4 py-2 rounded-md text-sm transition-all ${
            activeTab === "documents" 
              ? "bg-primary text-white" 
              : "text-muted hover:text-white hover:bg-white/5"
          }`}
        >
          <Files className="w-4 h-4" />
          Documents
        </button>
        <button
          onClick={() => setActiveTab("text")}
          className={`flex items-center gap-2 px-4 py-2 rounded-md text-sm transition-all ${
            activeTab === "text" 
              ? "bg-primary text-white" 
              : "text-muted hover:text-white hover:bg-white/5"
          }`}
        >
          <FileText className="w-4 h-4" />
          Text Snippets
        </button>
      </div>

      {activeTab === "documents" ? (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Upload Zone */}
          <div className="lg:col-span-1 bg-surface border border-white/5 rounded-xl p-6 shadow-lg flex flex-col h-fit">
            <div className="absolute top-0 right-0 w-[200px] h-[200px] bg-success/5 blur-[80px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
            
            <h2 className="text-lg font-semibold mb-1 relative z-10 flex items-center gap-2">
              <UploadCloud className="w-5 h-5 text-primary" />
              Upload Files
            </h2>
            <p className="text-sm text-muted mb-4 relative z-10">Upload PDFs, images, or documents to be indexed.</p>
            
            <div 
              className={`relative z-10 border-2 border-dashed rounded-lg p-6 text-center transition-all cursor-pointer ${
                dragActive 
                  ? "border-primary bg-primary/10" 
                  : "border-white/10 hover:border-white/20"
              }`}
              onDragOver={(e) => { e.preventDefault(); setDragActive(true); }}
              onDragLeave={() => setDragActive(false)}
              onDrop={handleDrop}
              onClick={() => fileInputRef.current?.click()}
            >
              <input 
                ref={fileInputRef}
                type="file"
                multiple
                accept=".pdf,.png,.jpg,.jpeg,.txt,.docx"
                className="hidden"
                onChange={(e) => handleFileUpload(e.target.files)}
              />
              
              {isUploading ? (
                <div className="flex flex-col items-center gap-2">
                  <div className="w-10 h-10 rounded-full border-2 border-primary border-t-transparent animate-spin" />
                  <p className="text-sm text-white">Uploading... {Math.round(uploadProgress)}%</p>
                </div>
              ) : (
                <>
                  <UploadCloud className="w-10 h-10 mx-auto text-muted mb-2" />
                  <p className="text-sm text-white mb-1">Drag & drop files here</p>
                  <p className="text-xs text-muted">or click to browse</p>
                  <p className="text-xs text-muted mt-2">PDF, PNG, JPG, TXT, DOCX</p>
                </>
              )}
            </div>
          </div>

          {/* Documents Table */}
          <div className="lg:col-span-2 bg-surface border border-white/5 rounded-xl overflow-hidden shadow-lg flex flex-col h-[500px]">
            <div className="p-3 border-b border-white/5 bg-surface-hover flex items-center justify-between">
              <div className="flex items-center gap-2">
                <button 
                  onClick={fetchDocuments}
                  className="p-1.5 text-muted hover:text-white hover:bg-white/10 rounded-md transition-colors"
                >
                  <RotateCw className="w-4 h-4" />
                </button>
                <span className="text-sm text-muted">{docTotal} documents</span>
              </div>
              {selectedDocIds.size > 0 && (
                <button 
                  onClick={async () => {
                    for (const id of selectedDocIds) {
                      await handleDeleteDocument(id);
                    }
                  }}
                  className="flex items-center gap-2 px-3 py-1.5 bg-danger/10 text-danger hover:bg-danger/20 rounded-md text-sm"
                >
                  <Trash2 className="w-4 h-4" />
                  Delete ({selectedDocIds.size})
                </button>
              )}
            </div>

            {documentsLoading && (
              <div className="flex-1 flex items-center justify-center">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
              </div>
            )}
            
            {documentsError && (
              <div className="flex-1 flex items-center justify-center text-danger">
                <AlertCircle className="w-5 h-5 mr-2" />
                {documentsError}
              </div>
            )}

            {!documentsLoading && !documentsError && documents.length === 0 && (
              <div className="flex-1 flex flex-col items-center justify-center text-muted">
                <Files className="w-12 h-12 mb-3 opacity-50" />
                <p className="text-sm">No documents uploaded</p>
                <p className="text-xs mt-1">Upload files to get started</p>
              </div>
            )}

            {!documentsLoading && !documentsError && documents.length > 0 && (
              <div className="flex-1 overflow-auto custom-scrollbar">
                <table className="w-full text-left text-sm whitespace-nowrap">
                  <thead className="bg-[#0a0710] text-muted text-xs uppercase tracking-wider sticky top-0 z-10">
                    <tr>
                      <th className="px-4 py-3 border-b border-white/5 w-10">
                        <input 
                          type="checkbox" 
                          className="rounded-sm bg-white/5 border-white/10 text-primary focus:ring-primary/50 cursor-pointer"
                          checked={selectedDocIds.size === documents.length}
                          onChange={() => {
                            if (selectedDocIds.size === documents.length) {
                              setSelectedDocIds(new Set());
                            } else {
                              setSelectedDocIds(new Set(documents.map(d => d.id)));
                            }
                          }}
                        />
                      </th>
                      <th className="px-4 py-3 font-medium border-b border-white/5">File Name</th>
                      <th className="px-4 py-3 font-medium border-b border-white/5">Size</th>
                      <th className="px-4 py-3 font-medium border-b border-white/5">Status</th>
                      <th className="px-4 py-3 font-medium border-b border-white/5">Uploaded</th>
                      <th className="px-4 py-3 border-b border-white/5 w-10"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {documents.map((doc) => (
                      <tr key={doc.id} className="hover:bg-white/5 transition-colors group">
                        <td className="px-4 py-2.5">
                          <input 
                            type="checkbox" 
                            className="rounded-sm bg-background border-white/10 text-primary focus:ring-primary/50 cursor-pointer"
                            checked={selectedDocIds.has(doc.id)}
                            onChange={() => toggleDocSelect(doc.id)}
                          />
                        </td>
                        <td className="px-4 py-2.5">
                          <div className="flex items-center gap-3">
                            <File className="w-4 h-4 text-muted shrink-0" />
                            <span className="font-medium text-white/90 truncate max-w-[200px]" title={doc.fileName}>
                              {doc.fileName}
                            </span>
                          </div>
                        </td>
                        <td className="px-4 py-2.5 text-xs text-muted">{formatSize(doc.fileSizeBytes)}</td>
                        <td className="px-4 py-2.5">{getStatusBadge(doc.status)}</td>
                        <td className="px-4 py-2.5 text-xs text-muted">{formatDateUtc(doc.createdAtUtc)}</td>
                        <td className="px-4 py-2.5 text-right">
                          <button 
                            onClick={() => handleDeleteDocument(doc.id)}
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

            {/* Pagination */}
            {!documentsLoading && docTotal > 20 && (
              <div className="p-3 border-t border-white/5 flex items-center justify-center gap-2">
                <button
                  onClick={() => setDocPage(p => Math.max(1, p - 1))}
                  disabled={docPage === 1}
                  className="px-3 py-1 text-sm text-muted hover:text-white disabled:opacity-50"
                >
                  Previous
                </button>
                <span className="text-sm text-muted">Page {docPage}</span>
                <button
                  onClick={() => setDocPage(p => p + 1)}
                  disabled={docPage * 20 >= docTotal}
                  className="px-3 py-1 text-sm text-muted hover:text-white disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            )}
          </div>
        </div>
      ) : (
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
                        <td className="px-4 py-2.5 text-xs text-muted">{formatSize(item.rawContent.length)}</td>
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
            <p className="text-sm text-muted mb-6 relative z-10">Paste raw text directly into the agent&apos;s vectorized memory store.</p>
            
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
      )}
    </div>
  );
}
