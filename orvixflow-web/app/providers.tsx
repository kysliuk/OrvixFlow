"use client";

import { SessionProvider, signOut, useSession } from "next-auth/react";
import { useEffect } from "react";

function SessionWatcher() {
  const { data: session } = useSession();

  useEffect(() => {
    if (session?.error === "RefreshTokenExpired") {
      console.warn("Session expired (refresh failed). Signing out...");
      
      // Best-effort backend logout if token exists
      const apiToken = session?.apiToken;
      if (apiToken) {
        fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/auth/logout`, {
          method: "POST",
          headers: { "Authorization": `Bearer ${apiToken}` }
        }).catch(() => {/* Ignore errors if backend is down */});
      }

      signOut({ callbackUrl: "/login" });
    }
  }, [session]);

  return null;
}

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <SessionProvider>
      <SessionWatcher />
      {children}
    </SessionProvider>
  );
}
