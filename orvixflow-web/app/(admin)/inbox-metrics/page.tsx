"use client";

import { useState, useEffect } from "react";
import { useSession } from "next-auth/react";
import { Mail, CheckCircle2, AlertTriangle, XCircle, Clock, Bot, Activity, TrendingUp } from "lucide-react";

interface InboxMetrics {
  totalEvents: number;
  totalActions: number;
  pendingActions: number;
  failedEvents: number;
  completedEvents: number;
  avgConfidence: number;
  feedbackCount: number;
  avgEditDistance: number;
  connectionCount: number;
  activeConnections: number;
}

export default function AdminInboxMetricsPage() {
  const { data: session } = useSession();
  const [metrics, setMetrics] = useState<InboxMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const apiToken = (session as any)?.apiToken;

  useEffect(() => {
    if (apiToken) loadMetrics();
  }, [apiToken]);

  const loadMetrics = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/admin/inbox/metrics`, {
        headers: { "Authorization": `Bearer ${apiToken}` },
      });
      if (!res.ok) throw new Error(`Failed to fetch: ${res.status}`);
      setMetrics(await res.json());
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div className="text-muted text-sm">Loading metrics...</div>;
  if (error) return <div className="text-danger text-sm">{error}</div>;
  if (!metrics) return null;

  const statCards = [
    { label: "Total Events", value: metrics.totalEvents, icon: <Mail className="w-5 h-5" />, color: "text-white" },
    { label: "Completed", value: metrics.completedEvents, icon: <CheckCircle2 className="w-5 h-5" />, color: "text-success" },
    { label: "Failed", value: metrics.failedEvents, icon: <XCircle className="w-5 h-5" />, color: "text-danger" },
    { label: "Pending Actions", value: metrics.pendingActions, icon: <Clock className="w-5 h-5" />, color: "text-warning" },
    { label: "Avg Confidence", value: `${(metrics.avgConfidence * 100).toFixed(0)}%`, icon: <TrendingUp className="w-5 h-5" />, color: "text-primary" },
    { label: "Feedback Records", value: metrics.feedbackCount, icon: <Bot className="w-5 h-5" />, color: "text-white" },
    { label: "Avg Edit Distance", value: `${(metrics.avgEditDistance * 100).toFixed(0)}%`, icon: <Activity className="w-5 h-5" />, color: "text-white" },
    { label: "Active Connections", value: `${metrics.activeConnections}/${metrics.connectionCount}`, icon: <Mail className="w-5 h-5" />, color: "text-success" },
  ];

  return (
    <div>
      <div className="flex justify-between items-end mb-6">
        <div>
          <h1 className="text-2xl font-semibold mb-1">Inbox Guardian Metrics</h1>
          <p className="text-sm text-muted">Global overview of Inbox Guardian performance and usage.</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        {statCards.map(stat => (
          <div key={stat.label} className="bg-surface border border-white/10 rounded-xl p-5">
            <div className="flex items-center justify-between mb-3">
              <span className="text-muted">{stat.icon}</span>
            </div>
            <div className={`text-2xl font-bold ${stat.color}`}>{stat.value}</div>
            <div className="text-xs text-muted mt-1">{stat.label}</div>
          </div>
        ))}
      </div>

      <div className="mt-8 bg-surface border border-white/10 rounded-xl p-6">
        <h2 className="text-lg font-semibold mb-4">Health Summary</h2>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted">Success Rate</span>
            <span className="text-sm font-medium">
              {metrics.totalEvents > 0
                ? `${((metrics.completedEvents / metrics.totalEvents) * 100).toFixed(1)}%`
                : "N/A"}
            </span>
          </div>
          <div className="w-full bg-background rounded-full h-2">
            <div
              className="bg-success h-2 rounded-full transition-all"
              style={{ width: `${metrics.totalEvents > 0 ? (metrics.completedEvents / metrics.totalEvents) * 100 : 0}%` }}
            />
          </div>

          <div className="flex items-center justify-between mt-4">
            <span className="text-sm text-muted">Failure Rate</span>
            <span className="text-sm font-medium text-danger">
              {metrics.totalEvents > 0
                ? `${((metrics.failedEvents / metrics.totalEvents) * 100).toFixed(1)}%`
                : "N/A"}
            </span>
          </div>
          <div className="w-full bg-background rounded-full h-2">
            <div
              className="bg-danger h-2 rounded-full transition-all"
              style={{ width: `${metrics.totalEvents > 0 ? (metrics.failedEvents / metrics.totalEvents) * 100 : 0}%` }}
            />
          </div>

          <div className="flex items-center justify-between mt-4">
            <span className="text-sm text-muted">Avg AI Confidence</span>
            <span className="text-sm font-medium text-primary">{(metrics.avgConfidence * 100).toFixed(0)}%</span>
          </div>
          <div className="w-full bg-background rounded-full h-2">
            <div
              className="bg-primary h-2 rounded-full transition-all"
              style={{ width: `${metrics.avgConfidence * 100}%` }}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
