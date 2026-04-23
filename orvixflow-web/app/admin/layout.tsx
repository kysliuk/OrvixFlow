/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
"use client";

import { useSession, signOut } from "next-auth/react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { ShieldAlert, Users, LayoutDashboard, Database, Activity, LogOut, ArrowLeft, CreditCard, Building, Mail, Key } from "lucide-react";
import { useEffect, useState } from "react";

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  const { data: session, status } = useSession();
  const pathname = usePathname();
  const router = useRouter();
  const [isLoggingOutAll, setIsLoggingOutAll] = useState(false);

  const role = (session?.user as any)?.globalRole || session?.user?.role;
  const isSuperAdmin = role === "SuperAdmin" || role === "InternalOperator";

  useEffect(() => {
    // Only redirect if we KNOW the user is fully loaded and still not an admin
    if (status === "unauthenticated") {
      router.push("/");
    }
  }, [status, router]);

  // Show loading state while session resolves — never redirect during this phase
  if (status === "loading") {
    return <div className="min-h-screen flex items-center justify-center text-danger font-mono text-sm animate-pulse">VERIFYING GOD MODE IDENT...</div>;
  }

  // Show lock screen if session loaded but not admin
  if (status === "authenticated" && !isSuperAdmin) {
    return <div className="min-h-screen flex items-center justify-center text-danger font-mono text-sm">ACCESS DENIED // INSUFFICIENT CLEARANCE</div>;
  }

  const navItems = [
    { href: "/admin", label: "Global Platform", icon: LayoutDashboard },
    { href: "/admin/plans", label: "Plan Templates", icon: CreditCard },
    { href: "/admin/tenants", label: "Companies", icon: Building },
    { href: "/admin/modules", label: "Modules", icon: Key },
    { href: "/admin/inbox-metrics", label: "Inbox Metrics", icon: Mail },
    { href: "/admin/test", label: "Inbox Simulator", icon: ShieldAlert },
    { href: "/admin/logs", label: "Kernel Trace Logs", icon: Activity },
    { href: "/admin/vector-db", label: "Raw pgvector", icon: Database },
  ];

  const handleLogoutAll = async () => {
    const token = (session as any)?.apiToken;
    if (!token || isLoggingOutAll) return;

    setIsLoggingOutAll(true);
    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080"}/api/auth/logout-all`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
      });

      if (!res.ok) {
        console.error("Failed to revoke all sessions", await res.text());
      }
    } catch (error) {
      console.error("Failed to revoke all sessions", error);
    } finally {
      await signOut({ callbackUrl: "/login" });
    }
  };

  return (
    <div className="flex h-screen bg-[#0a0505] text-white overflow-hidden selection:bg-danger/30">
      
      {/* Admin Sidebar (Deep Red Theme) */}
      <aside className="w-64 border-r border-danger/20 bg-black/50 backdrop-blur-xl flex flex-col shrink-0 relative z-20">
        <div className="h-16 flex items-center px-6 border-b border-danger/20">
          <Link href="/admin" className="flex items-center gap-2 text-danger hover:text-danger/80 transition-colors">
            <ShieldAlert className="w-6 h-6" />
            <span className="font-semibold tracking-tight text-lg">Orvix<span className="opacity-60">Admin</span></span>
          </Link>
        </div>

        <div className="p-4 flex-1 flex flex-col gap-1 overflow-y-auto">
          <div className="text-[10px] font-bold uppercase tracking-widest text-danger/60 mb-2 px-3 mt-2">God Mode</div>
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all ${
                  isActive 
                    ? "bg-danger/15 text-danger border border-danger/30 shadow-[0_0_15px_rgba(244,63,94,0.15)]" 
                    : "text-white/60 hover:text-white hover:bg-danger/5 border border-transparent"
                }`}
              >
                <Icon className={`w-4 h-4 ${isActive ? "text-danger" : "opacity-70"}`} />
                {item.label}
              </Link>
            )
          })}

          <div className="mt-8">
            <Link 
              href="/"
              className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-white/50 hover:text-white hover:bg-white/5 transition-all"
            >
              <ArrowLeft className="w-4 h-4 opacity-70" />
              Exit to Standard UI
            </Link>
          </div>
        </div>

        <div className="p-4 border-t border-danger/20 flex flex-col gap-2">
          <div className="bg-danger/10 border border-danger/20 rounded-lg p-3 text-xs">
            <div className="font-semibold text-danger mb-1">Active Session</div>
            <div className="text-white/80 font-mono truncate">{session?.user?.email}</div>
            <div className="text-danger/70 mt-1 uppercase font-bold tracking-widest text-[9px]">{session?.user?.role}</div>
          </div>
          <button 
            onClick={() => signOut({ callbackUrl: "/login" })}
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-danger/80 hover:text-danger hover:bg-danger/10 rounded-lg transition-colors w-full"
          >
            <LogOut className="w-4 h-4" /> Terminate Session
          </button>
          <button 
            onClick={handleLogoutAll}
            disabled={isLoggingOutAll}
            className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-white/70 hover:text-white hover:bg-white/5 rounded-lg transition-colors w-full disabled:opacity-50"
          >
            <LogOut className="w-4 h-4" /> {isLoggingOutAll ? "Ending All Sessions..." : "Terminate All Sessions"}
          </button>
        </div>
      </aside>

      {/* Admin Content Area */}
      <div className="flex-1 flex flex-col min-w-0 relative">
        <div className="absolute top-0 right-0 w-[800px] h-[500px] bg-danger/5 blur-[120px] rounded-full pointer-events-none -translate-y-1/2 translate-x-1/4" />
        
        <header className="h-16 flex items-center justify-between px-8 border-b border-danger/10 bg-black/50 backdrop-blur-md sticky top-0 z-10 shrink-0">
          <div className="flex items-center gap-2 text-sm text-danger/70 font-mono">
            SUPER_ADMIN_MODE // GLOBAL_OVERRIDE_ENABLED
          </div>
        </header>

        <main className="flex-1 overflow-y-auto p-8 relative z-0 custom-scrollbar">
          {children}
        </main>
      </div>

    </div>
  );
}
