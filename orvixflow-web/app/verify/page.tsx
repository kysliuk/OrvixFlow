"use client";

import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { CheckCircle2, XCircle, Loader2, ShieldCheck } from "lucide-react";

function VerifyEmailContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [message, setMessage] = useState("");

  useEffect(() => {
    if (!token) {
      setStatus("error");
      setMessage("Verification token is missing.");
      return;
    }

    const verify = async () => {
      try {
        const res = await fetch(`/api/auth/verify`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ token }),
        });

        const data = await res.json();

        if (res.ok) {
          setStatus("success");
          setMessage(data.message || "Email verified successfully!");
        } else {
          setStatus("error");
          setMessage(data.error || "Verification failed. The link may be invalid or expired.");
        }
      } catch (err) {
        setStatus("error");
        setMessage("An unexpected error occurred. Please try again later.");
      }
    };

    verify();
  }, [token]);

  return (
    <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10 text-center">
      <div className="flex flex-col items-center mb-8">
        <div className="w-12 h-12 rounded-xl bg-primary/20 flex items-center justify-center mb-4 border border-primary/30 shadow-[0_0_20px_var(--accent-glow)]">
          <ShieldCheck className="w-6 h-6 text-primary" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-white mb-2">Email Verification</h1>
        <p className="text-sm text-muted">OrvixFlow Identity Service</p>
      </div>

      <div className="py-6">
        {status === "loading" && (
          <div className="flex flex-col items-center gap-4">
            <Loader2 className="w-12 h-12 text-primary animate-spin" />
            <p className="text-white font-medium">Validating your credentials...</p>
          </div>
        )}

        {status === "success" && (
          <div className="flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-success/20 flex items-center justify-center border border-success/30 shadow-[0_0_20px_rgba(34,197,94,0.2)]">
              <CheckCircle2 className="w-8 h-8 text-success" />
            </div>
            <p className="text-success font-bold text-lg">{message}</p>
            <p className="text-muted text-sm px-4">Your account is now fully activated. You can proceed to the dashboard.</p>
            <Link 
              href="/login" 
              className="mt-6 w-full bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-4 py-3 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all active:scale-[0.98]"
            >
              Sign In to Dashboard
            </Link>
          </div>
        )}

        {status === "error" && (
          <div className="flex flex-col items-center gap-4">
            <div className="w-16 h-16 rounded-full bg-danger/20 flex items-center justify-center border border-danger/30 shadow-[0_0_20px_rgba(244,63,94,0.2)]">
              <XCircle className="w-8 h-8 text-danger" />
            </div>
            <p className="text-danger font-bold text-lg">Verification Failed</p>
            <p className="text-muted text-sm px-4">{message}</p>
            <Link 
              href="/login" 
              className="mt-6 w-full bg-white/5 hover:bg-white/10 text-white font-medium rounded-lg px-4 py-3 text-sm border border-white/10 transition-all font-medium"
            >
              Return to Login
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}

export default function VerifyPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0a0710] relative overflow-hidden p-4 font-sans">
      {/* Background elements to match overall theme */}
      <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-primary/10 rounded-full blur-[120px] -translate-y-1/2 translate-x-1/3 pointer-events-none" />
      <div className="absolute top-1/2 left-0 w-[600px] h-[600px] bg-success/5 rounded-full blur-[120px] -translate-x-1/3 -translate-y-1/2 pointer-events-none" />
      
      <Suspense fallback={
        <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10 text-center flex flex-col items-center gap-4">
          <Loader2 className="w-12 h-12 text-primary animate-spin" />
          <p className="text-white font-medium">Loading Identity Context...</p>
        </div>
      }>
        <VerifyEmailContent />
      </Suspense>
    </div>
  );
}
