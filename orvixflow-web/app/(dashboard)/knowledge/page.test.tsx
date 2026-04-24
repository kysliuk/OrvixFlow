import React from "react"
import { render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

import KnowledgeBasePage from "./page"

const useSessionMock = vi.fn()

vi.mock("next-auth/react", () => ({
  useSession: () => useSessionMock(),
}))

describe("KnowledgeBasePage", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
    })
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.includes("/api/v1/knowledge/documents")) {
          return new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), { status: 200 })
        }

        return new Response(JSON.stringify({ items: [], total: 0, limit: 100, offset: 0 }), { status: 200 })
      })
    )
  })

  it("waits for session readiness before fetching knowledge data", async () => {
    useSessionMock
      .mockReturnValueOnce({ status: "loading", data: null })
      .mockReturnValue({
        status: "authenticated",
        data: { apiToken: "api-token", user: { activeCompanyId: "company-1" } },
      })

    const { rerender } = render(<KnowledgeBasePage />)

    expect(screen.queryByText("Not authenticated")).toBeNull()
    expect(fetch).not.toHaveBeenCalled()

    rerender(<KnowledgeBasePage />)

    await waitFor(() => {
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/v1/knowledge?limit=100"),
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: "Bearer api-token" }),
        })
      )
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining("/api/v1/knowledge/documents?page=1&pageSize=20"),
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: "Bearer api-token" }),
        })
      )
    })
  })
})
