/* eslint-disable @typescript-eslint/no-explicit-any, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import {
  Mail,
  CheckCircle,
  XCircle,
  Trash2,
  Pencil,
  Shield,
} from "lucide-react";

import {
  canAccessDepartmentScopedOrganizationSettings,
  canManageMember,
  canManageOrganization,
  getAssignableCompanyRoles,
  getManagedDepartmentIds,
} from "@/lib/org-permissions";

// ─── Types ────────────────────────────────────────────────────────────────────

type DeptMembership = {
  departmentId: string;
  departmentRole: string;
};

type Member = {
  userId: string;
  email: string;
  displayName: string;
  companyRole: string;
  joinedAt: string;
  /** Legacy flat list kept for backward compat */
  departmentIds: string[];
  /** Per-department role data returned by the updated API */
  departments: DeptMembership[];
};

type Invite = {
  id: string;
  email: string;
  assignedRole: string;
  invitedDepartmentRole?: string | null;
  departmentId?: string | null;
  createdAt: string;
};

type Department = {
  departmentId: string;
  name: string;
  code: string;
  role: string;
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

const DEPT_ROLE_LABELS: Record<string, string> = {
  DepartmentManager: "Manager",
  DepartmentOperator: "Operator",
};

function deptRoleLabel(role: string): string {
  return DEPT_ROLE_LABELS[role] ?? role;
}

const DEPT_ROLE_BADGE: Record<string, string> = {
  DepartmentManager:
    "bg-violet-500/15 text-violet-300 border-violet-500/30",
  DepartmentOperator:
    "bg-sky-500/15 text-sky-300 border-sky-500/30",
};

function deptRoleBadgeClass(role: string): string {
  return DEPT_ROLE_BADGE[role] ?? "bg-white/5 text-muted border-white/10";
}

function getAssignableDepartmentRoles(canManageOrg: boolean): string[] {
  return canManageOrg
    ? ["DepartmentOperator", "DepartmentManager"]
    : ["DepartmentOperator"];
}

// ─── Component ────────────────────────────────────────────────────────────────

export function TeamTab({ currentRole }: { currentRole?: string | null }) {
  const { data: session } = useSession();
  const [members, setMembers] = useState<Member[]>([]);
  const [invites, setInvites] = useState<Invite[]>([]);
  const [departments, setDepartments] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);

  // invite form
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteCompanyRole, setInviteCompanyRole] = useState("CompanyMember");
  const [inviteDepartmentRole, setInviteDepartmentRole] =
    useState("DepartmentOperator");
  const [inviteDepartmentId, setInviteDepartmentId] = useState("");
  const [inviteMessage, setInviteMessage] = useState<{
    text: string;
    isError: boolean;
  } | null>(null);
  const [isInviting, setIsInviting] = useState(false);

  // department assignment editor (CompanyAdmin+)
  const [departmentEditorMember, setDepartmentEditorMember] =
    useState<Member | null>(null);
  const [selectedDepartmentIds, setSelectedDepartmentIds] = useState<string[]>(
    []
  );
  const [isSavingDepartments, setIsSavingDepartments] = useState(false);

  // dept-role editor (per-dept modal, all managers)
  const [deptRoleEditor, setDeptRoleEditor] = useState<{
    member: Member;
    departmentId: string;
    currentRole: string;
  } | null>(null);
  const [deptRoleEditorValue, setDeptRoleEditorValue] = useState("");
  const [isSavingDeptRole, setIsSavingDeptRole] = useState(false);
  const [deptRoleError, setDeptRoleError] = useState<string | null>(null);

  const resolvedRole = currentRole ?? session?.user?.role ?? null;
  const canManageOrg = canManageOrganization(resolvedRole);
  const assignableRoles = getAssignableCompanyRoles(resolvedRole);
  const managedDepartmentIds = getManagedDepartmentIds(departments);
  const canManageTeam = canAccessDepartmentScopedOrganizationSettings(
    resolvedRole,
    departments
  );
  const assignableDepartmentRoles = getAssignableDepartmentRoles(canManageOrg);

  const visibleInviteDepartments = canManageOrg
    ? departments
    : departments.filter((d) => managedDepartmentIds.includes(d.departmentId));

  useEffect(() => {
    if (
      canManageOrg &&
      assignableRoles.length > 0 &&
      !assignableRoles.includes(inviteCompanyRole)
    ) {
      setInviteCompanyRole(assignableRoles[0]);
    }
  }, [assignableRoles, canManageOrg, inviteCompanyRole]);

  useEffect(() => {
    if (!assignableDepartmentRoles.includes(inviteDepartmentRole)) {
      setInviteDepartmentRole(assignableDepartmentRoles[0]);
    }
  }, [assignableDepartmentRoles, inviteDepartmentRole]);

  // ── Data fetching ──────────────────────────────────────────────────────────

  const fetchTeamData = async () => {
    if (!(session as any)?.apiToken) return;
    try {
      const headers = {
        Authorization: `Bearer ${(session as any).apiToken}`,
      };
      const apiUrl =
        process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

      const [membersRes, invitesRes, departmentsRes] = await Promise.all([
        fetch(`${apiUrl}/api/team`, { headers }),
        fetch(`${apiUrl}/api/invite`, { headers }),
        fetch(`${apiUrl}/api/org/departments`, { headers }),
      ]);

      if (membersRes.ok) {
        const raw = await membersRes.json();
        // Normalise: ensure `departments` array is always present
        const normalised: Member[] = (raw as any[]).map((m) => ({
          ...m,
          departments: m.departments ?? [],
          departmentIds: m.departmentIds ?? [],
        }));
        setMembers(normalised);
      }
      if (invitesRes.ok) setInvites(await invitesRes.json());
      if (departmentsRes.ok) setDepartments(await departmentsRes.json());
    } catch (e) {
      console.error("Failed to load team data", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTeamData();
  }, [session]);

  // ── Actions ────────────────────────────────────────────────────────────────

  const handleSendInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!(session as any)?.apiToken) return;
    setIsInviting(true);
    setInviteMessage(null);
    try {
      const assignedRole = canManageOrg ? inviteCompanyRole : "CompanyMember";
      const invitedDepartmentRolePayload = inviteDepartmentId
        ? inviteDepartmentRole
        : null;
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/invite`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${(session as any).apiToken}`,
          },
          body: JSON.stringify({
            email: inviteEmail,
            assignedRole,
            departmentId: inviteDepartmentId || null,
            invitedDepartmentRole: invitedDepartmentRolePayload,
          }),
        }
      );
      const data = await res.json();
      if (res.ok) {
        setInviteMessage({ text: "Invitation sent successfully!", isError: false });
        setInviteEmail("");
        setInviteDepartmentId("");
        setInviteDepartmentRole("DepartmentOperator");
        fetchTeamData();
      } else {
        setInviteMessage({
          text: data.error || "Failed to send invitation.",
          isError: true,
        });
      }
    } catch {
      setInviteMessage({ text: "A network error occurred.", isError: true });
    } finally {
      setIsInviting(false);
    }
  };

  const handleRemoveMember = async (userId: string) => {
    if (!(session as any)?.apiToken) return;
    if (!confirm("Remove this member from the active company?")) return;
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/team/${userId}`,
        {
          method: "DELETE",
          headers: { Authorization: `Bearer ${(session as any).apiToken}` },
        }
      );
      if (res.ok) { fetchTeamData(); return; }
      const err = await res.json();
      alert(err.error || "Failed to remove member");
    } catch (e) {
      console.error(e);
    }
  };

  const handleUpdateDepartments = async (
    userId: string,
    deptIds: string[]
  ) => {
    if (!(session as any)?.apiToken) return;
    try {
      setIsSavingDepartments(true);
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/team/${userId}/departments`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${(session as any).apiToken}`,
          },
          body: JSON.stringify({ departmentIds: deptIds }),
        }
      );
      if (res.ok) { setDepartmentEditorMember(null); fetchTeamData(); return; }
      const err = await res.json();
      alert(err.error || "Failed to update departments");
    } catch (e) {
      console.error(e);
    } finally {
      setIsSavingDepartments(false);
    }
  };

  const handleRevokeInvite = async (inviteId: string) => {
    if (!(session as any)?.apiToken) return;
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/invite/${inviteId}`,
        {
          method: "DELETE",
          headers: { Authorization: `Bearer ${(session as any).apiToken}` },
        }
      );
      if (res.ok) fetchTeamData();
    } catch (e) {
      console.error(e);
    }
  };

  const handleChangeCompanyRole = async (userId: string, newRole: string) => {
    if (!(session as any)?.apiToken) return;
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/team/${userId}/role`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${(session as any).apiToken}`,
          },
          body: JSON.stringify({ newRole }),
        }
      );
      if (res.ok) { fetchTeamData(); return; }
      const err = await res.json();
      alert(err.error || "Failed to update role");
    } catch (e) {
      console.error(e);
    }
  };

  const handleSaveDeptRole = async () => {
    if (!deptRoleEditor || !(session as any)?.apiToken) return;
    setIsSavingDeptRole(true);
    setDeptRoleError(null);
    try {
      const res = await fetch(
        `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/team/${deptRoleEditor.member.userId}/department-role`,
        {
          method: "PUT",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${(session as any).apiToken}`,
          },
          body: JSON.stringify({
            departmentId: deptRoleEditor.departmentId,
            newDepartmentRole: deptRoleEditorValue,
          }),
        }
      );
      if (res.ok) {
        setDeptRoleEditor(null);
        fetchTeamData();
      } else {
        const err = await res.json();
        setDeptRoleError(err.error || "Failed to update department role.");
      }
    } catch {
      setDeptRoleError("A network error occurred.");
    } finally {
      setIsSavingDeptRole(false);
    }
  };

  // ── Helpers ────────────────────────────────────────────────────────────────

  const getDepartmentName = (departmentId?: string | null) => {
    if (!departmentId) return "Unassigned";
    return (
      departments.find((d) => d.departmentId === departmentId)?.name ??
      "Unknown"
    );
  };

  const openDepartmentEditor = (member: Member) => {
    setDepartmentEditorMember(member);
    setSelectedDepartmentIds(member.departmentIds);
  };

  const toggleDepartmentSelection = (departmentId: string) => {
    setSelectedDepartmentIds((curr) =>
      curr.includes(departmentId)
        ? curr.filter((id) => id !== departmentId)
        : [...curr, departmentId]
    );
  };

  const openDeptRoleEditor = (
    member: Member,
    deptId: string,
    currentDeptRole: string
  ) => {
    setDeptRoleEditor({ member, departmentId: deptId, currentRole: currentDeptRole });
    setDeptRoleEditorValue(
      assignableDepartmentRoles.includes(currentDeptRole)
        ? currentDeptRole
        : assignableDepartmentRoles[0]
    );
    setDeptRoleError(null);
  };

  /** Can the current user change this member's dept role in `deptId`? */
  const canEditDeptRole = (member: Member, deptId: string): boolean => {
    if (member.userId === (session?.user as any)?.id) return false;
    if (canManageOrg) return true;
    // DeptManager: only in departments they manage
    return managedDepartmentIds.includes(deptId);
  };

  // ── Guards ─────────────────────────────────────────────────────────────────

  if (loading) {
    return (
      <div className="animate-pulse text-muted text-sm">
        Loading team data...
      </div>
    );
  }

  if (!canManageTeam) {
    return (
      <div className="text-sm text-muted">
        Company Admin or Department Manager permissions are required to manage
        team members.
      </div>
    );
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <div className="animate-in fade-in duration-300 flex flex-col gap-8">
      <div>
        <h2 className="text-lg font-semibold mb-1">Team &amp; Roles</h2>
        <p className="text-sm text-muted">
          Manage your organization members and their access levels.
        </p>
      </div>

      {/* ── Invite Form ─────────────────────────────────────────────────── */}
      <div className="bg-background border border-white/5 rounded-xl p-6">
        <h3 className="text-sm font-semibold mb-4 flex items-center gap-2">
          <Mail className="w-4 h-4 text-primary" />
          Invite a New Member
        </h3>

        {!canManageOrg && (
          <div className="mb-4 rounded-lg border border-white/10 bg-surface px-4 py-3 text-sm text-muted">
            Invites from Department Managers are scoped to a managed department.
            The invited user will join as{" "}
            <span className="text-white font-medium">CompanyMember</span>.
          </div>
        )}

        <form onSubmit={handleSendInvite} className="flex flex-col gap-4">
          {/* Row 1: Email (full width) */}
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-medium text-muted">
              Email Address
            </label>
            <input
              type="email"
              required
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              placeholder="colleague@company.com"
              className="bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
            />
          </div>

          {/* Row 2: selects grid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {canManageOrg && (
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-muted">
                  Company Role
                </label>
                <select
                  data-testid="company-role-select"
                  value={inviteCompanyRole}
                  onChange={(e) => setInviteCompanyRole(e.target.value)}
                  className="bg-surface border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 hover:bg-white/5 transition-colors"
                >
                  {assignableRoles.map((role) => (
                    <option key={role} value={role}>
                      {role}
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted">
                Department
                {!canManageOrg && (
                  <span className="ml-1 text-danger">*</span>
                )}
              </label>
              <select
                value={inviteDepartmentId}
                onChange={(e) => setInviteDepartmentId(e.target.value)}
                className="bg-surface border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 hover:bg-white/5 transition-colors"
              >
                <option value="">
                  {canManageOrg ? "No department" : "Select department…"}
                </option>
                {visibleInviteDepartments.map((d) => (
                  <option key={d.departmentId} value={d.departmentId}>
                    {d.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-medium text-muted">
                Department Role
              </label>
              <select
                data-testid="department-role-select"
                value={inviteDepartmentRole}
                onChange={(e) => setInviteDepartmentRole(e.target.value)}
                disabled={!inviteDepartmentId}
                className="bg-surface border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 hover:bg-white/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
              >
                {assignableDepartmentRoles.map((role) => (
                  <option key={role} value={role}>
                    {deptRoleLabel(role)}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Row 3: submit + feedback */}
          <div className="flex items-center gap-4 flex-wrap">
            <button
              type="submit"
              disabled={
                isInviting ||
                !inviteEmail ||
                (!canManageOrg && !inviteDepartmentId)
              }
              className="px-5 py-2.5 bg-primary hover:bg-primary/90 disabled:bg-primary/50 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all whitespace-nowrap"
            >
              {isInviting ? "Sending…" : "Send Invite"}
            </button>

            {inviteMessage && (
              <div
                className={`flex items-center gap-2 text-sm ${
                  inviteMessage.isError ? "text-danger" : "text-emerald-400"
                }`}
              >
                {inviteMessage.isError ? (
                  <XCircle className="w-4 h-4 shrink-0" />
                ) : (
                  <CheckCircle className="w-4 h-4 shrink-0" />
                )}
                {inviteMessage.text}
              </div>
            )}
          </div>
        </form>
      </div>

      <div className="h-px bg-white/5" />

      {/* ── Pending Invitations ─────────────────────────────────────────── */}
      {invites.length > 0 && (
        <div>
          <h3 className="text-sm font-semibold mb-4 text-muted">
            Pending Invitations ({invites.length})
          </h3>
          <div className="bg-surface border border-white/5 rounded-xl overflow-hidden">
            <table className="w-full text-sm text-left">
              <thead className="bg-white/5 text-muted text-xs uppercase tracking-wider">
                <tr>
                  <th className="px-6 py-3 font-medium">Email</th>
                  <th className="px-6 py-3 font-medium">Access</th>
                  <th className="px-6 py-3 font-medium">Department</th>
                  <th className="px-6 py-3 font-medium">Sent</th>
                  <th className="px-6 py-3 font-medium text-right">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {invites.map((invite) => (
                  <tr
                    key={invite.id}
                    className="hover:bg-white/5 transition-colors"
                  >
                    <td className="px-6 py-3 text-white">{invite.email}</td>
                    <td className="px-6 py-3">
                      <span
                        className={`px-2 py-0.5 rounded text-xs border ${deptRoleBadgeClass(
                          invite.invitedDepartmentRole ?? ""
                        )}`}
                      >
                        {invite.invitedDepartmentRole
                          ? deptRoleLabel(invite.invitedDepartmentRole)
                          : invite.assignedRole}
                      </span>
                    </td>
                    <td className="px-6 py-3 text-muted text-xs">
                      {getDepartmentName(invite.departmentId)}
                    </td>
                    <td className="px-6 py-3 text-muted">
                      {new Date(invite.createdAt).toLocaleDateString()}
                    </td>
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

      {/* ── Active Members ──────────────────────────────────────────────── */}
      <div>
        <h3 className="text-sm font-semibold mb-4 text-muted">
          Active Members ({members.length})
        </h3>
        <div className="bg-surface border border-white/5 rounded-xl overflow-x-auto shadow-lg">
          <table className="w-full min-w-[780px] text-sm text-left">
            <thead className="bg-white/5 text-muted text-xs uppercase tracking-wider font-semibold border-b border-white/5">
              <tr>
                <th className="px-6 py-4">User</th>
                <th className="px-6 py-4">Company Role</th>
                <th className="px-6 py-4">Departments &amp; Roles</th>
                <th className="px-6 py-4">Joined</th>
                <th className="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {members.map((member) => {
                const canManageCompanyMember =
                  canManageOrg &&
                  canManageMember(
                    resolvedRole,
                    member.companyRole,
                    member.userId === (session?.user as any)?.id
                  );

                return (
                  <tr
                    key={member.userId}
                    className="hover:bg-white/[0.02] transition-colors group"
                  >
                    {/* User */}
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center text-primary font-bold text-xs ring-1 ring-primary/30 shrink-0">
                          {member.displayName?.charAt(0) || "U"}
                        </div>
                        <div>
                          <div className="font-medium text-white">
                            {member.displayName}
                          </div>
                          <div className="text-xs text-muted">
                            {member.email}
                          </div>
                        </div>
                      </div>
                    </td>

                    {/* Company Role */}
                    <td className="px-6 py-4">
                      {canManageCompanyMember ? (
                        <select
                          value={member.companyRole}
                          onChange={(e) =>
                            handleChangeCompanyRole(
                              member.userId,
                              e.target.value
                            )
                          }
                          className="min-w-36 bg-transparent border border-transparent group-hover:border-white/10 rounded px-2 py-1 text-xs cursor-pointer focus:outline-none focus:border-primary focus:bg-surface transition-all"
                        >
                          {assignableRoles.map((role) => (
                            <option
                              key={role}
                              value={role}
                              className="bg-surface text-white"
                            >
                              {role}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <span className="px-2 py-0.5 rounded text-xs border border-white/10 bg-white/5 text-muted">
                          {member.companyRole}
                        </span>
                      )}
                    </td>

                    {/* Departments & Dept Roles */}
                    <td className="px-6 py-4">
                      <div className="flex flex-wrap items-center gap-2">
                        {member.departments.length > 0 ? (
                          member.departments.map((dm) => {
                            const canEdit = canEditDeptRole(
                              member,
                              dm.departmentId
                            );
                            return (
                              <div
                                key={dm.departmentId}
                                className="flex items-center gap-1"
                              >
                                <span className="text-xs text-muted/70">
                                  {getDepartmentName(dm.departmentId)}
                                </span>
                                <span
                                  className={`px-2 py-0.5 rounded text-xs border ${deptRoleBadgeClass(
                                    dm.departmentRole
                                  )}`}
                                >
                                  {deptRoleLabel(dm.departmentRole)}
                                </span>
                                {canEdit && (
                                  <button
                                    onClick={() =>
                                      openDeptRoleEditor(
                                        member,
                                        dm.departmentId,
                                        dm.departmentRole
                                      )
                                    }
                                    className="p-0.5 rounded text-muted/50 hover:text-white hover:bg-white/10 transition-colors"
                                    title={`Edit role in ${getDepartmentName(dm.departmentId)}`}
                                  >
                                    <Shield className="w-3 h-3" />
                                  </button>
                                )}
                              </div>
                            );
                          })
                        ) : (
                          <span className="text-xs text-muted">
                            No departments
                          </span>
                        )}

                        {/* Edit dept assignments (CompanyAdmin+ only) */}
                        {canManageCompanyMember && (
                          <button
                            onClick={() => openDepartmentEditor(member)}
                            className="inline-flex items-center gap-1 rounded-md border border-white/10 px-2 py-1 text-xs text-muted hover:bg-white/5 hover:text-white transition-colors"
                          >
                            <Pencil className="h-3.5 w-3.5" />
                            Edit depts
                          </button>
                        )}
                      </div>
                    </td>

                    {/* Joined */}
                    <td className="px-6 py-4 text-muted text-xs">
                      {new Date(member.joinedAt).toLocaleDateString()}
                    </td>

                    {/* Actions */}
                    <td className="px-6 py-4 text-right">
                      {canManageCompanyMember ? (
                        <button
                          data-testid={`remove-company-member-${member.userId}`}
                          onClick={() => handleRemoveMember(member.userId)}
                          className="text-muted hover:text-danger p-1 rounded hover:bg-danger/10 transition-colors"
                          title="Remove Member"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      ) : (
                        <span className="text-xs text-muted">Scoped</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* ── Dept Assignment Editor Modal (CompanyAdmin+) ─────────────── */}
      {canManageOrg && departmentEditorMember && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-lg rounded-2xl border border-white/10 bg-surface shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
              <div>
                <h4 className="text-base font-semibold text-white">
                  Edit Department Assignments
                </h4>
                <p className="mt-1 text-sm text-muted">
                  {departmentEditorMember.displayName} can belong to multiple
                  departments. Roles are set per department after assignment.
                </p>
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
                <p className="text-sm text-muted">
                  No departments available yet.
                </p>
              ) : (
                <div className="space-y-3">
                  {departments.map((dept) => {
                    const checked = selectedDepartmentIds.includes(
                      dept.departmentId
                    );
                    const existingDeptMembership =
                      departmentEditorMember.departments.find(
                        (d) => d.departmentId === dept.departmentId
                      );
                    return (
                      <label
                        key={dept.departmentId}
                        className="flex cursor-pointer items-center justify-between rounded-xl border border-white/10 bg-background px-4 py-3 hover:border-white/20 transition-colors"
                      >
                        <div className="flex items-center gap-3">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={() =>
                              toggleDepartmentSelection(dept.departmentId)
                            }
                            className="h-4 w-4 rounded border-white/20 bg-surface text-primary focus:ring-primary/40"
                          />
                          <div>
                            <div className="font-medium text-white text-sm">
                              {dept.name}
                            </div>
                            <div className="text-xs text-muted">
                              {dept.code}
                            </div>
                          </div>
                        </div>
                        {existingDeptMembership && (
                          <span
                            className={`px-2 py-0.5 rounded text-xs border ${deptRoleBadgeClass(
                              existingDeptMembership.departmentRole
                            )}`}
                          >
                            {deptRoleLabel(
                              existingDeptMembership.departmentRole
                            )}
                          </span>
                        )}
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
                onClick={() =>
                  handleUpdateDepartments(
                    departmentEditorMember.userId,
                    selectedDepartmentIds
                  )
                }
                disabled={isSavingDepartments}
                className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isSavingDepartments ? "Saving…" : "Save Departments"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Dept Role Editor Modal (all managers, per-dept) ───────────── */}
      {deptRoleEditor && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="w-full max-w-sm rounded-2xl border border-white/10 bg-surface shadow-2xl">
            <div className="flex items-center justify-between border-b border-white/10 px-6 py-4">
              <div>
                <h4 className="text-base font-semibold text-white flex items-center gap-2">
                  <Shield className="w-4 h-4 text-primary" />
                  Edit Department Role
                </h4>
                <p className="mt-1 text-sm text-muted">
                  <span className="text-white">
                    {deptRoleEditor.member.displayName}
                  </span>{" "}
                  in{" "}
                  <span className="text-white">
                    {getDepartmentName(deptRoleEditor.departmentId)}
                  </span>
                </p>
              </div>
              <button
                onClick={() => setDeptRoleEditor(null)}
                className="rounded-md p-1 text-muted hover:bg-white/5 hover:text-white transition-colors"
                title="Close"
              >
                <XCircle className="h-5 w-5" />
              </button>
            </div>

            <div className="px-6 py-5 flex flex-col gap-4">
              <div className="flex flex-col gap-1.5">
                <label className="text-xs font-medium text-muted">
                  Department Role
                </label>
                <select
                  data-testid="dept-role-editor-select"
                  value={deptRoleEditorValue}
                  onChange={(e) => setDeptRoleEditorValue(e.target.value)}
                  className="bg-background border border-white/10 rounded-lg px-3 py-2.5 text-sm outline-none text-white focus:border-primary/50 transition-colors"
                >
                  {assignableDepartmentRoles.map((role) => (
                    <option key={role} value={role}>
                      {role === "DepartmentManager"
                        ? "Manager — can manage members & data in this department"
                        : "Operator — can use modules, view data"}
                    </option>
                  ))}
                </select>
              </div>

              {deptRoleError && (
                <div className="flex items-center gap-2 text-sm text-danger">
                  <XCircle className="w-4 h-4 shrink-0" />
                  {deptRoleError}
                </div>
              )}

              <p className="text-xs text-muted/70">
                This change only affects{" "}
                <span className="text-muted">
                  {getDepartmentName(deptRoleEditor.departmentId)}
                </span>
                . Other department roles for this user are not affected.
              </p>
            </div>

            <div className="flex items-center justify-end gap-3 border-t border-white/10 px-6 py-4">
              <button
                type="button"
                onClick={() => setDeptRoleEditor(null)}
                className="rounded-lg border border-white/10 px-4 py-2 text-sm text-white hover:bg-white/5 transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSaveDeptRole}
                disabled={
                  isSavingDeptRole ||
                  deptRoleEditorValue === deptRoleEditor.currentRole
                }
                className="rounded-lg bg-primary px-4 py-2 text-sm font-medium text-white hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                {isSavingDeptRole ? "Saving…" : "Save Role"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
