"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { Key, Plus, Save, Edit2, Check, X, ToggleLeft, ToggleRight } from "lucide-react";

interface ModuleDefinition {
  id: string;
  key: string;
  displayName: string;
  description: string;
  category: string;
  tier: string;
  visibility: string;
  isOperational: boolean;
  isActive: boolean;
  isPremium: boolean;
  iconKey?: string;
  upgradePromptText?: string;
  sortOrder: number;
  createdAt: string;
}

export default function AdminModulesPage() {
  const { data: session } = useSession();
  const [modules, setModules] = useState<ModuleDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<Partial<ModuleDefinition>>({});
  const [showNew, setShowNew] = useState(false);
  const [newForm, setNewForm] = useState({ key: "", displayName: "", description: "", category: "AI", tier: "Core", isPremium: false, sortOrder: 0 });
  const [saving, setSaving] = useState(false);

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken) loadModules();
  }, [apiToken]);

  const getHeaders = () => ({
    "Authorization": `Bearer ${apiToken}`,
    "Content-Type": "application/json"
  });

  const loadModules = async () => {
    setLoading(true);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/modules`, { headers: getHeaders() });
      if (!res.ok) throw new Error(`Failed to load: ${res.status}`);
      setModules(await res.json());
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = async (id: string, current: boolean) => {
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/modules/${id}/toggle`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify({ isActive: !current })
    });
    loadModules();
  };

  const handleEdit = (mod: ModuleDefinition) => {
    setEditingId(mod.id);
    setEditForm({ ...mod });
  };

  const handleSaveEdit = async () => {
    if (!editingId) return;
    setSaving(true);
    await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/modules/${editingId}`, {
      method: "PUT",
      headers: getHeaders(),
      body: JSON.stringify(editForm)
    });
    setSaving(false);
    setEditingId(null);
    loadModules();
  };

  const handleCreate = async () => {
    setSaving(true);
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/modules`, {
      method: "POST",
      headers: getHeaders(),
      body: JSON.stringify(newForm)
    });
    setSaving(false);
    if (res.ok) {
      setShowNew(false);
      setNewForm({ key: "", displayName: "", description: "", category: "AI", tier: "Core", isPremium: false, sortOrder: 0 });
      loadModules();
    }
  };

  if (loading) return <div className="text-muted text-sm">Loading modules...</div>;
  if (error) return <div className="text-danger text-sm">{error}</div>;

  return (
    <div>
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1 flex items-center gap-2">
            <Key className="w-6 h-6 text-danger" /> Module Definitions
          </h1>
          <p className="text-sm text-muted">Manage available modules and their properties.</p>
        </div>
        <button
          onClick={() => setShowNew(!showNew)}
          className="flex items-center gap-2 px-4 py-2 bg-danger/20 text-danger text-sm font-medium rounded-lg hover:bg-danger/30 transition-colors"
        >
          <Plus className="w-4 h-4" /> New Module
        </button>
      </div>

      {showNew && (
        <div className="bg-surface border border-danger/20 rounded-xl p-6 mb-6 space-y-4">
          <h2 className="text-lg font-semibold">Create Module</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <input
              value={newForm.key}
              onChange={e => setNewForm(f => ({ ...f, key: e.target.value }))}
              placeholder="Key (e.g., inbox-guardian)"
              className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            />
            <input
              value={newForm.displayName}
              onChange={e => setNewForm(f => ({ ...f, displayName: e.target.value }))}
              placeholder="Display Name"
              className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            />
            <input
              value={newForm.description}
              onChange={e => setNewForm(f => ({ ...f, description: e.target.value }))}
              placeholder="Description"
              className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            />
            <select
              value={newForm.category}
              onChange={e => setNewForm(f => ({ ...f, category: e.target.value }))}
              className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            >
              <option value="AI">AI</option>
              <option value="Workflow">Workflow</option>
              <option value="Integration">Integration</option>
              <option value="Utility">Utility</option>
            </select>
            <select
              value={newForm.tier}
              onChange={e => setNewForm(f => ({ ...f, tier: e.target.value }))}
              className="bg-background border border-white/10 rounded-lg px-4 py-2 text-sm text-white focus:outline-none focus:border-danger/50"
            >
              <option value="Core">Core</option>
              <option value="Premium">Premium</option>
              <option value="Enterprise">Enterprise</option>
            </select>
            <label className="flex items-center gap-2 text-sm text-muted">
              <input
                type="checkbox"
                checked={newForm.isPremium}
                onChange={e => setNewForm(f => ({ ...f, isPremium: e.target.checked }))}
                className="rounded border-white/20"
              />
              Premium
            </label>
          </div>
          <div className="flex gap-2">
            <button onClick={handleCreate} disabled={saving || !newForm.key || !newForm.displayName} className="flex items-center gap-2 px-4 py-2 bg-danger/20 text-danger text-sm font-medium rounded-lg hover:bg-danger/30 transition-colors disabled:opacity-50">
              <Save className="w-4 h-4" /> {saving ? "Creating..." : "Create"}
            </button>
            <button onClick={() => setShowNew(false)} className="px-4 py-2 bg-white/5 text-white/70 text-sm rounded-lg hover:bg-white/10 transition-colors">
              Cancel
            </button>
          </div>
        </div>
      )}

      <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-surface-hover/50 border-b border-white/10">
            <tr>
              <th className="text-left px-4 py-3 text-muted font-medium">Status</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Key</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Display Name</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Category</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Tier</th>
              <th className="text-left px-4 py-3 text-muted font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {modules.length === 0 && (
              <tr><td colSpan={6} className="px-4 py-8 text-center text-muted">No modules found</td></tr>
            )}
            {modules.map(m => (
              <tr key={m.id} className="border-b border-white/5 hover:bg-white/5">
                <td className="px-4 py-3">
                  <button onClick={() => handleToggle(m.id, m.isActive)} className="text-muted hover:text-white transition-colors">
                    {m.isActive ? <ToggleRight className="w-6 h-6 text-success" /> : <ToggleLeft className="w-6 h-6 text-muted" />}
                  </button>
                </td>
                <td className="px-4 py-3">
                  {editingId === m.id ? (
                    <input
                      value={editForm.key ?? ""}
                      onChange={e => setEditForm(f => ({ ...f, key: e.target.value }))}
                      className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white w-full"
                    />
                  ) : (
                    <code className="text-xs bg-white/5 px-2 py-0.5 rounded">{m.key}</code>
                  )}
                </td>
                <td className="px-4 py-3">
                  {editingId === m.id ? (
                    <input
                      value={editForm.displayName ?? ""}
                      onChange={e => setEditForm(f => ({ ...f, displayName: e.target.value }))}
                      className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white w-full"
                    />
                  ) : m.displayName}
                </td>
                <td className="px-4 py-3">
                  {editingId === m.id ? (
                    <select
                      value={editForm.category ?? "AI"}
                      onChange={e => setEditForm(f => ({ ...f, category: e.target.value }))}
                      className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white"
                    >
                      <option value="AI">AI</option>
                      <option value="Workflow">Workflow</option>
                      <option value="Integration">Integration</option>
                      <option value="Utility">Utility</option>
                    </select>
                  ) : m.category}
                </td>
                <td className="px-4 py-3">
                  {editingId === m.id ? (
                    <select
                      value={editForm.tier ?? "Core"}
                      onChange={e => setEditForm(f => ({ ...f, tier: e.target.value }))}
                      className="bg-background border border-white/10 rounded px-2 py-1 text-sm text-white"
                    >
                      <option value="Core">Core</option>
                      <option value="Premium">Premium</option>
                      <option value="Enterprise">Enterprise</option>
                    </select>
                  ) : (
                    <span className={`text-xs px-2 py-0.5 rounded ${
                      m.tier === "Enterprise" ? "bg-purple-500/10 text-purple-400" :
                      m.tier === "Premium" ? "bg-warning/10 text-warning" :
                      "bg-success/10 text-success"
                    }`}>{m.tier}</span>
                  )}
                </td>
                <td className="px-4 py-3 flex gap-2">
                  {editingId === m.id ? (
                    <>
                      <button onClick={handleSaveEdit} disabled={saving} className="text-xs text-success hover:underline"><Check className="w-3 h-3 inline" /> Save</button>
                      <button onClick={() => setEditingId(null)} className="text-xs text-muted hover:underline"><X className="w-3 h-3 inline" /> Cancel</button>
                    </>
                  ) : (
                    <button onClick={() => handleEdit(m)} className="text-xs text-primary hover:underline"><Edit2 className="w-3 h-3 inline" /> Edit</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
