"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { ModuleGate } from "@/components/module-gate";
import { AlertTriangle, CheckCircle2, Clock, XCircle, RotateCw, ChevronRight, Edit, Send } from "lucide-react";

interface PendingAction {
  id: string;
  inboxEventId: string;
  evaluatedCategory: string;
  confidenceScore: number;
  draftResponse: string;
  policyReason: string;
  status: string;
  expiresAtUtc: string;
}

interface ApiResponse {
  items: PendingAction[];
  total: number;
  limit: number;
  offset: number;
}

export default function PendingActionsPage() {
  const { data: session } = useSession();
  const [actions, setActions] = useState<PendingAction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedAction, setSelectedAction] = useState<PendingAction | null>(null);
  const [modifiedResponse, setModifiedResponse] = useState("");
  const [isResolving, setIsResolving] = useState(false);
  const [resolveMessage, setResolveMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    fetchPendingActions();
  }, []);

  const fetchPendingActions = async () => {
    setLoading(true);
    setError(null);
    
    try {
      const apiToken = (session as any)?.apiToken;
      if (!apiToken) {
        setError("Not authenticated");
        return;
      }

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/actions/pending`, {
        headers
      });

      if (!res.ok) {
        throw new Error(`Failed to fetch: ${res.status}`);
      }

      const data: ApiResponse = await res.json();
      setActions(data.items || []);
      
      if (data.items?.length > 0 && !selectedAction) {
        handleSelectAction(data.items[0]);
      }
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const handleSelectAction = (action: PendingAction) => {
    setSelectedAction(action);
    setModifiedResponse(action.draftResponse);
    setResolveMessage(null);
  };

  const handleResolve = async (approved: boolean) => {
    if (!selectedAction) return;
    
    setIsResolving(true);
    setResolveMessage(null);
    
    try {
      const apiToken = (session as any)?.apiToken;
      if (!apiToken) {
        setResolveMessage({ type: 'error', text: "Not authenticated" });
        return;
      }

      const imp = localStorage.getItem("impersonateTenantId");
      const headers: Record<string, string> = {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${apiToken}`
      };
      if (imp) headers["X-Impersonate-Tenant"] = imp;

      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/actions/${selectedAction.id}/resolve`, {
        method: "POST",
        headers,
        body: JSON.stringify({
          approved,
          modifiedResponse: modifiedResponse !== selectedAction.draftResponse ? modifiedResponse : null,
          rowVersion: 0
        }),
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.error || "Failed to resolve action");
      }

      setResolveMessage({
        type: 'success',
        text: approved ? "Email approved and will be sent" : "Email rejected"
      });

      setTimeout(() => {
        setSelectedAction(null);
        fetchPendingActions();
      }, 1500);
    } catch (e: any) {
      setResolveMessage({ type: 'error', text: e.message });
    } finally {
      setIsResolving(false);
    }
  };

  const getTimeRemaining = (expiresAt: string) => {
    const expires = new Date(expiresAt);
    const now = new Date();
    const diff = expires.getTime() - now.getTime();
    
    if (diff <= 0) return "Expired";
    
    const hours = Math.floor(diff / (1000 * 60 * 60));
    const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    
    if (hours > 24) return `${Math.floor(hours / 24)}d remaining`;
    if (hours > 0) return `${hours}h ${minutes}m remaining`;
    return `${minutes}m remaining`;
  };

  const formatConfidence = (score: number) => {
    return `${(score * 100).toFixed(0)}%`;
  };

  return (
    <ModuleGate moduleKey="inbox.auto" fallbackMessage="Inbox Guardian is only available on the Starter plan or higher.">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Pending Approvals</h1>
          <p className="text-sm text-muted">Review and approve AI-generated email responses.</p>
        </div>
        <div className="flex items-center gap-3">
          <button 
            onClick={fetchPendingActions}
            className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors"
          >
            <RotateCw className="w-4 h-4" /> Refresh
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-12 gap-6 h-[calc(100vh-12rem)] min-h-[600px]">
        
        {/* Left Pane: Pending Actions List */}
        <div className="xl:col-span-4 lg:col-span-5 flex flex-col bg-surface border border-white/10 rounded-xl overflow-hidden shadow-lg h-full">
          <div className="p-4 border-b border-white/10 bg-surface-hover/50">
            <div className="flex items-center gap-2">
              <Clock className="w-4 h-4 text-warning" />
              <span className="text-sm font-medium">
                {loading ? "Loading..." : `${actions.length} pending`}
              </span>
            </div>
          </div>
          
          <div className="flex-1 overflow-y-auto custom-scrollbar">
            {loading && (
              <div className="flex items-center justify-center h-full">
                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
              </div>
            )}
            
            {!loading && error && (
              <div className="p-4 text-danger text-sm">{error}</div>
            )}
            
            {!loading && !error && actions.length === 0 && (
              <div className="flex flex-col items-center justify-center h-full text-muted">
                <CheckCircle2 className="w-12 h-12 mb-3 text-success/50" />
                <p className="text-sm">No pending actions</p>
                <p className="text-xs mt-1">All caught up!</p>
              </div>
            )}
            
            {!loading && !error && actions.map((action) => (
              <div 
                key={action.id}
                onClick={() => handleSelectAction(action)}
                className={`p-4 border-b border-white/5 cursor-pointer transition-all ${
                  selectedAction?.id === action.id
                    ? "bg-primary/10 border-l-[3px] border-l-primary"
                    : "hover:bg-white/5 border-l-[3px] border-l-transparent"
                }`}
              >
                <div className="flex items-center justify-between mb-2">
                  <span className="text-[10px] uppercase font-bold text-warning bg-warning/10 px-2 py-0.5 rounded-sm border border-warning/20">
                    {action.evaluatedCategory}
                  </span>
                  <span className="text-[10px] text-muted flex items-center gap-1">
                    <Clock className="w-3 h-3" /> {getTimeRemaining(action.expiresAtUtc)}
                  </span>
                </div>
                <p className="text-xs text-muted line-clamp-2 mb-2">
                  {action.policyReason || "Awaiting human review"}
                </p>
                <div className="flex items-center justify-between">
                  <span className="text-[10px] text-muted">
                    Confidence: {formatConfidence(action.confidenceScore)}
                  </span>
                  <ChevronRight className={`w-4 h-4 text-muted transition-transform ${
                    selectedAction?.id === action.id ? "rotate-90" : ""
                  }`} />
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Right Pane: Action Detail & Resolution */}
        <div className="xl:col-span-8 lg:col-span-7 flex flex-col gap-6 h-full overflow-y-auto">
          
          {!selectedAction && (
            <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg flex items-center justify-center h-full">
              <div className="text-center text-muted">
                <AlertTriangle className="w-12 h-12 mx-auto mb-3 opacity-50" />
                <p className="text-sm">Select an action to review</p>
              </div>
            </div>
          )}
          
          {selectedAction && (
            <>
              {/* Draft Response Preview */}
              <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg relative overflow-hidden">
                <div className="absolute top-0 right-0 w-[300px] h-[300px] bg-primary/5 blur-[100px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/2" />
                
                <div className="flex items-center justify-between mb-4 relative z-10">
                  <div>
                    <h2 className="text-lg font-semibold text-white mb-1">Draft Response</h2>
                    <div className="flex items-center gap-3 text-xs text-muted">
                      <span>Category: <span className="text-warning">{selectedAction.evaluatedCategory}</span></span>
                      <span>•</span>
                      <span>Confidence: {formatConfidence(selectedAction.confidenceScore)}</span>
                    </div>
                  </div>
                  <div className="text-xs text-muted">
                    Expires: {new Date(selectedAction.expiresAtUtc).toLocaleString()}
                  </div>
                </div>

                {/* Policy Reason */}
                <div className="mb-4 p-3 bg-warning/5 border border-warning/20 rounded-lg">
                  <div className="flex items-center gap-2 mb-1">
                    <AlertTriangle className="w-4 h-4 text-warning" />
                    <span className="text-xs font-bold uppercase text-warning">Policy Reason</span>
                  </div>
                  <p className="text-sm text-white/80">{selectedAction.policyReason}</p>
                </div>

                {/* Draft Response */}
                <div className="relative z-10">
                  <label className="text-xs font-bold uppercase tracking-widest text-muted mb-2 block">AI Generated Response</label>
                  <textarea
                    className="w-full h-48 bg-background border border-white/10 rounded-lg p-4 text-sm text-white focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-mono resize-none"
                    value={modifiedResponse}
                    onChange={(e) => setModifiedResponse(e.target.value)}
                    placeholder="Enter your response..."
                  />
                </div>
              </div>

              {/* Action Buttons */}
              <div className="bg-surface border border-white/10 rounded-xl p-6 shadow-lg">
                {resolveMessage && (
                  <div className={`mb-4 p-3 rounded-lg ${
                    resolveMessage.type === 'success' 
                      ? 'bg-success/10 text-success border border-success/20' 
                      : 'bg-danger/10 text-danger border border-danger/20'
                  }`}>
                    {resolveMessage.text}
                  </div>
                )}
                
                <div className="flex items-center justify-between">
                  <p className="text-xs text-muted">
                    {modifiedResponse !== selectedAction.draftResponse 
                      ? "You have modified the response" 
                      : "Response unchanged from AI draft"}
                  </p>
                  
                  <div className="flex items-center gap-3">
                    <button
                      onClick={() => handleResolve(false)}
                      disabled={isResolving}
                      className="flex items-center gap-2 px-4 py-2 bg-danger/10 text-danger border border-danger/20 rounded-lg text-sm font-medium hover:bg-danger/20 transition-all disabled:opacity-50"
                    >
                      <XCircle className="w-4 h-4" />
                      Reject
                    </button>
                    
                    <button
                      onClick={() => handleResolve(true)}
                      disabled={isResolving}
                      className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm font-medium hover:bg-primary/90 transition-all disabled:opacity-50 shadow-[0_4px_14px_var(--accent-glow)]"
                    >
                      {isResolving ? (
                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                      ) : (
                        <>
                          <CheckCircle2 className="w-4 h-4" />
                          Approve
                        </>
                      )}
                    </button>
                  </div>
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </ModuleGate>
  );
}
