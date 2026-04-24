import React from "react"
import { render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

import InboxGuardianPage from "./page"

const useSessionMock = vi.fn()

vi.mock("next-auth/react", () => ({
  useSession: () => useSessionMock(),
}))

vi.mock("@/components/module-gate", () => ({
  ModuleGate: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}))

describe("InboxGuardianPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
    })
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ items: [], total: 0, limit: 100, offset: 0 }), { status: 200 })))
  })

  it("waits for session readiness before fetching events", async () => {
    useSessionMock
      .mockReturnValueOnce({ status: "loading", data: null })
      .mockReturnValue({
        status: "authenticated",
        data: { apiToken: "api-token", user: { activeCompanyId: "company-1" } },
      })

    const { rerender } = render(<InboxGuardianPage />)

    expect(screen.queryByText("Not authenticated")).toBeNull()
    expect(fetch).not.toHaveBeenCalled()

    rerender(<InboxGuardianPage />)

    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/v1/inbox/events?limit=100"),
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: "Bearer api-token" }),
        })
      )
    })
  })
})
