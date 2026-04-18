"use client";

import { signIn } from "next-auth/react";
import { useEffect, useState, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { TerminalSquare, AlertTriangle } from "lucide-react";
import { useRouter } from "next/navigation";
import Link from "next/link";

function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const errorParam = searchParams.get("error");
    if (errorParam) {
      if (errorParam === "Configuration") {
        setError("Identity services are temporarily misconfigured. Please contact support.");
      } else if (errorParam === "OAuthAccountNotLinked") {
        setError("Your email matches an existing local account. Please sign in with password first.");
      } else if (errorParam === "CredentialsSignin") {
        // Generic fallback when no specific code was set
        setError("Invalid email or password.");
      } else {
        // CustomAuthError passes backend message as the error code directly in URL
        setError(errorParam);
      }
    }
  }, [searchParams]);

  const handleCredentialsLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");
    try {
      const res = await signIn("credentials", {
        redirect: false,
        email,
        password,
      });
      if (res?.error) {
        // In NextAuth 5 with CustomAuthError, res.code holds the backend-specific message.
        // res.error is always "CredentialsSignin" (the class type).
        // res.code is the specific message we set (e.g. "Please verify your email...").
        const specificMessage = res?.error;
        if (specificMessage && specificMessage !== "CredentialsSignin" && specificMessage !== "null") {
          setError(specificMessage);
        } else {
          setError("Invalid email or password.");
        }
      } else if (res?.ok) {
        router.push("/");
      }
    } catch (err) {
      setError("An unexpected error occurred");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10">
      <div className="flex flex-col items-center text-center mb-8">
        <div className="w-12 h-12 rounded-xl bg-primary/20 flex items-center justify-center mb-4 border border-primary/30 shadow-[0_0_20px_var(--accent-glow)]">
          <TerminalSquare className="w-6 h-6 text-primary" />
        </div>
        <h1 className="text-2xl font-bold tracking-tight text-white mb-2">Welcome to OrvixFlow</h1>
        <p className="text-sm text-muted">Sign in to your enterprise orchestration dashboard</p>
      </div>

      {error && (
        <div className="mb-6 p-3 rounded-lg bg-danger/10 border border-danger/20 text-danger text-sm flex items-center gap-3">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      <form onSubmit={handleCredentialsLogin} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="email" className="text-xs font-medium text-muted ml-0.5">Work Email</label>
          <input 
            type="email" 
            id="email" 
            placeholder="you@company.com" 
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
            required 
          />
        </div>
        
        <div className="flex flex-col gap-1.5 mb-2">
          <div className="flex items-center justify-between ml-0.5">
            <label htmlFor="password" className="text-xs font-medium text-muted">Password</label>
            <a href="#" className="text-xs text-primary hover:text-white transition-colors">Forgot password?</a>
          </div>
          <input 
            type="password" 
            id="password" 
            placeholder="••••••••" 
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
            required 
          />
        </div>

        <button 
          type="submit" 
          disabled={isLoading}
          className="w-full bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-4 py-3 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isLoading ? "Signing in..." : "Sign in securely"}
        </button>
      </form>

      <div className="flex items-center gap-4 my-6">
        <div className="flex-1 h-px bg-white/10" />
        <span className="text-xs text-muted font-medium uppercase tracking-wider">Or continue with</span>
        <div className="flex-1 h-px bg-white/10" />
      </div>

      <div className="flex flex-col gap-3">
        <button 
          type="button" 
          onClick={() => signIn("google", { callbackUrl: "/" })}
          className="w-full flex items-center justify-center gap-3 bg-surface-hover hover:bg-white/5 border border-white/10 text-white text-sm font-medium rounded-lg px-4 py-3 transition-colors"
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
            <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
            <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
            <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
          </svg>
          Sign in with Google
        </button>
        
        <button 
          type="button" 
          onClick={() => signIn("microsoft-entra-id", { callbackUrl: "/" })}
          className="w-full flex items-center justify-center gap-3 bg-surface-hover hover:bg-white/5 border border-white/10 text-white text-sm font-medium rounded-lg px-4 py-3 transition-colors"
        >
          <svg width="18" height="18" viewBox="0 0 21 21" xmlns="http://www.w3.org/2000/svg">
            <rect x="1" y="1" width="9" height="9" fill="#f25022"/>
            <rect x="11" y="1" width="9" height="9" fill="#7fba00"/>
            <rect x="1" y="11" width="9" height="9" fill="#00a4ef"/>
            <rect x="11" y="11" width="9" height="9" fill="#ffb900"/>
          </svg>
          Sign in with Microsoft
        </button>
      </div>

      <p className="mt-8 text-center text-xs text-muted">
        Don't have a tenant yet?{" "}
        <Link href="/register" className="text-primary hover:text-white transition-colors font-medium">Create one here</Link>
      </p>
    </div>
  );
}

export default function LoginPage() {
  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0a0710] relative overflow-hidden p-4">
      {/* Dynamic Background Glow */}
      <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-primary/10 rounded-full blur-[120px] -translate-y-1/2 translate-x-1/3 pointer-events-none" />
      <div className="absolute bottom-0 left-0 w-[600px] h-[600px] bg-blue-500/10 rounded-full blur-[120px] translate-y-1/3 -translate-x-1/3 pointer-events-none" />

      <Suspense fallback={
        <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10 flex flex-col items-center">
          <div className="animate-spin rounded-full h-8 w-8 border-t-2 border-primary"></div>
          <p className="mt-4 text-white font-medium">Loading Identity Module...</p>
        </div>
      }>
        <LoginForm />
      </Suspense>
    </div>
  );
}
