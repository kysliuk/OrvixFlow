"use client";

import { useSession } from "next-auth/react";
import { Lock } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

interface ModuleGateProps {
  moduleKey: string;
  children: React.ReactNode;
  fallbackMessage?: string;
  requiredLimits?: { type: string; amount?: number }[];
}

interface PermissionData {
  canView: boolean;
  canUse: boolean;
}

interface LimitData {
  allowed: boolean;
  exceededLimit?: string;
  currentUsage?: number;
  limit?: number;
  upgradeUrl?: string;
}

const MODULE_LIMIT_TYPES: Record<string, string> = {
  "agent": "ai-tokens",
  "inbox-guardian": "inbox-messages",
  "knowledge-base": "knowledge-bases",
};

export function ModuleGate({ moduleKey, children, fallbackMessage, requiredLimits }: ModuleGateProps) {
  const { data: session, status } = useSession();
  const [permissions, setPermissions] = useState<PermissionData | null>(null);
  const [limitStatus, setLimitStatus] = useState<LimitData | null>(null);
  const limitTypes = requiredLimits || [{ type: MODULE_LIMIT_TYPES[moduleKey] || "ai-tokens" }];
  const activeLimit = limitTypes.find((limit) => limit.type);

  useEffect(() => {
    if (!session?.apiToken) return;

    fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/modules/${moduleKey}/permissions`, {
      headers: { Authorization: `Bearer ${session.apiToken}` },
    })
      .then(async (res) => {
        if (res.status === 404) {
          setPermissions({ canView: false, canUse: false });
          return null;
        }
        if (!res.ok) throw new Error("Failed permissions fetch");
        return res.json();
      })
      .then((data) => {
        if (data) {
          setPermissions({ canView: !!data.canView, canUse: !!data.canUse });
        }
      })
      .catch(() => setPermissions({ canView: false, canUse: false }));
  }, [moduleKey, session?.apiToken]);

  useEffect(() => {
    if (!session?.apiToken || !permissions?.canView) return;

    if (!activeLimit) return;

    fetch(
      `${process.env.NEXT_PUBLIC_API_URL}/api/billing/limits/${activeLimit.type}?amount=${activeLimit.amount || 1}`,
      { headers: { Authorization: `Bearer ${session.apiToken}` } }
    )
      .then(async (res) => {
        if (res.status === 402) {
          const data = await res.json();
          setLimitStatus({ allowed: false, ...data });
          return;
        }
        if (!res.ok) throw new Error("Failed limit check");
        return res.json();
      })
      .then((data) => {
        if (data) setLimitStatus(data);
      })
      .catch(() => setLimitStatus({ allowed: true }));
  }, [activeLimit, moduleKey, session?.apiToken, permissions?.canView]);

  if (status === "loading" || permissions == null) return null;

  if (!permissions.canView) return null;

  if (activeLimit && limitStatus !== null && !limitStatus.allowed) {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-center min-h-[400px] bg-surface/50 border-2 border-dashed border-danger/30 rounded-2xl">
        <div className="w-16 h-16 rounded-full bg-danger/10 flex items-center justify-center mb-6">
          <Lock className="w-8 h-8 text-danger" />
        </div>
        <h2 className="text-2xl font-semibold mb-2 text-white">Limit Exceeded</h2>
        <p className="text-muted max-w-md mx-auto mb-2">
          You&apos;ve reached your {limitStatus.exceededLimit} limit.
        </p>
        <p className="text-sm text-muted mb-6">
          {limitStatus.currentUsage} / {limitStatus.limit} used
        </p>
        <Link 
          href={limitStatus.upgradeUrl || "/billing"} 
          className="px-6 py-3 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg shadow-[0_4px_12px_var(--accent-glow)] transition-all active:scale-[0.98]"
        >
          Upgrade to Continue
        </Link>
      </div>
    );
  }

  if (permissions.canUse) return <>{children}</>;

  return (
    <div className="flex flex-col items-center justify-center p-12 text-center min-h-[400px] bg-surface/50 border-2 border-dashed border-white/10 rounded-2xl">
      <div className="w-16 h-16 rounded-full bg-surface-hover flex items-center justify-center mb-6">
        <Lock className="w-8 h-8 text-primary" />
      </div>
      <h2 className="text-2xl font-semibold mb-2 text-white">Upgrade Required</h2>
      <p className="text-muted max-w-md mx-auto mb-8">
        {fallbackMessage || "Your role can view this module but cannot execute actions inside it."}
      </p>
      <Link 
        href="/billing" 
        className="px-6 py-3 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg shadow-[0_4px_12px_var(--accent-glow)] transition-all active:scale-[0.98]"
      >
        View Billing & Access
      </Link>
    </div>
  );
}
