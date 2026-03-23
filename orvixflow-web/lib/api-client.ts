// Helper utility for making API calls to our .NET backend
import { auth } from "@/auth";

export async function fetchApi(endpoint: string, options: RequestInit = {}) {
  // Get the session on the server
  const session = await auth();
  
  const headers = new Headers(options.headers);
  headers.set("Content-Type", "application/json");

  // Inject our JWT Bearer token into all requests
  if (session?.apiToken) {
    headers.set("Authorization", `Bearer ${session.apiToken}`);
  }

  const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}${endpoint}`, {
    ...options,
    headers,
  });

  return res;
}
