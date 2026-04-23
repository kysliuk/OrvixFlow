/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
"use client";

import { useSession, signOut } from "next-auth/react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import {
  Home,
  Inbox,
  Database,
  Settings,
  CreditCard,
  LogOut,
  TerminalSquare,
  Search,
  ChevronDown,
  Bell
} from "lucide-react";

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { data: session } = useSession();
  const pathname = usePathname();

  const [visibleModules, setVisibleModules] = useState<string[]>([]);
  const [modulesLoaded, setModulesLoaded] = useState(false);
  const [isLoggingOutAll, setIsLoggingOutAll] = useState(false);

  useEffect(() => {
    if (!session) {
      setModulesLoaded(true);
      return;
    }

    const token = (session as any)?.apiToken;
    if (!token) {
      setModulesLoaded(true);
      return;
    }

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000);

    fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/modules/visible`, {
      headers: { "Authorization": `Bearer ${token}` },
      signal: controller.signal
    })
      .then(res => res.json())
      .then(data => {
        setVisibleModules(data.modules || []);
        setModulesLoaded(true);
      })
      .catch(err => {
        console.error("Failed to load visible modules", err);
        setModulesLoaded(true);
      })
      .finally(() => clearTimeout(timeoutId));
  }, [session]);

  const allLinks = [
    { name: "Dashboard", href: "/", icon: Home, moduleKey: null },
    { name: "Inbox Guardian", href: "/inbox", icon: Inbox, moduleKey: "inbox-guardian" },
    { name: "Knowledge Base", href: "/knowledge", icon: Database, moduleKey: "knowledge-base" },
    { name: "Settings", href: "/settings", icon: Settings, moduleKey: null },
    { name: "Billing", href: "/billing", icon: CreditCard, moduleKey: null },
  ];

  const links = allLinks.filter(link =>
    !link.moduleKey || visibleModules.includes(link.moduleKey)
  );

  const getBreadcrumb = () => {
    if (pathname === "/") return "Overview";
    const path = pathname.split("/")[1];
    return path.charAt(0).toUpperCase() + path.slice(1).replace("-", " ");
  };

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
    <div className="flex min-h-screen bg-background text-white font-sans overflow-hidden">
      {/* Fixed Sidebar */}
      <aside className="w-64 bg-surface border-r border-white/5 flex flex-col shrink-0 relative z-20">
        <div className="h-16 flex items-center px-6 border-b border-white/5">
          <Link href="/" className="flex items-center gap-2 group">
            <div className="w-8 h-8 rounded-lg bg-primary/20 flex items-center justify-center group-hover:bg-primary/30 transition-colors">
              <TerminalSquare className="w-5 h-5 text-primary" />
            </div>
            <span className="font-semibold text-lg tracking-tight hover:text-white transition-colors">OrvixFlow</span>
          </Link>
        </div>

        <div className="flex-1 py-6 px-3 flex flex-col gap-1 overflow-y-auto">
          <div className="text-xs font-semibold text-muted uppercase tracking-wider mb-2 px-3">Main Navigation</div>
          {!modulesLoaded ? (
            <div className="px-3 py-2 text-sm text-muted animate-pulse">Loading modules...</div>
          ) : (
            links.map((link) => {
              const Icon = link.icon;
              const isActive = pathname === link.href;
              return (
                <Link
                  key={link.href}
                  href={link.href}
                  className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all ${isActive
                      ? "bg-primary/15 text-primary shadow-[inset_2px_0_0_var(--accent-primary)]"
                      : "text-muted hover:text-white hover:bg-white/5"
                    }`}
                >
                  <Icon className={`w-4 h-4 ${isActive ? "text-primary" : "text-muted"}`} />
                  {link.name}
                </Link>
              );
            })
          )}
        </div>

        <div className="p-4 border-t border-white/5 bg-surface-hover/30">
          <div className="flex items-center gap-3 mb-4">
            <div className="w-9 h-9 rounded-full bg-gradient-to-tr from-primary to-danger/80 flex items-center justify-center shrink-0 shadow-lg">
              <span className="font-bold text-sm">{session?.user?.name?.charAt(0) || "U"}</span>
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-sm font-medium truncate">{session?.user?.name || "Agent User"}</div>
              <div className="text-xs text-muted truncate">{session?.user?.email}</div>
            </div>
          </div>
          <div className="flex flex-col gap-2">
            <button
              className="w-full flex items-center gap-2 px-3 py-2 rounded-md text-xs font-medium text-muted hover:text-danger hover:bg-danger/10 transition-colors"
              onClick={() => signOut({ callbackUrl: "/login" })}
            >
              <LogOut className="w-4 h-4" />
              Sign Out
            </button>
            <button
              className="w-full flex items-center gap-2 px-3 py-2 rounded-md text-xs font-medium text-muted hover:text-white hover:bg-white/5 transition-colors disabled:opacity-50"
              onClick={handleLogoutAll}
              disabled={isLoggingOutAll}
            >
              <LogOut className="w-4 h-4" />
              {isLoggingOutAll ? "Ending All Sessions..." : "Sign Out All Sessions"}
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Pane */}
      <main className="flex-1 flex flex-col min-w-0 bg-[#0a0710] relative">

        {/* Subtle background glow effect for depth */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-full max-w-2xl h-[300px] bg-primary/5 blur-[120px] pointer-events-none rounded-full" />

        {/* Topbar */}
        <header className="h-16 shrink-0 border-b border-white/5 flex items-center justify-between px-8 relative z-10 backdrop-blur-md bg-background/80">

          <div className="flex items-center gap-4 text-sm">
            <div className="flex items-center gap-2 text-muted">
              <TerminalSquare className="w-4 h-4" />
              <span>OrvixFlow</span>
              <span className="text-white/20">/</span>
              <span className="text-white font-medium">{getBreadcrumb()}</span>
            </div>
            <div className="h-4 w-px bg-white/10 mx-2" />
            <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold tracking-widest uppercase border ${session?.user?.plan === "Free" ? "border-white/10 text-muted" : "border-primary/30 text-primary bg-primary/10"
              }`}>
              {session?.user?.plan || "Free"} Plan
            </span>
          </div>

          <div className="flex items-center gap-6">
            <div className="relative group">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted group-focus-within:text-primary transition-colors" />
              <input
                type="text"
                placeholder="Search resources... (Cmd+K)"
                className="w-64 bg-surface border border-white/10 rounded-full py-1.5 pl-9 pr-4 text-sm text-white placeholder:text-muted focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
              />
            </div>
            <button className="relative text-muted hover:text-white transition-colors">
              <Bell className="w-5 h-5" />
              <span className="absolute top-0 right-0 w-2 h-2 bg-danger rounded-full border-2 border-background" />
            </button>
          </div>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-y-auto">
          <div className="max-w-[1600px] mx-auto p-8 relative z-10">
            {children}
          </div>
        </div>
      </main>
    </div>
  );
}
