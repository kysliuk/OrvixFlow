/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { useSession } from "next-auth/react";
import { useEffect, useState } from "react";
import { BellRing, Key, User } from "lucide-react";

type SettingsTabId = "profile" | "api-keys" | "notifications";

const tabs: Array<{ id: SettingsTabId; label: string; icon: typeof User }> = [
  { id: "profile", label: "Profile", icon: User },
  { id: "api-keys", label: "API Keys", icon: Key },
  { id: "notifications", label: "Notifications", icon: BellRing },
];

export default function SettingsPage() {
  const { data: session, update } = useSession();
  const [activeTab, setActiveTab] = useState<SettingsTabId>("profile");

  const [displayName, setDisplayName] = useState("");
  const [savingProfile, setSavingProfile] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSuccess, setProfileSuccess] = useState(false);

  const apiToken = (session as any)?.apiToken;
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

  useEffect(() => {
    if (session?.user?.name) {
      setDisplayName(session.user.name as string);
    }
  }, [session]);

  const handleSaveProfile = async () => {
    if (!apiToken) return;

    setSavingProfile(true);
    setProfileError(null);
    setProfileSuccess(false);

    try {
      const response = await fetch(`${apiUrl}/api/auth/profile`, {
        method: "PUT",
        headers: { "Content-Type": "application/json", Authorization: `Bearer ${apiToken}` },
        body: JSON.stringify({ displayName }),
      });

      if (response.ok) {
        const data = await response.json();
        await update(data);
        setProfileSuccess(true);
        setTimeout(() => setProfileSuccess(false), 3000);
      } else {
        const error = await response.json();
        setProfileError(error.error || "Failed to save profile");
      }
    } catch {
      setProfileError("An unexpected error occurred");
    } finally {
      setSavingProfile(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-semibold text-white">Settings</h1>
        <p className="text-sm text-muted">Manage your account preferences and personal access settings.</p>
      </div>

      <div className="rounded-2xl border border-white/5 bg-surface p-4 shadow-lg sm:p-6">
        <div className="mb-6 flex flex-wrap gap-2 border-b border-white/10 pb-4">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;

            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`inline-flex items-center gap-2 rounded-xl border px-4 py-2 text-sm font-medium transition-all ${
                  isActive
                    ? "border-primary/30 bg-primary/15 text-primary"
                    : "border-white/10 text-muted hover:border-white/20 hover:bg-white/5 hover:text-white"
                }`}
              >
                <Icon className="h-4 w-4" />
                {tab.label}
              </button>
            );
          })}
        </div>

        {activeTab === "profile" && (
          <div className="animate-in fade-in duration-300">
            <h2 className="mb-6 text-lg font-semibold text-white">Personal Information</h2>

            <div className="flex flex-col gap-6">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:gap-6">
                <div className="flex h-20 w-20 items-center justify-center rounded-full bg-gradient-to-tr from-primary to-danger text-3xl font-bold shadow-lg">
                  {session?.user?.name?.charAt(0) || "U"}
                </div>
                <button className="w-fit rounded-lg border border-white/10 px-4 py-2 text-sm font-medium transition-colors hover:bg-white/5">
                  Upload Avatar
                </button>
              </div>

              <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-muted">Full Name</label>
                  <input
                    type="text"
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
                    className="rounded-lg border border-white/10 bg-background px-3 py-2 text-sm text-white focus:border-primary/50 focus:outline-none focus:ring-1 focus:ring-primary/50"
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <label className="text-xs font-medium text-muted">Email Address</label>
                  <input
                    type="email"
                    defaultValue={session?.user?.email || ""}
                    disabled
                    className="cursor-not-allowed rounded-lg border border-white/5 bg-white/5 px-3 py-2 text-sm text-muted"
                  />
                </div>
              </div>

              {(session?.user as any)?.globalRole && (
                <div className="flex flex-col gap-1.5 pt-2">
                  <label className="text-xs font-medium text-muted">Global Role</label>
                  <div className="w-fit rounded-lg border border-white/5 bg-white/5 px-3 py-2 text-sm font-semibold text-white">
                    {(session?.user as any)?.globalRole}
                  </div>
                </div>
              )}

              <div className="mt-2 border-t border-white/5 pt-6">
                {profileError && (
                  <div className="mb-4 rounded-lg border border-danger/20 bg-danger/10 px-4 py-2 text-sm text-danger">
                    {profileError}
                  </div>
                )}
                {profileSuccess && (
                  <div className="mb-4 rounded-lg border border-success/20 bg-success/10 px-4 py-2 text-sm text-success">
                    Profile updated successfully!
                  </div>
                )}
                <button
                  onClick={handleSaveProfile}
                  disabled={savingProfile}
                  className="rounded-lg bg-primary px-5 py-2.5 text-sm font-medium text-white shadow-[0_4px_14px_var(--accent-glow)] transition-all hover:bg-primary/90 disabled:opacity-50"
                >
                  {savingProfile ? "Saving..." : "Save Changes"}
                </button>
              </div>
            </div>
          </div>
        )}

        {activeTab === "api-keys" && (
          <div className="animate-in fade-in duration-300">
            <h2 className="mb-1 text-lg font-semibold text-white">API Keys</h2>
            <p className="mb-6 text-sm text-muted">Use these keys to authenticate your webhooks and n8n nodes.</p>

            <div className="mb-6 rounded-lg border border-white/5 bg-background p-5">
              <div className="mb-2 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <span className="text-sm font-medium text-white">Production Key</span>
                <span className="text-xs text-muted">Created Oct 10, 2026</span>
              </div>
              <div className="flex flex-col gap-3 sm:flex-row">
                <input
                  type="password"
                  value="sk_live_12345abcdefghijklmnopqrstuvwxyz"
                  readOnly
                  className="flex-1 rounded-lg border border-white/10 bg-surface px-3 py-2 font-mono text-sm text-muted"
                />
                <button className="rounded-lg border border-white/10 px-4 py-2 text-sm font-medium transition-colors hover:bg-white/5">
                  Reveal
                </button>
              </div>
            </div>

            <button className="rounded-lg border border-white/10 bg-white/5 px-4 py-2 text-sm font-medium text-white transition-all hover:bg-white/10">
              Generate New Key
            </button>
          </div>
        )}

        {activeTab === "notifications" && (
          <div className="animate-in fade-in duration-300">
            <h2 className="mb-1 text-lg font-semibold text-white">Notifications</h2>
            <p className="mb-6 text-sm text-muted">Notification preferences will appear here once delivery channels are connected.</p>

            <div className="flex flex-col gap-4">
              <div className="h-10 w-full animate-pulse rounded bg-white/5" />
              <div className="h-10 w-3/4 animate-pulse rounded bg-white/5" />
              <div className="h-10 w-full animate-pulse rounded bg-white/5" />
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
