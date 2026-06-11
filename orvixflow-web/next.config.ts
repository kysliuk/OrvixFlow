import type { NextConfig } from "next";
import { withSentryConfig } from "@sentry/nextjs";

function buildContentSecurityPolicy() {
  const connectSources = [
    "'self'",
    process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080",
    "https://accounts.google.com",
    "https://login.microsoftonline.com",
  ];

  return [
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: https: blob:",
    "font-src 'self' https:",
    `connect-src ${connectSources.join(" ")}` ,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self' https://accounts.google.com https://login.microsoftonline.com",
    "frame-ancestors 'none'",
  ].join("; ");
}

const nextConfig: NextConfig = {
  async headers() {
    return [
      {
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
            key: "Permissions-Policy",
            value: "camera=(), microphone=(), geolocation=()",
          },
          {
            key: "Content-Security-Policy",
            value: buildContentSecurityPolicy(),
          },
        ],
      },
    ];
  },
};

export default withSentryConfig(nextConfig, {
  silent: true,
  widenClientFileUpload: true,
  hideSourceMaps: true,
  disableLogger: true,
});
