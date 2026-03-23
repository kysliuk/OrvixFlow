"use client";

import { useSession } from "next-auth/react";
import { Lock } from "lucide-react";
import Link from "next/link";

interface ModuleGateProps {
  requiredPlan: "Free" | "Starter" | "Pro" | "Enterprise";
  children: React.ReactNode;
  fallbackMessage?: string;
}

const planHierarchy = { "Free": 0, "Starter": 1, "Pro": 2, "Enterprise": 3 };

export function ModuleGate({ requiredPlan, children, fallbackMessage }: ModuleGateProps) {
  const { data: session, status } = useSession();

  if (status === "loading") return null;

  const userPlan = (session?.user?.plan as string) || "Free";
  
  const hasAccess = planHierarchy[userPlan as keyof typeof planHierarchy] >= planHierarchy[requiredPlan];

  if (hasAccess) return <>{children}</>;

  return (
    <div className="flex flex-col items-center justify-center p-12 text-center min-h-[400px] bg-surface/50 border-2 border-dashed border-white/10 rounded-2xl">
      <div className="w-16 h-16 rounded-full bg-surface-hover flex items-center justify-center mb-6">
        <Lock className="w-8 h-8 text-primary" />
      </div>
      <h2 className="text-2xl font-semibold mb-2 text-white">Upgrade Required</h2>
      <p className="text-muted max-w-md mx-auto mb-8">
        {fallbackMessage || `This module requires the ${requiredPlan} plan or higher.`}
      </p>
      <Link 
        href="/billing" 
        className="px-6 py-3 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg shadow-[0_4px_12px_var(--accent-glow)] transition-all active:scale-[0.98]"
      >
        Upgrade to {requiredPlan}
      </Link>
    </div>
  );
}
