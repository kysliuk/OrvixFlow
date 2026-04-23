/* eslint-disable @typescript-eslint/no-explicit-any, @typescript-eslint/no-unused-vars */
import { NextResponse } from 'next/server';

export async function POST(req: Request) {
  try {
    const body = await req.json();
    const apiBaseUrl = process.env.INTERNAL_API_URL || process.env.NEXT_PUBLIC_API_URL;
    
    console.log(`[Verify Route] Forwarding verification to: ${apiBaseUrl}/api/auth/verify`);
    
    const forwardedFor = req.headers.get("x-forwarded-for");
    const realIp = req.headers.get("x-real-ip");
    const userAgent = req.headers.get("user-agent");

    const forwardHeaders: Record<string, string> = {
      "Content-Type": "application/json",
    };

    if (forwardedFor) forwardHeaders["X-Forwarded-For"] = forwardedFor;
    if (realIp) forwardHeaders["X-Real-IP"] = realIp;
    if (userAgent) forwardHeaders["User-Agent"] = userAgent;

    const res = await fetch(`${apiBaseUrl}/api/auth/verify`, {
      method: "POST",
      headers: forwardHeaders,
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const contentType = res.headers.get("content-type") || "";
      let errorData = { error: "Verification failed" };
      
      const text = await res.text();
      console.error(`[Verify Route] Backend returned status ${res.status}: ${text}`);

      if (contentType.includes("application/json")) {
        try {
          const parsed = JSON.parse(text);
          errorData = { error: parsed.error || parsed.message || "Verification failed" };
        } catch (e) {
          console.error("[Verify Route] Failed to parse backend error JSON");
        }
      }
      return NextResponse.json(errorData, { status: res.status });
    }

    const data = await res.json();
    return NextResponse.json(data);
  } catch (err: any) {
    console.error("[Verify Route] Server error:", err);
    return NextResponse.json({ 
      error: "Failed to reach the authentication service. Please try again later.",
      details: err.message
    }, { status: 503 });
  }
}
