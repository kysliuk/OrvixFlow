import { type DefaultSession } from "next-auth"

declare module "next-auth" {
  /**
   * Returned by `useSession`, `auth()`, and received as a prop on the `SessionProvider` React Context
   */
  interface Session {
    apiToken: string
    user: {
      tenantId: string | null
      activeCompanyId: string | null
      plan: string
      role: string
      companies?: { companyId: string; companyName: string; role: string }[]
    } & DefaultSession["user"]
  }

  interface User {
    apiToken?: string
    tenantId?: string | null
    activeCompanyId?: string | null
    plan?: string
    role?: string
    companies?: { companyId: string; companyName: string; role: string }[]
  }
}
