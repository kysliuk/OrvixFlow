"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { ModuleGate } from "@/components/module-gate";
import { Settings, Mail, Shield, Bot, Plus, Trash2, Save, RefreshCw, AlertTriangle, Check } from "lucide-react";

interface WorkflowPolicy {
  id: string;
  category: string;
  autoExecute: boolean;
  confidenceThreshold: number;
  excludedKeywords: string;
}

interface AgentPersona {
  id?: string;
  tone: string;
  customInstructions: string;
  customSignOff?: string;
  updatedAtUtc?: string;
}

interface MailboxConnection {
  id: string;
  emailAddress: string;
  provider: string;
  isActive: boolean;
  n8nWorkflowId?: string;
  createdAtUtc: string;
  connectedAtUtc?: string;
}

type Tab = "connections" | "policies" | "persona";

export default function InboxSettingsPage() {
  const { data: session } = useSession();
  const [activeTab, setActiveTab] = useState<Tab>("connections");
  const [loading, setLoading] = useState(true);

  // Connections
  const [connections, setConnections] = useState<MailboxConnection[]>([]);
  const [newEmail, setNewEmail] = useState("");
  const [newProvider, setNewProvider] = useState("Gmail");

  // Policies
  const [policies, setPolicies] = useState<WorkflowPolicy[]>([]);
  const [editingPolicy, setEditingPolicy] = useState<WorkflowPolicy | null>(null);
  const [newPolicy, setNewPolicy] = useState({ category: "", autoExecute: false, confidenceThreshold: 0.85, excludedKeywords: "" });

  // Persona
  const [persona, setPersona] = useState<AgentPersona>({ tone: "Professional", customInstructions: "", customSignOff: "" });
  const [personaSaved, setPersonaSaved] = useState(false);

  // Provisioning errors
  const [provisioningErrors, setProvisioningErrors] = useState<Map<string, string>>(new Map());
  const [provisioningIds, setProvisioningIds] = useState<Set<string>>(new Set());

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken) {
      loadAll();
    }
  }, [apiToken]);

  const getHeaders = () => {
    const headers: Record<string, string> = { "Content-Type": "application/json", "Authorization": `Bearer ${apiToken}` };
    const imp = localStorage.getItem("impersonateTenantId");
    if (imp) headers["X-Impersonate-Tenant"] = imp;
    return headers;
  };

  const loadAll = async () => {
    setLoading(true);
    await Promise.all([loadConnections(), loadPolicies(), loadPersona()]);
    setLoading(false);
  };

  const loadConnections = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/connections`, { headers: getHeaders() });
      if (res.ok) setConnections(await res.json());
    } catch { /* ignore */ }
  };

  const loadPolicies = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/policies`, { headers: getHeaders() });
      if (res.ok) setPolicies(await res.json());
    } catch { /* ignore */ }
  };

  const loadPersona = async () => {
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/persona`, { headers: getHeaders() });
      if (res.ok) setPersona(await res.json());
    } catch { /* ignore */ }
  };

  const handleAddConnection = async () => {
    if (!newEmail || !newProvider) return;
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/connections`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify({ emailAddress: newEmail, provider: newProvider }),
    });
    if (res.ok) {
      setNewEmail("");
      await loadConnections();
    }
  };

  const handleToggleConnection = async (id: string, isActive: boolean) => {
    if (isActive) {
      setProvisioningIds(prev => new Set(prev).add(id));
      setProvisioningErrors(prev => {
        const next = new Map(prev);
        next.delete(id);
        return next;
      });
    }
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/connections/${id}/activate`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify({ isActive }),
    });
    if (res.ok) {
      const data = await res.json();
      if (!data.isActive) {
        const pollInterval = setInterval(async () => {
          const checkRes = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/connections`, { headers: getHeaders() });
          if (checkRes.ok) {
            const connections = await checkRes.json();
            const conn = connections.find((c: any) => c.id === id);
            if (conn?.isActive) {
              clearInterval(pollInterval);
              setProvisioningIds(prev => {
                const next = new Set(prev);
                next.delete(id);
                return next;
              });
              setConnections(connections);
            } else if (conn && !conn.isActive && !conn.n8nWorkflowId) {
              clearInterval(pollInterval);
              setProvisioningIds(prev => {
                const next = new Set(prev);
                next.delete(id);
                return next;
              });
              setProvisioningErrors(prev => {
                const next = new Map(prev);
                next.set(id, "Provisioning failed. Please try again or contact support.");
                return next;
              });
              setConnections(connections);
            }
          }
        }, 3000);
      }
      setConnections(prev => prev.map(c => c.id === id ? { ...c, ...data } : c));
    } else {
      setProvisioningIds(prev => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      const errorData = await res.json().catch(() => ({}));
      setProvisioningErrors(prev => {
        const next = new Map(prev);
        next.set(id, errorData.error || "Failed to activate connection");
        return next;
      });
    }
  };

  const handleDeleteConnection = async (id: string) => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/connections/${id}`, {
      method: "DELETE",
      headers: getHeaders(),
    });
    await loadConnections();
  };

  const handleCreatePolicy = async () => {
    if (!newPolicy.category) return;
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/policies`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify(newPolicy),
    });
    if (res.ok) {
      setNewPolicy({ category: "", autoExecute: false, confidenceThreshold: 0.85, excludedKeywords: "" });
      await loadPolicies();
    }
  };

  const handleUpdatePolicy = async () => {
    if (!editingPolicy) return;
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/policies/${editingPolicy.id}`, {
      method: "PUT",
      headers: getHeaders(),
      body: JSON.stringify(editingPolicy),
    });
    setEditingPolicy(null);
    await loadPolicies();
  };

  const handleDeletePolicy = async (id: string) => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/policies/${id}`, {
      method: "DELETE",
      headers: getHeaders(),
    });
    await loadPolicies();
  };

  const handleSavePersona = async () => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/inbox/settings/persona`, {
      method: "PUT",
      headers: getHeaders(),
      body: JSON.stringify(persona),
    });
    setPersonaSaved(true);
    setTimeout(() => setPersonaSaved(false), 2000);
  };

  const tabs: { key: Tab; label: string; icon: React.ReactNode }[] = [
    { key: "connections", label: "Connections", icon: <Mail className="w-4 h-4" /> },
    { key: "policies", label: "Policies", icon: <Shield className="w-4 h-4" /> },
    { key: "persona", label: "Persona", icon: <Bot className="w-4 h-4" /> },
  ];

  return (
    <ModuleGate moduleKey="inbox-guardian" fallbackMessage="Inbox Guardian is only available on the Starter plan or higher.">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Inbox Settings</h1>
          <p className="text-sm text-muted">Configure mailbox connections, auto-approval policies, and AI persona.</p>
        </div>
        <button onClick={loadAll} className="flex items-center gap-2 px-3 py-1.5 bg-surface border border-white/10 rounded-md text-sm text-muted hover:text-white transition-colors">
          <RefreshCw className="w-4 h-4" /> Refresh
        </button>
      </div>

      <div className="flex gap-1 mb-6 border-b border-white/10">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.key ? "border-primary text-white" : "border-transparent text-muted hover:text-white"
            }`}
          >
            {tab.icon} {tab.label}
          </button>
        ))}
      </div>

      {loading && <div className="text-muted text-sm">Loading...</div>}

      {/* Connections Tab */}
      {!loading && activeTab === "connections" && (
        <div className="space-y-6">
          <div className="bg-surface border border-white/10 rounded-xl p-6">
            <h2 className="text-lg font-semibold mb-4">Connect Mailbox</h2>
            <div className="flex gap-3">
              <input
                type="email"
                value={newEmail}
                onChange={e => setNewEmail(e.target.value)}
                placeholder="email@example.com"
                className="flex-1 bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
              />
              <select
                value={newProvider}
                onChange={e => setNewProvider(e.target.value)}
                className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
              >
                <option value="Gmail">Gmail</option>
                <option value="Outlook">Outlook</option>
                <option value="IMAP">IMAP</option>
              </select>
              <button
                onClick={handleAddConnection}
                className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm font-medium hover:bg-primary/90 transition-all"
              >
                <Plus className="w-4 h-4" /> Connect
              </button>
            </div>
          </div>

          <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-surface-hover/50 border-b border-white/10">
                <tr>
                  <th className="text-left px-4 py-3 text-muted font-medium">Email</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Provider</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Status</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {connections.length === 0 && (
                  <tr><td colSpan={4} className="px-4 py-8 text-center text-muted">No connections yet</td></tr>
                )}
                {connections.map(c => (
                  <tr key={c.id} className="border-b border-white/5 hover:bg-white/5">
                    <td className="px-4 py-3">{c.emailAddress}</td>
                    <td className="px-4 py-3">{c.provider}</td>
                    <td className="px-4 py-3">
                      {provisioningErrors.has(c.id) ? (
                        <div>
                          <span className="text-xs px-2 py-0.5 rounded bg-danger/10 text-danger">Error</span>
                          <span className="block text-xs text-danger mt-1">{provisioningErrors.get(c.id)}</span>
                        </div>
                      ) : c.isActive ? (
                        <div>
                          <span className="text-xs px-2 py-0.5 rounded bg-success/10 text-success">Active</span>
                          {!c.n8nWorkflowId && (
                            <span className="block text-xs text-warning mt-1">Pending setup</span>
                          )}
                        </div>
                      ) : (
                        <span className="text-xs px-2 py-0.5 rounded bg-muted/10 text-muted">Inactive</span>
                      )}
                    </td>
                    <td className="px-4 py-3 flex gap-2">
                      <button
                        onClick={() => handleToggleConnection(c.id, !c.isActive)}
                        className="text-xs text-primary hover:underline"
                      >
                        {c.isActive ? "Deactivate" : "Activate"}
                      </button>
                      <button onClick={() => handleDeleteConnection(c.id)} className="text-xs text-danger hover:underline">
                        <Trash2 className="w-3 h-3 inline" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {connections.some(c => c.isActive && !c.n8nWorkflowId) && (
            <div className="bg-primary/10 border border-primary/30 rounded-xl p-4 flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-primary flex-shrink-0 mt-0.5" />
              <div>
                <h3 className="font-medium text-primary">Complete OAuth Setup</h3>
                <p className="text-sm text-white/70 mt-1">
                  Your mailbox connection is pending OAuth authentication. Complete the setup in n8n to enable email processing.
                </p>
                <a
                  href={`${process.env.NEXT_PUBLIC_N8N_URL || "http://localhost:5678"}/credentials`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1 mt-2 text-sm text-primary hover:underline"
                >
                  Open n8n Credentials →
                </a>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Policies Tab */}
      {!loading && activeTab === "policies" && (
        <div className="space-y-6">
          <div className="bg-surface border border-white/10 rounded-xl p-6">
            <h2 className="text-lg font-semibold mb-4">Create Policy</h2>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-3">
              <input
                value={newPolicy.category}
                onChange={e => setNewPolicy(p => ({ ...p, category: e.target.value }))}
                placeholder="Category (e.g. Support)"
                className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
              />
              <input
                type="number"
                step="0.05"
                min="0"
                max="1"
                value={newPolicy.confidenceThreshold}
                onChange={e => setNewPolicy(p => ({ ...p, confidenceThreshold: parseFloat(e.target.value) || 0.85 }))}
                className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
              />
              <input
                value={newPolicy.excludedKeywords}
                onChange={e => setNewPolicy(p => ({ ...p, excludedKeywords: e.target.value }))}
                placeholder="Excluded keywords (comma-sep)"
                className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
              />
              <div className="flex items-center gap-3">
                <label className="flex items-center gap-2 text-sm text-muted">
                  <input
                    type="checkbox"
                    checked={newPolicy.autoExecute}
                    onChange={e => setNewPolicy(p => ({ ...p, autoExecute: e.target.checked }))}
                    className="rounded border-white/20"
                  />
                  Auto-execute
                </label>
                <button
                  onClick={handleCreatePolicy}
                  className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm font-medium hover:bg-primary/90 transition-all"
                >
                  <Plus className="w-4 h-4" /> Add
                </button>
              </div>
            </div>
          </div>

          <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-surface-hover/50 border-b border-white/10">
                <tr>
                  <th className="text-left px-4 py-3 text-muted font-medium">Category</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Auto-Execute</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Confidence</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Excluded Keywords</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {policies.length === 0 && (
                  <tr><td colSpan={5} className="px-4 py-8 text-center text-muted">No policies configured</td></tr>
                )}
                {policies.map(p => (
                  <tr key={p.id} className="border-b border-white/5 hover:bg-white/5">
                    <td className="px-4 py-3">
                      {editingPolicy?.id === p.id ? (
                        <input
                          value={editingPolicy.category}
                          onChange={e => setEditingPolicy({ ...editingPolicy, category: e.target.value })}
                          className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white w-full"
                        />
                      ) : p.category}
                    </td>
                    <td className="px-4 py-3">
                      {editingPolicy?.id === p.id ? (
                        <input
                          type="checkbox"
                          checked={editingPolicy.autoExecute}
                          onChange={e => setEditingPolicy({ ...editingPolicy, autoExecute: e.target.checked })}
                        />
                      ) : (
                        <span className={`text-xs px-2 py-0.5 rounded ${p.autoExecute ? "bg-success/10 text-success" : "bg-muted/10 text-muted"}`}>
                          {p.autoExecute ? "Yes" : "No"}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      {editingPolicy?.id === p.id ? (
                        <input
                          type="number"
                          step="0.05"
                          value={editingPolicy.confidenceThreshold}
                          onChange={e => setEditingPolicy({ ...editingPolicy, confidenceThreshold: parseFloat(e.target.value) || 0.85 })}
                          className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white w-20"
                        />
                      ) : `${(p.confidenceThreshold * 100).toFixed(0)}%`}
                    </td>
                    <td className="px-4 py-3">
                      {editingPolicy?.id === p.id ? (
                        <input
                          value={editingPolicy.excludedKeywords}
                          onChange={e => setEditingPolicy({ ...editingPolicy, excludedKeywords: e.target.value })}
                          className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white w-full"
                        />
                      ) : p.excludedKeywords || "—"}
                    </td>
                    <td className="px-4 py-3 flex gap-2">
                      {editingPolicy?.id === p.id ? (
                        <>
                          <button onClick={handleUpdatePolicy} className="text-xs text-success hover:underline"><Save className="w-3 h-3 inline" /> Save</button>
                          <button onClick={() => setEditingPolicy(null)} className="text-xs text-muted hover:underline">Cancel</button>
                        </>
                      ) : (
                        <>
                          <button onClick={() => setEditingPolicy(p)} className="text-xs text-primary hover:underline">Edit</button>
                          <button onClick={() => handleDeletePolicy(p.id)} className="text-xs text-danger hover:underline"><Trash2 className="w-3 h-3 inline" /></button>
                        </>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Persona Tab */}
      {!loading && activeTab === "persona" && (
        <div className="bg-surface border border-white/10 rounded-xl p-6 space-y-6">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">AI Persona</h2>
            <button
              onClick={handleSavePersona}
              className="flex items-center gap-2 px-4 py-2 bg-primary text-white rounded-lg text-sm font-medium hover:bg-primary/90 transition-all"
            >
              {personaSaved ? <Check className="w-4 h-4" /> : <Save className="w-4 h-4" />}
              {personaSaved ? "Saved" : "Save"}
            </button>
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-widest text-muted mb-2 block">Tone</label>
            <select
              value={persona.tone}
              onChange={e => setPersona(p => ({ ...p, tone: e.target.value }))}
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
            >
              <option value="Professional">Professional</option>
              <option value="Casual">Casual</option>
              <option value="Technical">Technical</option>
              <option value="Friendly">Friendly</option>
              <option value="Formal">Formal</option>
            </select>
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-widest text-muted mb-2 block">Custom Instructions</label>
            <textarea
              value={persona.customInstructions}
              onChange={e => setPersona(p => ({ ...p, customInstructions: e.target.value }))}
              placeholder="Additional instructions for the AI (e.g., always mention our 30-day guarantee)"
              rows={4}
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50 resize-none"
            />
          </div>

          <div>
            <label className="text-xs font-bold uppercase tracking-widest text-muted mb-2 block">Custom Sign-Off</label>
            <input
              value={persona.customSignOff || ""}
              onChange={e => setPersona(p => ({ ...p, customSignOff: e.target.value }))}
              placeholder="e.g., Best regards, The Support Team"
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-primary/50"
            />
          </div>
        </div>
      )}
    </ModuleGate>
  );
}
