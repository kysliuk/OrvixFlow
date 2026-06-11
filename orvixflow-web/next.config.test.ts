import { beforeEach, describe, expect, it } from "vitest"
import nextConfig from "./next.config"

describe("next security headers", () => {
  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.orvixflow.test"
  })

  it("adds a Content-Security-Policy header for all routes", async () => {
    const headerGroups = await nextConfig.headers?.()
    const rootHeaders = headerGroups?.find((entry) => entry.source === "/:path*")?.headers ?? []
    const cspHeader = rootHeaders.find((header) => header.key === "Content-Security-Policy")

    expect(cspHeader).toBeDefined()
    expect(cspHeader?.value).toContain("default-src 'self'")
    expect(cspHeader?.value).toContain("frame-ancestors 'none'")
  })

  it("allows API and OAuth endpoints in connect-src", async () => {
    const headerGroups = await nextConfig.headers?.()
    const rootHeaders = headerGroups?.find((entry) => entry.source === "/:path*")?.headers ?? []
    const cspValue = rootHeaders.find((header) => header.key === "Content-Security-Policy")?.value ?? ""

    expect(cspValue).toContain("connect-src 'self' https://api.orvixflow.test https://accounts.google.com https://login.microsoftonline.com")
  })
})
