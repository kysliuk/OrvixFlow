/* eslint-disable @typescript-eslint/no-explicit-any, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { CheckCircle, XCircle, Trash2, Edit2, Network, Plus } from "lucide-react";

import { canAccessDepartmentScopedOrganizationSettings, canManageOrganization } from "@/lib/org-permissions";

type Department = {
  departmentId: string;
  name: string;
  code: string;
  role: string;
};

export function DepartmentsTab({ currentRole }: { currentRole?: string | null }) {
  const { data: session } = useSession();
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);

  const [isEditing, setIsEditing] = useState<string | null>(null);
  const [formName, setFormName] = useState("");
  const [formCode, setFormCode] = useState("");
  const [formMessage, setFormMessage] = useState<{ text: string; isError: boolean } | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const resolvedRole = currentRole ?? session?.user?.role ?? null;
  const canManageDepartments = canManageOrganization(resolvedRole);
  const canAccessDepartments = canAccessDepartmentScopedOrganizationSettings(resolvedRole, departments);

  const fetchDepartments = async () => {
    if (!(session as any)?.apiToken) return;
    try {
      const headers = { Authorization: `Bearer ${(session as any).apiToken}` };
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

      const res = await fetch(`${apiUrl}/api/org/departments`, { headers });
      if (res.ok) {
        setDepartments(await res.json());
      }
    } catch (e) {
      console.error("Failed to load departments", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDepartments();
  }, [session]);

  const resetForm = () => {
    setIsEditing(null);
    setFormName("");
    setFormCode("");
    setFormMessage(null);
  };

  const handleEditClick = (dept: Department) => {
    setIsEditing(dept.departmentId);
    setFormName(dept.name);
    setFormCode(dept.code);
    setFormMessage(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!(session as any)?.apiToken) return;

    setIsSubmitting(true);
    setFormMessage(null);

    try {
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
      const isCreate = isEditing === "new";
      const url = isCreate ? `${apiUrl}/api/org/departments` : `${apiUrl}/api/org/departments/${isEditing}`;

      const res = await fetch(url, {
        method: isCreate ? "POST" : "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${(session as any).apiToken}`,
        },
        body: JSON.stringify({ name: formName, code: formCode }),
      });

      const data = await res.json();
      if (res.ok) {
        setFormMessage({ text: `Department ${isCreate ? "created" : "updated"} successfully!`, isError: false });
        resetForm();
        fetchDepartments();
      } else {
        setFormMessage({ text: data.error || "Failed to save department.", isError: true });
      }
    } catch {
      setFormMessage({ text: "A network error occurred.", isError: true });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!(session as any)?.apiToken) return;
    if (!confirm("Are you sure you want to delete this department? This may disrupt assigned users and isolated data.")) return;

    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/org/departments/${id}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${(session as any).apiToken}` },
      });
      if (res.ok) fetchDepartments();
      else alert("Failed to delete the department.");
    } catch (e) {
      console.error(e);
      alert("Network error deleting department.");
    }
  };

  if (loading) {
    return <div className="animate-pulse text-muted text-sm">Loading department data...</div>;
  }

  if (!canAccessDepartments) {
    return <div className="text-sm text-muted">Company Admin or Department Manager permissions are required to access departments.</div>;
  }

  return (
    <div className="animate-in fade-in duration-300 flex flex-col gap-8">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold mb-1">Departments</h2>
          <p className="text-sm text-muted">Manage isolated data boundaries and organizational units.</p>
        </div>
        {canManageDepartments && !isEditing ? (
          <button
            onClick={() => {
              resetForm();
              setIsEditing("new");
            }}
            className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary hover:bg-primary/20 rounded-lg text-sm font-medium transition-colors border border-primary/20"
          >
            <Plus className="w-4 h-4" />
            New Department
          </button>
        ) : null}
      </div>

      {!canManageDepartments ? (
        <div className="rounded-xl border border-white/10 bg-background px-4 py-3 text-sm text-muted">
          Department creation is limited to Company Admins and Company Owners. Department Managers can review their department access here.
        </div>
      ) : null}

      {canManageDepartments && isEditing !== null ? (
        <div className="bg-background border border-primary/30 rounded-xl p-6 shadow-[0_0_15px_rgba(var(--color-primary),0.1)]">
          <h3 className="text-sm font-semibold mb-4 flex items-center gap-2 text-white">
            <Network className="w-4 h-4 text-primary" />
            {isEditing === "new" ? "Create New Department" : "Edit Department"}
          </h3>
          <form onSubmit={handleSubmit} className="flex flex-col md:flex-row items-start md:items-end gap-4">
            <div className="flex flex-col gap-1.5 flex-1 w-full">
              <label className="text-xs font-medium text-muted mb-1">Department Name</label>
              <input
                type="text"
                required
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                placeholder="e.g. Finance & Accounting"
                className="bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
              />
            </div>
            <div className="flex flex-col gap-1.5 flex-1 w-full">
              <label className="text-xs font-medium text-muted mb-1">Internal Code</label>
              <input
                type="text"
                required
                value={formCode}
                onChange={(e) => setFormCode(e.target.value)}
                placeholder="e.g. FIN-01"
                className="bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 font-mono"
              />
            </div>
            <div className="flex items-center gap-2 w-full md:w-auto mt-2 md:mt-0">
              <button
                type="button"
                onClick={resetForm}
                className="px-4 py-2.5 bg-surface hover:bg-white/5 border border-white/10 text-white font-medium rounded-lg text-sm transition-all whitespace-nowrap"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting || !formName || !formCode}
                className="px-5 py-2.5 bg-primary hover:bg-primary/90 disabled:bg-primary/50 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all whitespace-nowrap"
              >
                {isSubmitting ? "Saving..." : "Save Department"}
              </button>
            </div>
          </form>
          {formMessage ? (
            <div className={`mt-4 text-sm flex items-center gap-2 ${formMessage.isError ? "text-danger" : "text-emerald-400"}`}>
              {formMessage.isError ? <XCircle className="w-4 h-4" /> : <CheckCircle className="w-4 h-4" />}
              {formMessage.text}
            </div>
          ) : null}
        </div>
      ) : null}

      <div>
        <div className="bg-surface border border-white/5 rounded-xl overflow-hidden shadow-lg">
          <table className="w-full text-sm text-left">
            <thead className="bg-white/5 text-muted text-xs uppercase tracking-wider font-semibold border-b border-white/5">
              <tr>
                <th className="px-6 py-4">Department Name</th>
                <th className="px-6 py-4">Code</th>
                <th className="px-6 py-4">Your Access</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {departments.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-6 py-8 text-center text-muted">
                    No departments have been configured for this organization.
                  </td>
                </tr>
              ) : (
                departments.map((dept) => (
                  <tr key={dept.departmentId} className="hover:bg-white/[0.02] transition-colors group">
                    <td className="px-6 py-4">
                      <div className="font-medium text-white flex items-center gap-2">
                        <Network className="w-4 h-4 text-primary/70" />
                        {dept.name}
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted font-mono">{dept.code}</span>
                    </td>
                    <td className="px-6 py-4">
                      <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted">{dept.role || "—"}</span>
                    </td>
                    <td className="px-6 py-4 text-right">
                      {canManageDepartments ? (
                        <div className="flex items-center justify-end gap-2">
                          <button
                            onClick={() => handleEditClick(dept)}
                            className="text-muted hover:text-white p-1.5 rounded hover:bg-white/10 transition-colors"
                            title="Edit Department"
                          >
                            <Edit2 className="w-4 h-4" />
                          </button>
                          <button
                            onClick={() => handleDelete(dept.departmentId)}
                            className="text-muted hover:text-danger p-1.5 rounded hover:bg-danger/10 transition-colors"
                            title="Delete Department"
                          >
                            <Trash2 className="w-4 h-4" />
                          </button>
                        </div>
                      ) : (
                        <span className="text-xs text-muted">View only</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
