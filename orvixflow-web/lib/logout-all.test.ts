import { describe, it, expect, vi, beforeEach } from "vitest";

describe("logout-all API call", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    global.fetch = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
  });

  it("calls the logout-all endpoint with bearer auth", async () => {
    const token = "api-token-123";
    await fetch("http://localhost:8080/api/auth/logout-all", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
      },
    });

    expect(global.fetch).toHaveBeenCalledWith(
      "http://localhost:8080/api/auth/logout-all",
      expect.objectContaining({
        method: "POST",
        headers: expect.objectContaining({
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        }),
      })
    );
  });
});
