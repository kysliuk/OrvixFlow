/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { CheckCircle2, XCircle, Loader2, MailCheck } from "lucide-react";

function MailboxCallbackContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const { data: session, status: sessionStatus } = useSession();

  const code = searchParams.get("code");
  const state = searchParams.get("state");
  const queryConnectionId = searchParams.get("connectionId");

  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [message, setMessage] = useState("");

  useEffect(() => {
    // Only proceed once search params are loaded and session status is determined
    if (sessionStatus === "loading") return;

    const connectionId = queryConnectionId || sessionStorage.getItem("oauth_connection_id");

    if (!code || !state) {
      setStatus("error");
      setMessage("Missing authorization code or verification state.");
      return;
    }

    if (!connectionId) {
      setStatus("error");
      setMessage("Mailbox connection ID is missing.");
      return;
    }

    const apiToken = (session as any)?.apiToken;
    const getHeaders = () => {
      const headers: Record<string, string> = { 
        "Content-Type": "application/json" 
      };
      if (apiToken) {
        headers["Authorization"] = `Bearer ${apiToken}`;
      }
      const imp = typeof window !== "undefined" ? localStorage.getItem("impersonateTenantId") : null;
      if (imp) {
        headers["X-Impersonate-Tenant"] = imp;
      }
      return headers;
    };

    const exchangeCallback = async () => {
      try {
        const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";
        const res = await fetch(`${apiUrl}/api/v1/inbox/connections/${connectionId}/credential/callback`, {
          method: "POST",
          headers: getHeaders(),
          body: JSON.stringify({ code, state }),
        });

        const data = await res.json().catch(() => ({}));

        if (res.ok) {
          setStatus("success");
          setMessage(data.message || "Mailbox connected and authorized successfully!");
          sessionStorage.removeItem("oauth_connection_id");

          // Automatically redirect to inbox settings after 3 seconds
          const timer = setTimeout(() => {
            router.push("/settings/inbox");
          }, 3000);
          return () => clearTimeout(timer);
        } else {
          setStatus("error");
          setMessage(data.message || data.error || "Failed to finalize OAuth authorization with backend.");
        }
      } catch (err) {
        setStatus("error");
        setMessage("An unexpected error occurred communication with backend.");
      }
    };

    exchangeCallback();
  }, [code, state, queryConnectionId, session, sessionStatus, router]);

  return (
    <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10 text-center">
      <div className="flex flex-col items-center mb-8">
        <div className="w-12 h-12 rounded-xl bg-primary/20 flex items-center justify-center mb-4 border border-primary/30 shadow-[0_0_20px_var(--accent-glow)]">
          <MailCheck className="w-6 h-6 text-primary" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-white mb-2">Mailbox Connection</h1>
        <p className="text-sm text-muted">Authorizing OAuth Integration</p>
      </div>

      <div className="py-6">
        {status === "loading" && (
          <div className="flex flex-col items-center gap-4">
            <Loader2 className="w-12 h-12 text-primary animate-spin" />
            <p className="text-white font-medium">Connecting credentials to mailbox...</p>
          </div>
        )}

        {status === "success" && (
          <div className="flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-success/20 flex items-center justify-center border border-success/30 shadow-[0_0_20px_rgba(34,197,94,0.2)]">
              <CheckCircle2 className="w-8 h-8 text-success" />
            </div>
            <p className="text-success font-bold text-lg">{message}</p>
            <p className="text-muted text-sm px-4">Your credentials are saved. Redirecting to Inbox Settings shortly...</p>
            <Link 
              href="/settings/inbox" 
              className="mt-6 w-full bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-4 py-3 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all active:scale-[0.98]"
            >
              Return to Settings
            </Link>
          </div>
        )}

        {status === "error" && (
          <div className="flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-danger/20 flex items-center justify-center border border-danger/30 shadow-[0_0_20px_rgba(244,63,94,0.2)]">
              <XCircle className="w-8 h-8 text-danger" />
            </div>
            <p className="text-danger font-bold text-lg">Connection Failed</p>
            <p className="text-muted text-sm px-4">{message}</p>
            <Link 
              href="/settings/inbox" 
              className="mt-6 w-full bg-white/5 hover:bg-white/10 text-white font-medium rounded-lg px-4 py-3 text-sm border border-white/10 transition-all font-medium"
            >
              Return to Settings
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}

export default function MailboxCallbackPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0a0710] relative overflow-hidden p-4 font-sans">
      {/* Background elements to match overall theme */}
      <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-primary/10 rounded-full blur-[120px] -translate-y-1/2 translate-x-1/3 pointer-events-none" />
      <div className="absolute top-1/2 left-0 w-[600px] h-[600px] bg-success/5 rounded-full blur-[120px] -translate-x-1/3 -translate-y-1/2 pointer-events-none" />
      
      <Suspense fallback={
        <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10 text-center flex flex-col items-center gap-4">
          <Loader2 className="w-12 h-12 text-primary animate-spin" />
          <p className="text-white font-medium">Loading Authorization Context...</p>
        </div>
      }>
        <MailboxCallbackContent />
      </Suspense>
    </div>
  );
}
