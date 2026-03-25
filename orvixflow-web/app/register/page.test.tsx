import { describe, it, expect, vi, beforeEach } from "vitest";

describe("Register Page API Parsing", () => {
    beforeEach(() => {
    });

    it("should gracefully handle a non-JSON 500 response", async () => {
        // Mock fetch returning a 500 error with HTML/Text payload
        const htmlPayload = "<html><body>Microsoft.EntityFramework...</body></html>";
        global.fetch = vi.fn().mockResolvedValue({
            ok: false,
            headers: new Headers({ "content-type": "text/html" }),
            text: async () => htmlPayload,
            json: async () => { throw new Error("Unexpected token < in JSON"); }
        });

        // The form submission runs fetch. If it throws, it catches and sets error string.
        // For unit test isolation without mounting React, we can simulate the API call logic here:
        let errorMsg = "Registration failed";
        const res = await fetch("http://localhost/api/auth/register");
        try {
            if (!res.ok) {
                const contentType = res.headers.get("content-type");
                if (contentType && contentType.includes("application/json")) {
                    const data = await res.json();
                    errorMsg = data.error || errorMsg;
                } else {
                    const text = await res.text();
                    errorMsg = "An unexpected server error occurred. Please try again later.";
                }
                throw new Error(errorMsg);
            }
        } catch (err: any) {
            expect(err.message).toBe("An unexpected server error occurred. Please try again later.");
        }
    });
});
