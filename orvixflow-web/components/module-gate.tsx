"use client";

import { useSession } from "next-auth/react";
import { Lock } from "lucide-react";
import Link from "next/link";
import { useEffect, useState } from "react";

interface ModuleGateProps {
  moduleKey: string;
  children: React.ReactNode;
  fallbackMessage?: string;
}

export function ModuleGate({ moduleKey, children, fallbackMessage }: ModuleGateProps) {
  const { data: session, status } = useSession();
  const [permissions, setPermissions] = useState<{ canView: boolean; canUse: boolean } | null>(null);

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

  if (status === "loading" || permissions == null) return null;

  if (!permissions.canView) return null;

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
