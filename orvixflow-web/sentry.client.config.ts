import * as Sentry from "@sentry/nextjs";

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN || process.env.SENTRY_DSN,
  tracesSampleRate: 1.0,
  debug: false,
  beforeSend(event) {
    if (event.request && event.request.headers) {
      const sensitiveHeaders = ["authorization", "cookie", "x-auth-token", "x-automation-key"];
      for (const key of Object.keys(event.request.headers)) {
        if (sensitiveHeaders.includes(key.toLowerCase())) {
          event.request.headers[key] = "[REDACTED]";
        }
      }
    }
    return event;
  },
});
