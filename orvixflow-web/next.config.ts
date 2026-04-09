import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // F-32 FIX: Add HTTP security headers to protect against XSS, clickjacking,
  // MIME sniffing, and enforce strict referrer policy.
  async headers() {
    return [
      {
        // Apply to all routes
        source: "/:path*",
        headers: [
          {
            key: "X-Content-Type-Options",
            value: "nosniff",
          },
          {
            key: "X-Frame-Options",
            value: "DENY",
          },
          {
            key: "X-XSS-Protection",
            value: "1; mode=block",
          },
          {
            key: "Referrer-Policy",
            value: "strict-origin-when-cross-origin",
          },
          {
            // Permissions Policy restricts access to browser features.
            // Camera, microphone, and geolocation are disabled by default.
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=()",
          },
          // Content-Security-Policy should be audited and tightened once all
          // inline scripts are moved to separate files. For now, a basic
          // restrictive policy is better than none.
          // {
          //   key: "Content-Security-Policy",
          //   value: "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self';",
          // },
        ],
      },
    ];
  },
};

export default nextConfig;
