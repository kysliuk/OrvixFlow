import NextAuth from "next-auth";
import GoogleProvider from "next-auth/providers/google";
import CredentialsProvider from "next-auth/providers/credentials";
import MicrosoftEntraId from "next-auth/providers/microsoft-entra-id";

const apiBaseUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL;

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
            tenantId: data.profile.tenantId,
            activeCompanyId: data.profile.activeCompanyId,
            plan: data.profile.plan,
            role: data.profile.role,
            companies: data.profile.companies ?? [],
          };
        }
        return null;
      },
    }),
  ],
  callbacks: {
    async jwt({ token, user, account, profile, trigger, session }) {
      if (trigger === "update" && session) {
        if (session.token) token.apiToken = session.token;
        if (session.profile) {
          token.name = session.profile.displayName;
          token.tenantId = session.profile.tenantId;
          token.activeCompanyId = session.profile.activeCompanyId;
          token.plan = session.profile.plan;
          token.role = session.profile.role;
          token.companies = session.profile.companies ?? [];
        }
        return token;
      }

      // `account` and `user` are only defined on the very first sign in!
      if (account && user) {
        if (account.type === "oauth" || account.type === "oidc" || account.provider !== "credentials") {
          try {
            // Provision the tenant/user and get the API token from our backend
            const res = await fetch(`${apiBaseUrl}/api/auth/oauth-provision`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({
                email: user.email,
                displayName: user.name || (profile as any)?.name,
                provider: account.provider,
                externalId: user.id || (profile as any)?.sub,
              }),
            });
            
            if (res.ok) {
              const data = await res.json();
              token.apiToken = data.token;
              token.tenantId = data.profile.tenantId;
              token.activeCompanyId = data.profile.activeCompanyId;
              token.plan = data.profile.plan;
              token.role = data.profile.role;
              token.companies = data.profile.companies ?? [];
            } else {
              console.error("OAuth provisioning returned non-200");
            }
          } catch (e) {
            console.error("OAuth provisioning fetch failed", e);
          }
        } else if (account.type === "credentials") {
          // Local login already has the token bundled inside `user`
          token.apiToken = (user as any).apiToken;
          token.tenantId = (user as any).tenantId;
          token.activeCompanyId = (user as any).activeCompanyId;
          token.plan = (user as any).plan;
          token.role = (user as any).role;
          token.companies = (user as any).companies ?? [];
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
