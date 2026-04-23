/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import { Suspense, useMemo, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { signIn } from "next-auth/react";
import { AlertTriangle, MailCheck } from "lucide-react";

function InviteAcceptForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const hasToken = useMemo(() => token.trim().length > 0, [token]);

  const handleAcceptInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!hasToken) {
      setError("Invitation token is missing or invalid.");
      return;
    }

    setIsLoading(true);
    setError("");

    try {
      const res = await signIn("invite-accept", {
        redirect: false,
        token,
        displayName,
        password,
      });

      if (res?.error) {
        const specificMessage = (res as any).code;
        setError(specificMessage && specificMessage !== "CredentialsSignin" ? specificMessage : "Invitation could not be accepted.");
      } else if (res?.ok) {
        router.push("/");
      }
    } catch {
      setError("An unexpected error occurred while accepting the invitation.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10">
      <div className="flex flex-col items-center text-center mb-8">
        <div className="w-12 h-12 rounded-xl bg-primary/20 flex items-center justify-center mb-4 border border-primary/30 shadow-[0_0_20px_var(--accent-glow)]">
          <MailCheck className="w-6 h-6 text-primary" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-white mb-2">Accept invitation</h1>
        <p className="text-sm text-muted">Create your local account details if needed and join the workspace.</p>
      </div>

      {!hasToken && (
        <div className="mb-6 p-3 rounded-lg bg-danger/10 border border-danger/20 text-danger text-sm flex items-center gap-3">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <span>Invitation token is missing. Open the full invitation link from your email.</span>
        </div>
      )}

      {error && (
        <div className="mb-6 p-3 rounded-lg bg-danger/10 border border-danger/20 text-danger text-sm flex items-center gap-3">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleAcceptInvite} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="displayName" className="text-xs font-medium text-muted ml-0.5">Display name</label>
          <input
            id="displayName"
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            placeholder="Your name"
            className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
          />
        </div>

        <div className="flex flex-col gap-1.5 mb-2">
          <label htmlFor="password" className="text-xs font-medium text-muted ml-0.5">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="Create a password if you are new here"
            className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
          />
          <p className="text-xs text-muted">New local accounts must set a strong password. Existing accounts can usually leave this blank.</p>
        </div>

        <button
          type="submit"
          disabled={isLoading || !hasToken}
          className="w-full bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-4 py-3 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoading ? "Accepting invitation..." : "Accept invitation"}
        </button>
      </form>
    </div>
  );
}

export default function InvitePage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0a0710] relative overflow-hidden p-4">
      <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-primary/10 rounded-full blur-[120px] -translate-y-1/2 translate-x-1/3 pointer-events-none" />
      <div className="absolute bottom-0 left-0 w-[600px] h-[600px] bg-blue-500/10 rounded-full blur-[120px] translate-y-1/3 -translate-x-1/3 pointer-events-none" />

      <Suspense fallback={<div className="text-white">Loading invitation...</div>}>
        <InviteAcceptForm />
      </Suspense>
    </div>
  );
}
