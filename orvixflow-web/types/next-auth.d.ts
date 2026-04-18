import { type DefaultSession } from "next-auth"

declare module "next-auth" {
  /**
   * Returned by `useSession`, `auth()`, and received as a prop on the `SessionProvider` React Context
   */
  interface Session {
    apiToken: string
    error?: "RefreshTokenExpired"
    user: {
      tenantId: string
      activeCompanyId: string
      plan: string
      role: string
      globalRole?: string
      companies?: { companyId: string; companyName: string; role: string }[]
    } & DefaultSession["user"]
  }

  interface User {
    apiToken?: string
    refreshToken?: string
    tenantId?: string
    activeCompanyId?: string
    plan?: string
    role?: string
    globalRole?: string
    companies?: { companyId: string; companyName: string; role: string }[]
  }
}

import { JWT as DefaultJWT } from "next-auth/jwt"

declare module "next-auth/jwt" {
  interface JWT extends DefaultJWT {
    apiToken?: string
    refreshToken?: string
    tenantId?: string
    activeCompanyId?: string
    plan?: string
    role?: string
    globalRole?: string
    companies?: { companyId: string; companyName: string; role: string }[]
    error?: "RefreshTokenExpired"
  }
}
