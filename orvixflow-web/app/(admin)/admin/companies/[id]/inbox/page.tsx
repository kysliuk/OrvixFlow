/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars, react-hooks/exhaustive-deps */
"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Clock, AlertTriangle, CheckCircle2, XCircle, Shield, Mail } from "lucide-react";

interface InboxEvent {
  id: string;
  messageId: string;
  senderEmail: string;
  subject: string;
  status: string;
  receivedAtUtc: string;
}

interface StuckAction {
  id: string;
  inboxEventId: string;
  evaluatedCategory: string;
  confidenceScore: number;
  status: string;
  createdAtUtc: string;
  expiresAtUtc: string;
}

interface Policy {
  id: string;
  category: string;
  autoExecute: boolean;
  confidenceThreshold: number;
}

interface CompanyInboxData {
  events: InboxEvent[];
  stuckActions: StuckAction[];
  policies: Policy[];
}

export default function AdminCompanyInboxPage() {
  const { data: session } = useSession();
  const params = useParams();
  const router = useRouter();
  const companyId = params.id as string;

  const [data, setData] = useState<CompanyInboxData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<"events" | "stuck" | "policies">("stuck");

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken && companyId) loadData();
  }, [apiToken, companyId]);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/inbox/companies/${companyId}/inbox`, {
        headers: { "Authorization": `Bearer ${apiToken}` },
      });
      if (!res.ok) throw new Error(`Failed to fetch: ${res.status}`);
      setData(await res.json());
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "Human_Approved":
      case "Auto_Approved":
        return <CheckCircle2 className="w-4 h-4 text-success" />;
      case "Failed":
        return <XCircle className="w-4 h-4 text-danger" />;
      case "Action_Required":
        return <AlertTriangle className="w-4 h-4 text-warning" />;
      default:
        return <Clock className="w-4 h-4 text-muted" />;
    }
  };

  const timeSince = (dateStr: string) => {
    const date = new Date(dateStr);
    const now = new Date();
    const hours = Math.floor((now.getTime() - date.getTime()) / (1000 * 60 * 60));
    if (hours < 1) return "Just now";
    if (hours < 24) return `${hours}h ago`;
    return `${Math.floor(hours / 24)}d ago`;
  };

  const tabs = [
    { key: "stuck" as const, label: `Stuck Actions (${data?.stuckActions.length || 0})`, icon: <AlertTriangle className="w-4 h-4" /> },
    { key: "events" as const, label: `Events (${data?.events.length || 0})`, icon: <Mail className="w-4 h-4" /> },
    { key: "policies" as const, label: `Policies (${data?.policies.length || 0})`, icon: <Shield className="w-4 h-4" /> },
  ];

  if (loading) return <div className="text-muted text-sm">Loading company inbox data...</div>;
  if (error) return <div className="text-danger text-sm">{error}</div>;
  if (!data) return null;

  return (
    <div>
      <div className="flex items-center gap-4 mb-6">
        <Link href="/admin/tenants" className="text-muted hover:text-white transition-colors">
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-2xl font-semibold mb-1">Company Inbox</h1>
          <p className="text-sm text-muted">View stuck actions, events, and policies for this company.</p>
        </div>
      </div>

      <div className="flex gap-1 mb-6 border-b border-white/10">
        {tabs.map(tab => (
          <button
            key={tab.key}
            onClick={() => setActiveTab(tab.key)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === tab.key ? "border-danger text-white" : "border-transparent text-muted hover:text-white"
            }`}
          >
            {tab.icon} {tab.label}
          </button>
        ))}
      </div>

      {/* Stuck Actions */}
      {activeTab === "stuck" && (
        <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
          {data.stuckActions.length === 0 ? (
            <div className="px-4 py-12 text-center text-muted">
              <CheckCircle2 className="w-12 h-12 mx-auto mb-3 text-success/50" />
              <p>No stuck actions — all caught up!</p>
            </div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-hover/50 border-b border-white/10">
                <tr>
                  <th className="text-left px-4 py-3 text-muted font-medium">Category</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Confidence</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Created</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Expires</th>
                </tr>
              </thead>
              <tbody>
                {data.stuckActions.map(a => (
                  <tr key={a.id} className="border-b border-white/5 hover:bg-white/5">
                    <td className="px-4 py-3">
                      <span className="text-xs px-2 py-0.5 rounded bg-warning/10 text-warning">{a.evaluatedCategory}</span>
                    </td>
                    <td className="px-4 py-3">{(a.confidenceScore * 100).toFixed(0)}%</td>
                    <td className="px-4 py-3 text-muted">{timeSince(a.createdAtUtc)}</td>
                    <td className="px-4 py-3 text-muted">
                      {new Date(a.expiresAtUtc).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Events */}
      {activeTab === "events" && (
        <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
          {data.events.length === 0 ? (
            <div className="px-4 py-12 text-center text-muted">No events recorded</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-hover/50 border-b border-white/10">
                <tr>
                  <th className="text-left px-4 py-3 text-muted font-medium">Status</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Sender</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Subject</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Received</th>
                </tr>
              </thead>
              <tbody>
                {data.events.slice(0, 100).map(e => (
                  <tr key={e.id} className="border-b border-white/5 hover:bg-white/5">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {getStatusIcon(e.status)}
                        <span className="text-xs">{e.status.replace(/_/g, " ")}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-white">{e.senderEmail}</td>
                    <td className="px-4 py-3 text-muted max-w-md truncate">{e.subject}</td>
                    <td className="px-4 py-3 text-muted whitespace-nowrap">
                      {timeSince(e.receivedAtUtc)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}

      {/* Policies */}
      {activeTab === "policies" && (
        <div className="bg-surface border border-white/10 rounded-xl overflow-hidden">
          {data.policies.length === 0 ? (
            <div className="px-4 py-12 text-center text-muted">No policies configured</div>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-surface-hover/50 border-b border-white/10">
                <tr>
                  <th className="text-left px-4 py-3 text-muted font-medium">Category</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Auto-Execute</th>
                  <th className="text-left px-4 py-3 text-muted font-medium">Confidence Threshold</th>
                </tr>
              </thead>
              <tbody>
                {data.policies.map(p => (
                  <tr key={p.id} className="border-b border-white/5 hover:bg-white/5">
                    <td className="px-4 py-3">{p.category}</td>
                    <td className="px-4 py-3">
                      <span className={`text-xs px-2 py-0.5 rounded ${p.autoExecute ? "bg-success/10 text-success" : "bg-muted/10 text-muted"}`}>
                        {p.autoExecute ? "Yes" : "No"}
                      </span>
                    </td>
                    <td className="px-4 py-3">{(p.confidenceThreshold * 100).toFixed(0)}%</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}
