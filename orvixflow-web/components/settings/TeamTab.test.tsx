import React from "react"
import { render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

import { TeamTab } from "./TeamTab"

const useSessionMock = vi.fn()

vi.mock("next-auth/react", () => ({
  useSession: () => useSessionMock(),
}))

describe("TeamTab", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    useSessionMock.mockReturnValue({
      data: {
        apiToken: "api-token",
        user: {
          id: "current-user",
          role: "CompanyMember",
        },
      },
    })
  })

  it("renders department-manager invite controls for company members", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/team")) {
          return new Response(JSON.stringify([]), { status: 200 })
        }

        if (url.endsWith("/api/invite")) {
          return new Response(JSON.stringify([]), { status: 200 })
        }

        if (url.endsWith("/api/org/departments")) {
          return new Response(
            JSON.stringify([
              { departmentId: "dept-1", name: "Operations", code: "OPS", role: "DepartmentManager" },
            ]),
            { status: 200 }
          )
        }

        return new Response(JSON.stringify([]), { status: 404 })
      })
    )

    render(<TeamTab currentRole="CompanyMember" />)

    await screen.findByText("Invite a New Member")

    expect(screen.getByText(/CompanyMember access is assigned automatically/i)).toBeDefined()
    expect(screen.getByTestId("department-role-select")).toBeDefined()
    expect(screen.queryByTestId("company-role-select")).toBeNull()
  })

  it("hides company-removal actions from department managers", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input)
        if (url.endsWith("/api/team")) {
          return new Response(
            JSON.stringify([
              {
                userId: "member-1",
                email: "member@example.com",
                displayName: "Member One",
                companyRole: "CompanyMember",
                joinedAt: "2026-04-24T00:00:00Z",
                departmentIds: ["dept-1"],
              },
            ]),
            { status: 200 }
          )
        }

        if (url.endsWith("/api/invite")) {
          return new Response(JSON.stringify([]), { status: 200 })
        }

        if (url.endsWith("/api/org/departments")) {
          return new Response(
            JSON.stringify([
              { departmentId: "dept-1", name: "Operations", code: "OPS", role: "DepartmentManager" },
            ]),
            { status: 200 }
          )
        }

        return new Response(JSON.stringify([]), { status: 404 })
      })
    )

    render(<TeamTab currentRole="CompanyMember" />)

    await waitFor(() => {
      expect(screen.getByText("Member One")).toBeDefined()
    })

    expect(screen.queryByTestId("remove-company-member-member-1")).toBeNull()
  })
})
