import React from "react"
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react"
import { beforeEach, afterEach, describe, expect, it, vi } from "vitest"
import InboxSettingsPage from "./page"

const useSessionMock = vi.fn()

vi.mock("next-auth/react", () => ({
  useSession: () => useSessionMock(),
}))

vi.mock("@/components/module-gate", () => ({
  ModuleGate: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}))

describe("InboxSettingsPage - Connections & OAuth capturing", () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
    })
    
    // Stub sessionStorage
    const sessionStorageMock = (() => {
      let store: Record<string, string> = {}
      return {
        getItem: vi.fn((key: string) => store[key] || null),
        setItem: vi.fn((key: string, value: string) => {
          store[key] = value.toString()
        }),
        removeItem: vi.fn((key: string) => {
          delete store[key]
        }),
        clear: vi.fn(() => {
          store = {}
        })
      }
    })()
    vi.stubGlobal("sessionStorage", sessionStorageMock)

    // Stub window.location
    const locationMock = { href: "" }
    vi.stubGlobal("location", locationMock)
  })

  afterEach(() => {
    cleanup()
  })

  it("should fetch and render mailbox connections with correct credentials buttons and actions", async () => {
    useSessionMock.mockReturnValue({
      status: "authenticated",
      data: { apiToken: "api-token", user: { activeCompanyId: "company-1" } },
    })

    const mockConnections = [
      {
        id: "conn-gmail-unlinked",
        emailAddress: "gmail-unlinked@gmail.com",
        provider: "Gmail",
        isActive: false,
        hasCredential: false,
        createdAtUtc: "2026-06-11T12:00:00Z"
      },
      {
        id: "conn-outlook-linked",
        emailAddress: "outlook-linked@outlook.com",
        provider: "Outlook",
        isActive: true,
        hasCredential: true,
        createdAtUtc: "2026-06-11T12:00:00Z",
        n8nWorkflowId: "wf-123"
      }
    ]

    const fetchMock = vi.fn().mockImplementation((url: string) => {
      if (url.endsWith("/connections")) {
        return Promise.resolve(new Response(JSON.stringify(mockConnections), { status: 200 }))
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }))
    })
    vi.stubGlobal("fetch", fetchMock)

    render(<InboxSettingsPage />)

    // Wait for the fetch and elements to render
    await waitFor(() => {
      expect(screen.getByText("gmail-unlinked@gmail.com")).toBeDefined()
      expect(screen.getByText("outlook-linked@outlook.com")).toBeDefined()
    })

    // "gmail-unlinked@gmail.com" has no credential, so it should render "Connect Google Mail"
    expect(screen.getByRole("button", { name: "Connect Google Mail" })).toBeDefined()

    // "outlook-linked@outlook.com" has credential, so it should render "Disconnect" and "Deactivate"
    expect(screen.getByRole("button", { name: "Disconnect" })).toBeDefined()
    expect(screen.getByRole("button", { name: "Deactivate" })).toBeDefined()
  })

  it("should initiate OAuth flow and save connectionId in sessionStorage on Connect click", async () => {
    useSessionMock.mockReturnValue({
      status: "authenticated",
      data: { apiToken: "api-token", user: { activeCompanyId: "company-1" } },
    })

    const mockConnections = [
      {
        id: "conn-gmail-unlinked",
        emailAddress: "gmail-unlinked@gmail.com",
        provider: "Gmail",
        isActive: false,
        hasCredential: false,
        createdAtUtc: "2026-06-11T12:00:00Z"
      }
    ]

    const fetchMock = vi.fn().mockImplementation((url: string, options?: any) => {
      if (url.endsWith("/connections")) {
        return Promise.resolve(new Response(JSON.stringify(mockConnections), { status: 200 }))
      }
      if (url.endsWith("/conn-gmail-unlinked/credential/authorize")) {
        return Promise.resolve(new Response(JSON.stringify({ authorizationUrl: "https://accounts.google.com/oauth-url" }), { status: 200 }))
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }))
    })
    vi.stubGlobal("fetch", fetchMock)

    render(<InboxSettingsPage />)

    await waitFor(() => {
      expect(screen.getByText("gmail-unlinked@gmail.com")).toBeDefined()
    })

    const connectButton = screen.getByRole("button", { name: "Connect Google Mail" })
    fireEvent.click(connectButton)

    await waitFor(() => {
      expect(sessionStorage.setItem).toHaveBeenCalledWith("oauth_connection_id", "conn-gmail-unlinked")
      expect(window.location.href).toBe("https://accounts.google.com/oauth-url")
    })
  })

  it("should call delete credential API on Disconnect click and refresh list", async () => {
    useSessionMock.mockReturnValue({
      status: "authenticated",
      data: { apiToken: "api-token", user: { activeCompanyId: "company-1" } },
    })

    const mockConnections = [
      {
        id: "conn-outlook-linked",
        emailAddress: "outlook-linked@outlook.com",
        provider: "Outlook",
        isActive: true,
        hasCredential: true,
        createdAtUtc: "2026-06-11T12:00:00Z"
      }
    ]

    const fetchMock = vi.fn().mockImplementation((url: string, options?: any) => {
      if (url.endsWith("/connections")) {
        return Promise.resolve(new Response(JSON.stringify(mockConnections), { status: 200 }))
      }
      if (url.endsWith("/conn-outlook-linked/credential") && options?.method === "DELETE") {
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      return Promise.resolve(new Response(JSON.stringify([]), { status: 200 }))
    })
    vi.stubGlobal("fetch", fetchMock)

    render(<InboxSettingsPage />)

    await waitFor(() => {
      expect(screen.getByText("outlook-linked@outlook.com")).toBeDefined()
    })

    const disconnectButton = screen.getByRole("button", { name: "Disconnect" })
    fireEvent.click(disconnectButton)

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining("/api/v1/inbox/connections/conn-outlook-linked/credential"),
        expect.objectContaining({ method: "DELETE" })
      )
    })
  })
})
