/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
import NextAuth, { CredentialsSignin } from "next-auth";
import GoogleProvider from "next-auth/providers/google";
import CredentialsProvider from "next-auth/providers/credentials";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";

class CustomAuthError extends CredentialsSignin {
  constructor(public message: string) {
    super(message);
    this.code = message;
  }
}

const apiBaseUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL;

function parseJwtExp(token: string): number | null {
  try {
    const payload = Buffer.from(token.split('.')[1], 'base64').toString('utf-8');
    return JSON.parse(payload).exp;
  } catch {
    return null;
  }
}

function normalizeCompanyScope(tenantId: string | null | undefined, activeCompanyId: string | null | undefined) {
  if (activeCompanyId == null) {
    return {
      tenantId: null,
      activeCompanyId: null,
    };
  }

  return {
    tenantId: tenantId ?? null,
    activeCompanyId,
  };
}

function applyProfileToToken(token: any, profile: any) {
  const companyScope = normalizeCompanyScope(profile?.tenantId, profile?.activeCompanyId);

  token.name = profile?.displayName ?? token.name;
  token.tenantId = companyScope.tenantId;
  token.activeCompanyId = companyScope.activeCompanyId;
  token.plan = profile?.plan;
  token.role = profile?.role;
  token.globalRole = profile?.globalRole;
  token.companies = profile?.companies ?? [];
}

function clearAuthState(token: any) {
  token.apiToken = "";
  token.refreshToken = "";
  token.tenantId = undefined;
  token.activeCompanyId = undefined;
  token.plan = undefined;
  token.role = undefined;
  token.globalRole = undefined;
  token.companies = [];
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  trustHost: true,
  providers: [
    GoogleProvider({
      clientId: process.env.GOOGLE_CLIENT_ID,
      clientSecret: process.env.GOOGLE_CLIENT_SECRET,
    }),
    MicrosoftEntraId({
      clientId: process.env.AZURE_AD_CLIENT_ID,
      clientSecret: process.env.AZURE_AD_CLIENT_SECRET,
    }),
    CredentialsProvider({
      name: "Email/Password",
      credentials: {
        email: { label: "Email", type: "email" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        const res = await fetch(`${apiBaseUrl}/api/auth/login`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            email: credentials?.email,
            password: credentials?.password,
          }),
        });

        const data = await res.json();
        if (res.ok && data.token) {
          return {
            id: data.profile.userId,
            name: data.profile.displayName,
            email: data.profile.email,
            apiToken: data.token,
            refreshToken: data.refreshToken,
            tenantId: data.profile.tenantId,
            activeCompanyId: data.profile.activeCompanyId,
            plan: data.profile.plan,
            role: data.profile.role,
            globalRole: data.profile.globalRole,
            companies: data.profile.companies ?? [],
          };
        }

        // If we have a specific error message from the backend, throw it as an AuthError
        // In NextAuth 5, this allows it to be passed gracefully to the error= parameter
        if (data.error) {
          throw new CustomAuthError(data.error);
        }

        return null;
      },
    }),
    CredentialsProvider({
      id: "invite-accept",
      name: "Accept Invitation",
      credentials: {
        token: { label: "Token", type: "text" },
        displayName: { label: "Display Name", type: "text" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        const res = await fetch(`${apiBaseUrl}/api/invite/accept`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            token: credentials?.token,
            displayName: credentials?.displayName,
            password: credentials?.password,
          }),
        });

        const data = await res.json();
        if (res.ok && data.token) {
          return {
            id: data.profile.userId,
            name: data.profile.displayName,
            email: data.profile.email,
            apiToken: data.token,
            refreshToken: data.refreshToken,
            tenantId: data.profile.tenantId,
            activeCompanyId: data.profile.activeCompanyId,
            plan: data.profile.plan,
            role: data.profile.role,
            globalRole: data.profile.globalRole,
            companies: data.profile.companies ?? [],
          };
        }

        if (data.error) {
          throw new CustomAuthError(data.error);
        }

        return null;
      },
    }),
  ],
  callbacks: {
    async signIn({ user, account, profile }) {
      if (account && (account.type === "oauth" || account.type === "oidc" || account.provider !== "credentials")) {
        try {
          const res = await fetch(`${apiBaseUrl}/api/auth/oauth-provision`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              email: user.email,
              displayName: user.name || (profile as any)?.name,
              provider: account.provider,
              externalId: account.providerAccountId,
            }),
          });

          if (res.ok) {
            const data = await res.json();
            // Attach data to user so jwt callback can read it
            (user as any).apiData = data;
            return true;
          } else {
            const err = await res.json();
            if (err.error?.includes("already exists")) {
              return "/login?error=OAuthAccountNotLinked";
            }
            console.error("OAuth provisioning returned non-200:", err);
            return "/login?error=AccessDenied";
          }
        } catch (e) {
          console.error("OAuth provision failed in signIn:", e);
          return "/login?error=AccessDenied";
        }
      }
      return true;
    },
    async jwt({ token, user, account, profile, trigger, session }) {
      if (trigger === "update" && session) {
        if (session.token) token.apiToken = session.token;
        if ((session as any).refreshToken) token.refreshToken = (session as any).refreshToken;
        if (session.profile) {
          applyProfileToToken(token, session.profile);
        }
        return token;
      }

      // `account` and `user` are only defined on the very first sign in!
      if (account && user) {
        if (account.type === "oauth" || account.type === "oidc" || account.provider !== "credentials") {
          const data = (user as any).apiData;
          if (data && data.profile) {
            token.apiToken = data.token;
            token.refreshToken = data.refreshToken;
            token.sub = data.profile.userId;
            applyProfileToToken(token, data.profile);
          }
        } else if (account.type === "credentials") {
          // Local login already has the token bundled inside `user`
          token.apiToken = (user as any).apiToken;
          token.refreshToken = (user as any).refreshToken;
          applyProfileToToken(token, {
            displayName: user.name,
            tenantId: (user as any).tenantId,
            activeCompanyId: (user as any).activeCompanyId,
            plan: (user as any).plan,
            role: (user as any).role,
            globalRole: (user as any).globalRole,
            companies: (user as any).companies,
          });
        }
      }

      // Token Refresh Logic
      if (token.apiToken && token.refreshToken) {
        const exp = parseJwtExp(token.apiToken as string);
        if (exp) {
          // If token expires in less than 5 minutes (300 seconds)
          const nowInSeconds = Math.floor(Date.now() / 1000);
          if (exp - nowInSeconds < 300) {
            try {
              console.log("Token expiring soon, attempting refresh...");
              const refreshRes = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                  refreshToken: token.refreshToken,
                  activeCompanyId: token.activeCompanyId,
                }),
              });

              if (refreshRes.ok) {
                const data = await refreshRes.json();
                token.apiToken = data.token;
                token.refreshToken = data.refreshToken;
                if (data.profile) {
                  applyProfileToToken(token, data.profile);
                }
              } else {
                console.warn(`Failed to refresh token. API returned: ${refreshRes.status}`);
                // Invalidate API token to force re-auth on the next protected request.
                clearAuthState(token);
              }
            } catch (e) {
              console.error("Refresh token fetch failed", e);
              clearAuthState(token);
            }
          }
        }
      }

      return token;
    },
    async session({ session, token }) {
      if (token) {
        const companyScope = normalizeCompanyScope(token.tenantId as string | null | undefined, token.activeCompanyId as string | null | undefined);

        session.user.id = token.sub!;
        session.user.name = token.name as string;
        // Pass the API Token and company scope into the active session
        session.apiToken = token.apiToken as string;
        session.user.tenantId = companyScope.tenantId;
        session.user.activeCompanyId = companyScope.activeCompanyId;
        session.user.plan = token.plan as string;
        session.user.role = token.role as string;
        (session.user as any).globalRole = (token as any).globalRole as string;
        session.user.companies = (token.companies as any[]) || [];
      }
      return session;
    },
  },
  events: {
    async signOut(message) {
      if (!("token" in message)) return;

      const refreshToken = (message.token as any)?.refreshToken as string | undefined;
      if (!refreshToken) return;

      try {
        await fetch(`${apiBaseUrl}/api/auth/logout`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken }),
        });
      } catch (error) {
        console.error("Failed to revoke refresh token during sign-out", error);
      }
    },
  },
  pages: {
    signIn: "/login",
  },
  session: { strategy: "jwt" },
});
