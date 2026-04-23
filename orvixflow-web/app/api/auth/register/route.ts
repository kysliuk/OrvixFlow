/* eslint-disable @typescript-eslint/no-explicit-any */
import { NextResponse } from 'next/server';

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const apiBaseUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL;
    
    // Explicitly add connection diagnostics
    console.log(`[Register Route] Forwarding registration to: ${apiBaseUrl}/api/auth/register`);
    
    const forwardedFor = req.headers.get("x-forwarded-for");
    const realIp = req.headers.get("x-real-ip");
    const userAgent = req.headers.get("user-agent");

    const forwardHeaders: Record<string, string> = {
      "Content-Type": "application/json",
    };

    if (forwardedFor) forwardHeaders["X-Forwarded-For"] = forwardedFor;
    if (realIp) forwardHeaders["X-Real-IP"] = realIp;
    if (userAgent) forwardHeaders["User-Agent"] = userAgent;

    const res = await fetch(`${apiBaseUrl}/api/auth/register`, {
      method: "POST",
      headers: forwardHeaders,
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const contentType = res.headers.get("content-type") || "";
      let errorData = { error: "Registration failed" };
      
      if (contentType.includes("application/json")) {
        const parsed = await res.json();
        errorData = { error: parsed.error || parsed.message || "Registration failed" };
      } else {
        const text = await res.text();
        console.error(`[Register Route] Unexpected response: ${res.status} ${text}`);
      }
      return NextResponse.json(errorData, { status: res.status });
    }

    const data = await res.json();
    return NextResponse.json(data);
  } catch (err: any) {
    console.error("[Register Route] Server error:", err);
    return NextResponse.json({ 
      error: "Failed to reach the authentication service. Please try again later.",
      details: err.message,
      code: err.code
    }, { status: 503 });
  }
}
