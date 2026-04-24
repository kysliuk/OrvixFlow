/* eslint-disable @typescript-eslint/no-explicit-any */
import { beforeEach, describe, expect, it, vi } from "vitest";

const nextAuthState = vi.hoisted(() => ({
  config: null as any,
}));

vi.mock("next-auth", () => {
  class CredentialsSignin extends Error {
    code?: string;

    constructor(message?: string) {
      super(message);
      this.name = "CredentialsSignin";
    }
  }

  const NextAuth = (config: any) => {
    nextAuthState.config = config;
    return {
      handlers: {},
      auth: vi.fn(),
      signIn: vi.fn(),
      signOut: vi.fn(),
    };
  };

  return {
    default: NextAuth,
    CredentialsSignin,
  };

});

vi.mock("next-auth/providers/google", () => ({
  default: (config: any) => ({ id: "google", ...config }),
}));

vi.mock("next-auth/providers/credentials", () => ({
  default: (config: any) => ({ id: config.id ?? "credentials", ...config }),
}));

vi.mock("next-auth/providers/microsoft-entra-id", () => ({
  default: (config: any) => ({ id: "microsoft-entra-id", ...config }),
}));

import "./auth";

function createJwt(exp: number) {
  const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
  const payload = Buffer.from(JSON.stringify({ exp })).toString("base64url");
  return `${header}.${payload}.signature`;
}

const callbacks = nextAuthState.config.callbacks;

describe("auth callbacks", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.stubGlobal("fetch", vi.fn());
  });

  it("preserves no-org company scope for credentials sign-in", async () => {
    const result = await callbacks.jwt({
      token: {},
      user: {
        apiToken: createJwt(Math.floor(Date.now() / 1000) + 3600),
        refreshToken: "refresh-token",
        tenantId: "legacy-tenant",
        activeCompanyId: null,
        plan: "Free",
        role: "Viewer",
        globalRole: "",
        companies: [],
      },
      account: { type: "credentials", provider: "credentials" },
      trigger: "signIn",
    });

    expect(result.tenantId).toBeNull();
    expect(result.activeCompanyId).toBeNull();
  });

  it("preserves no-org company scope for oauth sign-in", async () => {
    const result = await callbacks.jwt({
      token: {},
      user: {
        apiData: {
          token: createJwt(Math.floor(Date.now() / 1000) + 3600),
          refreshToken: "refresh-token",
          profile: {
            userId: "user-1",
            tenantId: null,
            activeCompanyId: null,
            plan: "Free",
            role: "Viewer",
            globalRole: "",
            companies: [],
          },
        },
      },
      account: { type: "oauth", provider: "google" },
      trigger: "signIn",
    });

    expect(result.tenantId).toBeNull();
    expect(result.activeCompanyId).toBeNull();
  });

  it("preserves no-org company scope during update", async () => {
    const result = await callbacks.jwt({
      token: {},
      trigger: "update",
      session: {
        token: "api-token",
        refreshToken: "refresh-token",
        profile: {
          displayName: "No Org User",
          tenantId: null,
          activeCompanyId: null,
          plan: "Free",
          role: "Viewer",
          globalRole: "",
          companies: [],
        },
      },
    });

    expect(result.tenantId).toBeNull();
    expect(result.activeCompanyId).toBeNull();
  });

  it("preserves no-org company scope after refresh", async () => {
    (fetch as any).mockResolvedValue({
      ok: true,
      json: async () => ({
        token: createJwt(Math.floor(Date.now() / 1000) + 3600),
        refreshToken: "new-refresh-token",
        profile: {
          tenantId: null,
          activeCompanyId: null,
          plan: "Free",
          role: "Viewer",
          globalRole: "",
          companies: [],
        },
      }),
    });

    const result = await callbacks.jwt({
      token: {
        sub: "user-1",
        name: "No Org User",
        apiToken: createJwt(Math.floor(Date.now() / 1000) + 60),
        refreshToken: "refresh-token",
        tenantId: "legacy-tenant",
        activeCompanyId: "legacy-company",
        plan: "Starter",
        role: "Operator",
        globalRole: "",
        companies: [{ companyId: "company-1", companyName: "Example", role: "Operator" }],
      },
    });

    expect(result.tenantId).toBeNull();
    expect(result.activeCompanyId).toBeNull();
    expect(result.plan).toBe("Free");
    expect(result.role).toBe("Viewer");
    expect(result.companies).toEqual([]);
  });

  it("clears auth state when refresh fails", async () => {
    (fetch as any).mockResolvedValue({
      ok: false,
      status: 401,
    });

    const result = await callbacks.jwt({
      token: {
        sub: "user-1",
        name: "No Org User",
        apiToken: createJwt(Math.floor(Date.now() / 1000) + 60),
        refreshToken: "refresh-token",
        tenantId: "legacy-tenant",
        activeCompanyId: "legacy-company",
        plan: "Starter",
        role: "Operator",
        globalRole: "",
        companies: [{ companyId: "company-1", companyName: "Example", role: "Operator" }],
      },
    });

    expect(result.apiToken).toBe("");
    expect(result.refreshToken).toBe("");
    expect(result.tenantId).toBeUndefined();
    expect(result.activeCompanyId).toBeUndefined();
  });

  it("does not backfill active company from tenant id in the session", async () => {
    const result = await callbacks.session({
      session: { user: {} },
      token: {
        sub: "user-1",
        name: "No Org User",
        apiToken: "api-token",
        tenantId: "legacy-tenant",
        activeCompanyId: null,
        plan: "Free",
        role: "Viewer",
        globalRole: "",
        companies: [],
      },
    });

    expect(result.user.tenantId).toBeNull();
    expect(result.user.activeCompanyId).toBeNull();
  });

  it("applies switched-company profile data during session update", async () => {
    const result = await callbacks.jwt({
      token: {
        sub: "user-1",
        name: "Switch User",
        apiToken: "old-api-token",
        refreshToken: "old-refresh-token",
        tenantId: "company-1",
        activeCompanyId: "company-1",
        plan: "Starter",
        role: "Operator",
        globalRole: "",
        companies: [
          { companyId: "company-1", companyName: "Alpha", role: "Operator" },
          { companyId: "company-2", companyName: "Beta", role: "CompanyAdmin" },
        ],
      },
      trigger: "update",
      session: {
        token: "new-api-token",
        refreshToken: "new-refresh-token",
        profile: {
          displayName: "Switch User",
          tenantId: "company-2",
          activeCompanyId: "company-2",
          plan: "Growth",
          role: "CompanyAdmin",
          globalRole: "",
          companies: [
            { companyId: "company-1", companyName: "Alpha", role: "Operator" },
            { companyId: "company-2", companyName: "Beta", role: "CompanyAdmin" },
          ],
        },
      },
    })

    expect(result.apiToken).toBe("new-api-token")
    expect(result.refreshToken).toBe("new-refresh-token")
    expect(result.tenantId).toBe("company-2")
    expect(result.activeCompanyId).toBe("company-2")
    expect(result.plan).toBe("Growth")
    expect(result.role).toBe("CompanyAdmin")
  })
});
