"use client";

import { useSession } from "next-auth/react";
import { User, Shield, Key, Building, BellRing } from "lucide-react";
import { useState } from "react";

export default function SettingsPage() {
  const { data: session } = useSession();
  const [activeTab, setActiveTab] = useState("profile");

  const tabs = [
    { id: "profile", label: "Profile", icon: User },
    { id: "organization", label: "Organization", icon: Building },
    { id: "security", label: "Security", icon: Shield },
    { id: "api-keys", label: "API Keys", icon: Key },
    { id: "notifications", label: "Notifications", icon: BellRing },
  ];

  return (
    <div className="flex flex-col gap-6 max-w-5xl h-full">
      
      <div>
        <h1 className="text-2xl font-semibold mb-1">Settings</h1>
        <p className="text-sm text-muted">Manage your account preferences and tenant configurations.</p>
      </div>

      <div className="flex flex-col md:flex-row gap-8 mt-2 h-[600px]">
        
        {/* Vertical Tabs Navigation */}
        <div className="w-full md:w-56 flex flex-col gap-1 shrink-0">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all text-left ${
                  isActive 
                    ? "bg-primary text-white shadow-[0_2px_10px_var(--accent-glow)]" 
                    : "text-muted hover:text-white hover:bg-surface"
                }`}
              >
                <Icon className={`w-4 h-4 ${isActive ? "text-white" : "text-muted"}`} />
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Tab Content Area */}
        <div className="flex-1 bg-surface border border-white/5 rounded-xl p-8 relative overflow-hidden shadow-lg h-full overflow-y-auto">
          
          {activeTab === "profile" && (
            <div className="animate-in fade-in duration-300">
              <h2 className="text-lg font-semibold mb-6">Personal Information</h2>
              
              <div className="flex flex-col gap-6">
                <div className="flex items-center gap-6">
                  <div className="w-20 h-20 rounded-full bg-gradient-to-tr from-primary to-danger flex items-center justify-center text-3xl font-bold shadow-lg">
                    {session?.user?.name?.charAt(0) || "U"}
                  </div>
                  <button className="px-4 py-2 border border-white/10 hover:bg-white/5 rounded-lg text-sm font-medium transition-colors">
                    Upload Avatar
                  </button>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-medium text-muted">Full Name</label>
                    <input 
                      type="text" 
                      defaultValue={session?.user?.name || ""}
                      className="bg-background border border-white/10 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50"
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className="text-xs font-medium text-muted">Email Address</label>
                    <input 
                      type="email" 
                      defaultValue={session?.user?.email || ""}
                      disabled
                      className="bg-white/5 border border-white/5 rounded-lg px-3 py-2 text-sm text-muted cursor-not-allowed"
                    />
                  </div>
                </div>

                <div className="flex flex-col gap-1.5 pt-4">
                  <label className="text-xs font-medium text-muted">Global Role</label>
                  <div className="bg-white/5 border border-white/5 rounded-lg px-3 py-2 text-sm text-white w-fit">
                    {(session?.user as any)?.role || "Owner"}
                  </div>
                </div>

                <div className="border-t border-white/5 pt-6 mt-4">
                  <button className="px-5 py-2.5 bg-primary hover:bg-primary/90 text-white font-medium rounded-lg text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all">
                    Save Changes
                  </button>
                </div>
              </div>
            </div>
          )}

          {activeTab === "api-keys" && (
            <div className="animate-in fade-in duration-300">
              <h2 className="text-lg font-semibold mb-1">API Keys</h2>
              <p className="text-sm text-muted mb-6">Use these keys to authenticate your webhooks and n8n nodes.</p>
              
              <div className="bg-background border border-white/5 rounded-lg p-5 mb-6">
                <div className="flex items-center justify-between mb-2">
                  <span className="text-sm font-medium">Production Key</span>
                  <span className="text-xs text-muted">Created Oct 10, 2026</span>
                </div>
                <div className="flex gap-3">
                  <input 
                    type="password" 
                    value="sk_live_12345abcdefghijklmnopqrstuvwxyz" 
                    readOnly
                    className="flex-1 bg-surface border border-white/10 rounded-lg px-3 py-2 text-sm text-muted font-mono"
                  />
                  <button className="px-4 py-2 border border-white/10 hover:bg-white/5 rounded-lg text-sm font-medium transition-colors">
                    Reveal
                  </button>
                </div>
              </div>

              <button className="px-4 py-2 bg-white/5 border border-white/10 hover:bg-white/10 text-white font-medium rounded-lg text-sm transition-all">
                Generate New Key
              </button>
            </div>
          )}

          {/* Placeholder for other tabs using skeletons */}
          {activeTab !== "profile" && activeTab !== "api-keys" && (
            <div className="animate-in fade-in duration-300">
              <div className="h-6 w-48 bg-white/5 rounded animate-pulse mb-6" />
              <div className="flex flex-col gap-4">
                <div className="h-10 w-full bg-white/5 rounded animate-pulse" />
                <div className="h-10 w-3/4 bg-white/5 rounded animate-pulse" />
                <div className="h-10 w-full bg-white/5 rounded animate-pulse" />
              </div>
            </div>
          )}

        </div>
      </div>
    </div>
  );
}
