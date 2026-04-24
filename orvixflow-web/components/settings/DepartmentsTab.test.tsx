import React from "react"
import { render, screen } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"

import { DepartmentsTab } from "./DepartmentsTab"

const useSessionMock = vi.fn()

vi.mock("next-auth/react", () => ({
  useSession: () => useSessionMock(),
}))

describe("DepartmentsTab", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    useSessionMock.mockReturnValue({
      data: {
        apiToken: "api-token",
        user: {
          role: "CompanyMember",
        },
      },
    })
  })

  it("allows department managers to open the tab without department-admin actions", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(
          JSON.stringify([
            { departmentId: "dept-1", name: "Operations", code: "OPS", role: "DepartmentManager" },
          ]),
          { status: 200 }
        )
      )
    )

    render(<DepartmentsTab currentRole="CompanyMember" />)

    await screen.findByText("Operations")

    expect(screen.getByText(/department creation is limited to company admins/i)).toBeDefined()
    expect(screen.queryByRole("button", { name: /new department/i })).toBeNull()
  })
})
