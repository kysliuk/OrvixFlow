"use client";

import { useState } from "react";
import { signIn } from "next-auth/react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { TerminalSquare } from "lucide-react";

export default function RegisterPage() {
  const router = useRouter();
  const [formData, setFormData] = useState({ email: "", password: "", displayName: "" });
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    try {
      const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/auth/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formData),
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.error || "Registration failed");
      }

      const signInRes = await signIn("credentials", {
        redirect: false,
        email: formData.email,
        password: formData.password,
      });

      if (signInRes?.error) {
        throw new Error("Login after registration failed");
      }

      router.push("/");
    } catch (err: any) {
      setError(err.message || "An unexpected error occurred");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#0a0710] relative overflow-hidden p-4">
      
      {/* Dynamic Background Glow */}
      <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-primary/10 rounded-full blur-[120px] -translate-y-1/2 translate-x-1/3 pointer-events-none" />
      <div className="absolute outline outline-[1200px] outline-transparent top-1/2 left-0 w-[600px] h-[600px] bg-success/5 rounded-full blur-[120px] -translate-x-1/3 -translate-y-1/2 pointer-events-none" />

      <div className="w-full max-w-md bg-surface/80 backdrop-blur-xl border border-white/10 rounded-[24px] p-8 md:p-10 shadow-2xl relative z-10">
        
        <div className="flex flex-col items-center text-center mb-8">
          <div className="w-12 h-12 rounded-xl bg-primary/20 flex items-center justify-center mb-4 border border-primary/30 shadow-[0_0_20px_var(--accent-glow)]">
            <TerminalSquare className="w-6 h-6 text-primary" />
          </div>
          <h1 className="text-2xl font-bold tracking-tight text-white mb-2">Create your Tenant</h1>
          <p className="text-sm text-muted">Get started with OrvixFlow Enterprise</p>
        </div>

        {error && (
          <div className="mb-6 p-3 rounded-lg bg-danger/10 border border-danger/20 text-danger text-sm text-center">
            {error}
          </div>
        )}

        <form onSubmit={handleRegister} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label htmlFor="displayName" className="text-xs font-medium text-muted ml-0.5">Organization Name</label>
            <input 
              type="text" 
              id="displayName" 
              placeholder="Acme Corp" 
              value={formData.displayName}
              onChange={(e) => setFormData({ ...formData, displayName: e.target.value })}
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
              required 
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="email" className="text-xs font-medium text-muted ml-0.5">Work Email</label>
            <input 
              type="email" 
              id="email" 
              placeholder="you@company.com" 
              value={formData.email}
              onChange={(e) => setFormData({ ...formData, email: e.target.value })}
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
              required 
            />
          </div>
          
          <div className="flex flex-col gap-1.5 mb-2">
            <label htmlFor="password" className="text-xs font-medium text-muted ml-0.5">Password</label>
            <input 
              type="password" 
              id="password" 
              placeholder="••••••••" 
              value={formData.password}
              onChange={(e) => setFormData({ ...formData, password: e.target.value })}
              className="w-full bg-background border border-white/10 rounded-lg px-4 py-3 text-sm text-white placeholder:text-white/20 focus:outline-none focus:border-primary/50 focus:ring-1 focus:ring-primary/50 transition-all font-medium"
              required 
              minLength={8}
            />
          </div>

          <button 
            type="submit" 
            disabled={isLoading}
            className="w-full bg-primary hover:bg-primary/90 text-white font-medium rounded-lg px-4 py-3 text-sm shadow-[0_4px_14px_var(--accent-glow)] transition-all active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isLoading ? "Provisioning Tenant..." : "Create Account"}
          </button>
        </form>

        <p className="mt-8 text-center text-xs text-muted">
          Already have an account?{" "}
          <Link href="/login" className="text-primary hover:text-white transition-colors font-medium">Sign in instead</Link>
        </p>

      </div>
    </div>
  );
}
