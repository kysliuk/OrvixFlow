"use client";

import { useSession } from "next-auth/react";
import { Lock } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

import { getModuleGateState, shouldFetchCompanyScopedData } from "@/lib/dashboard-access";

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
  const [permissionsState, setPermissionsState] = useState<{ key: string; value: PermissionData } | null>(null);
  const [limitStatusState, setLimitStatusState] = useState<{ key: string; value: LimitData } | null>(null);
  const limitTypes = requiredLimits || [{ type: MODULE_LIMIT_TYPES[moduleKey] || "ai-tokens" }];
  const activeLimit = limitTypes.find((limit) => limit.type);
  const apiToken = session?.apiToken;
  const activeCompanyId = session?.user?.activeCompanyId ?? null;
  const canFetchCompanyScopedData = shouldFetchCompanyScopedData(apiToken, activeCompanyId);
  const permissionKey = `${activeCompanyId ?? "no-org"}:${moduleKey}`;
  const limitKey = `${permissionKey}:${activeLimit?.type ?? "no-limit"}:${activeLimit?.amount ?? 1}`;
  const permissions = permissionsState?.key === permissionKey ? permissionsState.value : null;
  const limitStatus = limitStatusState?.key === limitKey ? limitStatusState.value : null;

  useEffect(() => {
    if (!canFetchCompanyScopedData) {
      return;
    }

    fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/modules/${moduleKey}/permissions`, {
      headers: { Authorization: `Bearer ${apiToken}` },
    })
      .then(async (res) => {
        if (res.status === 404) {
          return { canView: false, canUse: false } satisfies PermissionData;
        }
        if (!res.ok) throw new Error("Failed permissions fetch");
        const data = await res.json();
        return { canView: !!data.canView, canUse: !!data.canUse } satisfies PermissionData;
      })
      .then((data) => {
        setPermissionsState({ key: permissionKey, value: data });
      })
      .catch(() => setPermissionsState({ key: permissionKey, value: { canView: false, canUse: false } }));
  }, [apiToken, canFetchCompanyScopedData, moduleKey, permissionKey]);

  useEffect(() => {
    if (!canFetchCompanyScopedData || !permissions?.canView || !activeLimit) {
      return;
    }

    fetch(
      `${process.env.NEXT_PUBLIC_API_URL}/api/billing/limits/${activeLimit.type}?amount=${activeLimit.amount || 1}`,
      { headers: { Authorization: `Bearer ${apiToken}` } }
    )
      .then(async (res) => {
        if (res.status === 402) {
          const data = await res.json();
          return { allowed: false, ...data } satisfies LimitData;
        }
        if (!res.ok) throw new Error("Failed limit check");
        return (await res.json()) as LimitData;
      })
      .then((data) => {
        setLimitStatusState({ key: limitKey, value: data });
      })
      .catch(() => setLimitStatusState({ key: limitKey, value: { allowed: true } }));
  }, [activeLimit, apiToken, canFetchCompanyScopedData, limitKey, permissions?.canView]);

  const gateState = getModuleGateState({
    status,
    activeCompanyId,
    permissions,
    limitStatus,
    hasActiveLimit: !!activeLimit,
  });

  if (gateState.kind === "loading") return null;

  if (gateState.kind === "no-org") {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-center min-h-[400px] bg-surface/50 border-2 border-dashed border-white/10 rounded-2xl">
        <div className="w-16 h-16 rounded-full bg-surface-hover flex items-center justify-center mb-6">
          <Lock className="w-8 h-8 text-primary" />
        </div>
        <h2 className="text-2xl font-semibold mb-2 text-white">{gateState.title}</h2>
        <p className="text-muted max-w-md mx-auto mb-8">{gateState.description}</p>
        <Link
          href={gateState.ctaHref}
          className="px-6 py-3 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg shadow-[0_4px_12px_var(--accent-glow)] transition-all active:scale-[0.98]"
        >
          {gateState.ctaLabel}
        </Link>
      </div>
    );
  }

  if (gateState.kind === "hidden") return null;

  if (gateState.kind === "limit-exceeded") {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-center min-h-[400px] bg-surface/50 border-2 border-dashed border-danger/30 rounded-2xl">
        <div className="w-16 h-16 rounded-full bg-danger/10 flex items-center justify-center mb-6">
          <Lock className="w-8 h-8 text-danger" />
        </div>
        <h2 className="text-2xl font-semibold mb-2 text-white">Limit Exceeded</h2>
        <p className="text-muted max-w-md mx-auto mb-2">
          You&apos;ve reached your {gateState.exceededLimit} limit.
        </p>
        <p className="text-sm text-muted mb-6">
          {gateState.currentUsage} / {gateState.limit} used
        </p>
        <Link
          href={gateState.upgradeUrl || "/billing"}
          className="px-6 py-3 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg shadow-[0_4px_12px_var(--accent-glow)] transition-all active:scale-[0.98]"
        >
          Upgrade to Continue
        </Link>
      </div>
    );
  }

  if (gateState.kind === "allowed") return <>{children}</>;

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
