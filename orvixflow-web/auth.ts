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
        if (session.profile) {
          token.name = session.profile.displayName;
          token.tenantId = session.profile.tenantId;
          token.activeCompanyId = session.profile.activeCompanyId;
          token.plan = session.profile.plan;
          token.role = session.profile.role;
          (token as any).globalRole = session.profile.globalRole;
          token.companies = session.profile.companies ?? [];
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
            token.tenantId = data.profile.tenantId;
            token.activeCompanyId = data.profile.activeCompanyId;
            token.plan = data.profile.plan;
            token.role = data.profile.role;
            (token as any).globalRole = data.profile.globalRole;
            token.companies = data.profile.companies ?? [];
          }
        } else if (account.type === "credentials") {
          // Local login already has the token bundled inside `user`
          token.apiToken = (user as any).apiToken;
          token.refreshToken = (user as any).refreshToken;
          token.tenantId = (user as any).tenantId;
          token.activeCompanyId = (user as any).activeCompanyId;
          token.plan = (user as any).plan;
          token.role = (user as any).role;
          (token as any).globalRole = (user as any).globalRole;
          token.companies = (user as any).companies ?? [];
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
                body: JSON.stringify({ refreshToken: token.refreshToken }),
              });

              if (refreshRes.ok) {
                const data = await refreshRes.json();
                token.apiToken = data.token;
                token.refreshToken = data.refreshToken;
              } else {
                console.warn(`Failed to refresh token. API returned: ${refreshRes.status}`);
                // Invalidate API token to force re-auth
                token.apiToken = "";
                token.refreshToken = "";
              }
            } catch (e) {
              console.error("Refresh token fetch failed", e);
            }
          }
        }
      }

      return token;
    },
    async session({ session, token }) {
      if (token) {
        session.user.id = token.sub!;
        session.user.name = token.name as string;
        // Pass the API Token and Tenant data into the active session
        session.apiToken = token.apiToken as string;
        session.user.tenantId = token.tenantId as string;
        session.user.activeCompanyId = (token.activeCompanyId as string) || (token.tenantId as string);
        session.user.plan = token.plan as string;
        session.user.role = token.role as string;
        (session.user as any).globalRole = (token as any).globalRole as string;
        session.user.companies = (token.companies as any[]) || [];
      }
      return session;
    },
  },
  pages: {
    signIn: "/login",
  },
  session: { strategy: "jwt" },
});
