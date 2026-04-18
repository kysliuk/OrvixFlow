import { describe, it, expect, vi } from "vitest";

vi.mock("@/auth", () => ({
  auth: (handler: unknown) => handler,
}));

import { handleAuthRouting } from "./middleware";

describe("handleAuthRouting", () => {
  function createRequest(pathname: string, authValue: any = null) {
    const nextUrl = new URL(`http://localhost${pathname}`) as URL & { pathname: string };
    Object.defineProperty(nextUrl, "pathname", { value: pathname, configurable: true });
    return {
      auth: authValue,
      nextUrl,
    } as any;
  }

  it("redirects protected routes to login when apiToken is missing", () => {
    const result = handleAuthRouting(createRequest("/settings", { user: { id: "u1" } }));

    expect(result).toBeInstanceOf(Response);
    expect((result as Response).headers.get("location")).toBe("http://localhost/login");
  });

  it("allows public invite route when apiToken is missing", () => {
    const result = handleAuthRouting(createRequest("/invite?token=abc", { user: { id: "u1" } }));

    expect(result).toBeUndefined();
  });

  it("redirects non-platform-admin users away from admin routes", () => {
    const result = handleAuthRouting(createRequest("/admin", {
      apiToken: "valid-token",
      user: { role: "CompanyAdmin" },
    }));

    expect(result).toBeInstanceOf(Response);
    expect((result as Response).headers.get("location")).toBe("http://localhost/");
  });
});
