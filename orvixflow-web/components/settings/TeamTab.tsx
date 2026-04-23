/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { Mail, CheckCircle, XCircle, Trash2, Shield, Users, Pencil } from "lucide-react";

import { canManageMember, canManageOrganization, getAssignableCompanyRoles } from "@/lib/org-permissions";

type Member = {
  userId: string;
  email: string;
  displayName: string;
  companyRole: string;
  joinedAt: string;
  departmentIds: string[];
};

type Invite = {
  id: string;
  email: string;
  assignedRole: string;
  departmentId?: string | null;
  createdAt: string;
};

type Department = {
  departmentId: string;
  name: string;
  code: string;
  role: string;
};

export function TeamTab({ currentRole }: { currentRole?: string | null }) {
  const { data: session } = useSession();
  const [members, setMembers] = useState<Member[]>([]);
  const [invites, setInvites] = useState<Invite[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);

  // Invite Form State
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("Viewer");
  const [inviteDepartmentId, setInviteDepartmentId] = useState("");
  const [inviteMessage, setInviteMessage] = useState<{ text: string; isError: boolean } | null>(null);
  const [isInviting, setIsInviting] = useState(false);
  const [departmentEditorMember, setDepartmentEditorMember] = useState<Member | null>(null);
  const [selectedDepartmentIds, setSelectedDepartmentIds] = useState<string[]>([]);
  const [isSavingDepartments, setIsSavingDepartments] = useState(false);
  const resolvedRole = currentRole ?? session?.user?.role ?? null;
  const canManageOrg = canManageOrganization(resolvedRole);
  const assignableRoles = getAssignableCompanyRoles(resolvedRole);

  const fetchTeamData = async () => {
    if (!(session as any)?.apiToken) return;
    try {
      const headers = { Authorization: `Bearer ${(session as any).apiToken}` };
      const apiUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';
      
      const [membersRes, invitesRes, departmentsRes] = await Promise.all([
        fetch(`${apiUrl}/api/team`, { headers }),
        fetch(`${apiUrl}/api/invite`, { headers }),
        fetch(`${apiUrl}/api/org/departments`, { headers })
      ]);

      if (membersRes.ok) {
        setMembers(await membersRes.json());
      }
      if (invitesRes.ok) {
        setInvites(await invitesRes.json());
      }
      if (departmentsRes.ok) {
        setDepartments(await departmentsRes.json());
      }
    } catch (e) {
      console.error("Failed to load team data", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTeamData();
  }, [session]);

  const handleSendInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!(session as any)?.apiToken) return;
    
    setIsInviting(true);
    setInviteMessage(null);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/invite`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${(session as any).apiToken}`,
        },
        body: JSON.stringify({
          email: inviteEmail,
          assignedRole: inviteRole,
          departmentId: inviteDepartmentId || null,
        }),
      });

      const data = await res.json();
      if (res.ok) {
        setInviteMessage({ text: "Invitation sent successfully!", isError: false });
        setInviteEmail("");
        setInviteDepartmentId("");
        fetchTeamData(); // Refresh pending list
      } else {
        setInviteMessage({ text: data.error || "Failed to send invitation.", isError: true });
      }
    } catch (error) {
      setInviteMessage({ text: "A network error occurred.", isError: true });
    } finally {
      setIsInviting(false);
    }
  };

  const handleRemoveMember = async (userId: string) => {
    if (!(session as any)?.apiToken) return;
    if (!confirm("Remove this member from the active company?")) return;

    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/team/${userId}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${(session as any).apiToken}` },
      });

      if (res.ok) {
        fetchTeamData();
        return;
      }

      const err = await res.json();
      alert(err.error || "Failed to remove member");
    } catch (e) {
      console.error(e);
    }
  };

  const handleUpdateDepartments = async (userId: string, departmentIds: string[]) => {
    if (!(session as any)?.apiToken) return;

    try {
      setIsSavingDepartments(true);
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/team/${userId}/departments`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${(session as any).apiToken}`,
        },
        body: JSON.stringify({ departmentIds }),
      });

      if (res.ok) {
        setDepartmentEditorMember(null);
        fetchTeamData();
        return;
      }

      const err = await res.json();
      alert(err.error || "Failed to update departments");
    } catch (e) {
      console.error(e);
    } finally {
      setIsSavingDepartments(false);
    }
  };

  const getDepartmentName = (departmentId?: string | null) => {
    if (!departmentId) return "Unassigned";
    return departments.find((department) => department.departmentId === departmentId)?.name ?? "Unknown Department";
  };

  const openDepartmentEditor = (member: Member) => {
    setDepartmentEditorMember(member);
    setSelectedDepartmentIds(member.departmentIds);
  };

  const toggleDepartmentSelection = (departmentId: string) => {
    setSelectedDepartmentIds((current) =>
      current.includes(departmentId)
        ? current.filter((id) => id !== departmentId)
        : [...current, departmentId]
    );
  };

  if (!canManageOrg) {
    return <div className="text-sm text-muted">Company Admin permissions are required to manage team members.</div>;
  }

  const handleRevokeInvite = async (inviteId: string) => {
    if (!(session as any)?.apiToken) return;
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/invite/${inviteId}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${(session as any).apiToken}` },
      });
      if (res.ok) fetchTeamData();
    } catch (e) {
      console.error(e);
    }
  };

  const handleChangeRole = async (userId: string, newRole: string) => {
    if (!(session as any)?.apiToken) return;
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/team/${userId}/role`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${(session as any).apiToken}`,
        },
        body: JSON.stringify({ newRole })
      });
      if (res.ok) fetchTeamData();
      else {
        const err = await res.json();
        alert(err.error || "Failed to update role");
      }
    } catch (e) {
      console.error(e);
    }
  };

  if (loading) {
    return <div className="animate-pulse text-muted text-sm">Loading team data...</div>;
  }

  return (
    <div className="animate-in fade-in duration-300 flex flex-col gap-8">
      <div>
        <h2 className="text-lg font-semibold mb-1">Team & Roles</h2>
        <p className="text-sm text-muted">Manage your organization members and their access levels.</p>
      </div>

      {/* Invite Section */}
      <div className="bg-background border border-white/5 rounded-xl p-6">
        <h3 className="text-sm font-semibold mb-4 flex items-center gap-2">
          <Mail className="w-4 h-4 text-primary" />
          Invite a New Member
        </h3>
        <form onSubmit={handleSendInvite} className="flex flex-col md:flex-row items-start md:items-end gap-4">
          <div className="flex flex-col gap-1.5 flex-1 w-full">
            <label className="text-xs font-medium text-muted mb-1">Email Address</label>
            <input 
              type="email" 
              required
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              placeholder="colleague@company.com" 
              className="bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
            />
          </div>
          <div className="flex flex-col gap-1.5 w-full md:w-64 shrink-0">
            <label className="text-xs font-medium text-muted mb-1">Role</label>
            <select
              value={inviteRole}
              onChange={(e) => setInviteRole(e.target.value)}
              className="bg-surface border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 hover:bg-white/5 transition-colors"
            >
              {assignableRoles.map(role => (
                <option key={role} value={role}>{role}</option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5 w-full md:w-64 shrink-0">
            <label className="text-xs font-medium text-muted mb-1">Department</label>
            <select
              value={inviteDepartmentId}
              onChange={(e) => setInviteDepartmentId(e.target.value)}
              className="bg-surface border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 hover:bg-white/5 transition-colors"
            >
              <option value="">No department</option>
              {departments.map((department) => (
                <option key={department.departmentId} value={department.departmentId}>{department.name}</option>
              ))}
            </select>
          </div>
          <button 
            type="submit"
            disabled={isInviting || !inviteEmail || assignableRoles.length === 0}
            className="px-5 py-2.5 bg-primary hover:bg-primary/90 disabled:bg-primary/50 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all whitespace-nowrap"
          >
            {isInviting ? "Sending..." : "Send Invite"}
          </button>
        </form>
        {inviteMessage && (
          <div className={`mt-4 text-sm flex items-center gap-2 ${inviteMessage.isError ? "text-danger" : "text-emerald-400"}`}>
            {inviteMessage.isError ? <XCircle className="w-4 h-4" /> : <CheckCircle className="w-4 h-4" />}
            {inviteMessage.text}
          </div>
        )}
      </div>

      <div className="h-px bg-white/5" />

      {/* Pending Invites */}
      {invites.length > 0 && (
        <div>
          <h3 className="text-sm font-semibold mb-4 text-muted">Pending Invitations ({invites.length})</h3>
          <div className="bg-surface border border-white/5 rounded-xl overflow-hidden">
            <table className="w-full text-sm text-left">
              <thead className="bg-white/5 text-muted text-xs uppercase tracking-wider">
                <tr>
                  <th className="px-6 py-3 font-medium">Email</th>
                  <th className="px-6 py-3 font-medium">Assigned Role</th>
                  <th className="px-6 py-3 font-medium">Department</th>
                  <th className="px-6 py-3 font-medium">Sent At</th>
                  <th className="px-6 py-3 font-medium text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {invites.map((invite) => (
                  <tr key={invite.id} className="hover:bg-white/5 transition-colors">
                    <td className="px-6 py-3 text-white">{invite.email}</td>
                     <td className="px-6 py-3">
                       <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted">
                         {invite.assignedRole}
                       </span>
                     </td>
                     <td className="px-6 py-3 text-muted text-xs">{getDepartmentName(invite.departmentId)}</td>
                     <td className="px-6 py-3 text-muted">{new Date(invite.createdAt).toLocaleDateString()}</td>
                     <td className="px-6 py-3 text-right">
                      <button 
                        onClick={() => handleRevokeInvite(invite.id)}
                        className="text-muted hover:text-danger p-1 rounded hover:bg-danger/10 transition-colors"
                        title="Revoke Invite"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Active Members */}
      <div>
        <h3 className="text-sm font-semibold mb-4 text-muted">Active Members ({members.length})</h3>
        <div className="bg-surface border border-white/5 rounded-xl overflow-x-auto shadow-lg">
          <table className="w-full min-w-[860px] text-sm text-left">
            <thead className="bg-white/5 text-muted text-xs uppercase tracking-wider font-semibold border-b border-white/5">
              <tr>
                <th className="px-6 py-4">User</th>
                <th className="px-6 py-4">Role</th>
                <th className="px-6 py-4">Departments</th>
                <th className="px-6 py-4">Joined</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {members.map((member) => (
                <tr key={member.userId} className="hover:bg-white/[0.02] transition-colors group">
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center text-primary font-bold text-xs ring-1 ring-primary/30">
                        {member.displayName?.charAt(0) || "U"}
                      </div>
                      <div>
                        <div className="font-medium text-white">{member.displayName}</div>
                        <div className="text-xs text-muted">{member.email}</div>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4">
                    {canManageMember(resolvedRole, member.companyRole, member.userId === (session?.user as any)?.id) ? (
                      <select
                        value={member.companyRole}
                        onChange={(e) => handleChangeRole(member.userId, e.target.value)}
                        className="min-w-36 bg-transparent border border-transparent group-hover:border-white/10 rounded px-2 py-1 text-xs cursor-pointer focus:outline-none focus:border-primary focus:bg-surface transition-all"
                      >
                        {assignableRoles.map((role) => (
                          <option key={role} value={role} className="bg-surface text-white">{role}</option>
                        ))}
                      </select>
                    ) : (
                      <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted">
                        {member.companyRole}
                      </span>
                    )}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex flex-wrap items-center gap-2">
                      {member.departmentIds.length > 0 ? (
                        member.departmentIds.map((departmentId) => (
                          <span key={departmentId} className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted">
                            {getDepartmentName(departmentId)}
                          </span>
                        ))
                      ) : (
                        <span className="text-xs text-muted">No departments</span>
                      )}

                      {canManageMember(resolvedRole, member.companyRole, member.userId === (session?.user as any)?.id) ? (
                        <button
                          onClick={() => openDepartmentEditor(member)}
                          className="inline-flex items-center gap-1 rounded-md border border-white/10 px-2 py-1 text-xs text-muted hover:bg-white/5 hover:text-white transition-colors"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                          Edit
                        </button>
                      ) : null}
                    </div>
                  </td>
                  <td className="px-6 py-4 text-muted text-xs">
                    {new Date(member.joinedAt).toLocaleDateString()}
                  </td>
                  <td className="px-6 py-4 text-right">
                    {canManageMember(resolvedRole, member.companyRole, member.userId === (session?.user as any)?.id) ? (
                      <button
                        onClick={() => handleRemoveMember(member.userId)}
                        className="text-muted hover:text-danger p-1 rounded hover:bg-danger/10 transition-colors"
                        title="Remove Member"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {departmentEditorMember && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg rounded-2xl border border-white/10 bg-surface shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
              <div>
                <h4 className="text-base font-semibold text-white">Edit Department Assignments</h4>
                <p className="mt-1 text-sm text-muted">{departmentEditorMember.displayName} can belong to multiple departments.</p>
              </div>
              <button
                onClick={() => setDepartmentEditorMember(null)}
                className="rounded-md p-1 text-muted hover:bg-white/5 hover:text-white transition-colors"
                title="Close"
              >
                <XCircle className="h-5 w-5" />
              </button>
            </div>

            <div className="max-h-[60vh] overflow-y-auto px-6 py-4">
              {departments.length === 0 ? (
                <p className="text-sm text-muted">No departments are available for assignment yet.</p>
              ) : (
                <div className="space-y-3">
                  {departments.map((department) => {
                    const checked = selectedDepartmentIds.includes(department.departmentId);

                    return (
                      <label
                        key={department.departmentId}
                        className="flex cursor-pointer items-center justify-between rounded-xl border border-white/10 bg-background px-4 py-3 hover:border-white/20"
                      >
                        <div>
                          <div className="font-medium text-white">{department.name}</div>
                          <div className="text-xs text-muted">{department.code}</div>
                        </div>
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleDepartmentSelection(department.departmentId)}
                          className="h-4 w-4 rounded border-white/20 bg-surface text-primary focus:ring-primary/40"
                        />
                      </label>
                    );
                  })}
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-white/10 px-6 py-4">
              <button
                type="button"
                onClick={() => setDepartmentEditorMember(null)}
                className="rounded-lg border border-white/10 px-4 py-2 text-sm text-white hover:bg-white/5 transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => handleUpdateDepartments(departmentEditorMember.userId, selectedDepartmentIds)}
                disabled={isSavingDepartments}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isSavingDepartments ? "Saving..." : "Save Departments"}
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
