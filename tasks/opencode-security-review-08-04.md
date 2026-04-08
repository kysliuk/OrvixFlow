# Security Review Report — OrvixFlow

**Reviewed:** 2026-04-08  
**Reviewer:** Security Reviewer Agent  
**Risk Level:** HIGH

---

## Executive Summary

| Category | Count |
|----------|-------|
| **Critical Issues** | 7 |
| **High Issues** | 8 |
| **Medium Issues** | 5 |
| **Low Issues** | 3 |

**Risk Level: HIGH** — The project has multiple critical issues that expose hardcoded secrets, insecure configurations, and OWASP Top 10 vulnerabilities. Fix critical issues before any production deployment.

---

## Critical Issues (Fix Immediately)

### 1. Hardcoded Secrets in Configuration Files
**Severity:** CRITICAL  
**Category:** Sensitive Data Exposure / Secrets Management  
**Locations:** 
- `OrvixFlow.Api/appsettings.Development.json:11-26`
- `docker-compose.yml:43,63-73`

**Issue:** Production API keys, OAuth credentials, database passwords, and JWT secrets are hardcoded directly in configuration files and docker-compose that may be committed to version control.

**Evidence:**
```json
// appsettings.Development.json
"ApiKey": "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"
"Secret": "dev-super-secret-jwt-key-32-chars-minimum-length-required-here!"
```

```yaml
# docker-compose.yml
GOOGLE_CLIENT_SECRET: "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"
AZURE_AD_CLIENT_SECRET: "tyu8Q~4v_FMRDRn8R~XDaTISfW2dUe61HXhLWaZd"
NEXTAUTH_SECRET: dev-nextauth-secret-change-me
```

**Impact:** 
- Complete account takeover via leaked OAuth credentials
- Unauthorized access to all JWT tokens
- Full database access with leaked credentials

**Remediation:**
Create a `.env.production` file and load all secrets from environment variables:
```csharp
// appsettings.json - use references
"Jwt": {
  "Secret": "${JWT_SECRET}",
  "Issuer": "orvixflow",
  "Audience": "orvixflow-web"
}
```

Docker Compose should use env_file with secrets:
```yaml
services:
  orvix-api:
    env_file:
      - .env.production
```

---

### 2. Missing Security Headers
**Severity:** CRITICAL  
**Category:** Security Misconfiguration  
**Location:** `OrvixFlow.Api/Program.cs`

**Issue:** No security headers are configured in the ASP.NET Core pipeline, exposing the application to XSS, clickjacking, MIME sniffing, and other client-side attacks.

**Missing Headers:**
- `Content-Security-Policy`
- `X-Frame-Options`
- `X-Content-Type-Options`
- `Strict-Transport-Security`
- `Referrer-Policy`

**Remediation:**
Add to Program.cs after `app.UseRateLimiter()`:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});
```

For production, add HSTS:
```csharp
app.UseHsts();
```

---

### 3. OAuth Account Linking Without Verification
**Severity:** CRITICAL  
**Category:** Broken Authentication  
**Location:** `OrvixFlow.Infrastructure/Auth/AuthService.cs:107-123`

**Issue:** If a user tries to sign in with OAuth using an email that already exists with a different provider, the accounts are automatically linked without email verification.

```csharp
// Lines 113-122 - automatic account linking
if (byEmail != null)
{
    // Upgrade account to OAuth if it was previously local
    byEmail.OAuthProvider = provider;
    byEmail.ExternalId = externalId;
    // No email verification sent!
    await _db.SaveChangesAsync();
}
```

**Impact:** Account takeover via OAuth provider that the attacker controls.

**Remediation:**
1. Require email verification before linking accounts
2. Send verification email with token
3. Only link after token validation
4. Notify user via existing email of new provider linkage

```csharp
if (byEmail != null)
{
    // Create verification token instead of auto-linking
    var verification = new EmailVerification {
        UserId = byEmail.Id,
        NewProvider = provider,
        NewExternalId = externalId,
        Token = GenerateSecureToken(),
        ExpiresAt = DateTime.UtcNow.AddHours(24)
    };
    _db.EmailVerifications.Add(verification);
    _db.SaveChangesAsync();
    // Send verification email
    return new AuthResult(false, Error: "Email verification required to link accounts");
}
```

---

### 4. Next.js Missing Security Configuration
**Severity:** CRITICAL  
**Category:** Security Misconfiguration  
**Location:** `orvixflow-web/next.config.ts`

**Issue:** The Next.js configuration is essentially empty with no security hardening.

**Remediation:**
```typescript
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "X-XSS-Protection", value: "1; mode=block" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
        ],
      },
    ];
  },
  async rewrites() {
    return [];
  },
  // Prevent exposure of file system
  fs: {
    paths: ["../../.env*"],
  },
};

export default nextConfig;
```

---

### 5. No Rate Limiting on Authentication Endpoints
**Severity:** CRITICAL  
**Category:** Broken Authentication  
**Location:** `OrvixFlow.Api/Program.cs:88-98`

**Issue:** Only file upload endpoint has rate limiting (10 req/min). Login, registration, and OAuth provision endpoints are unprotected.

**Remediation:**
Add fixed window rate limiting for auth:
```csharp
options.AddFixedWindowLimiter(policyName: "auth", options =>
{
    options.PermitLimit = 5;
    options.Window = TimeSpan.FromMinutes(1);
    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    options.QueueLimit = 0;
});
```

Apply to auth controller:
```csharp
[RateLimiterPolicy("auth")]
public class AuthController : ControllerBase
```

---

### 6. Long JWT Token Expiration
**Severity:** CRITICAL  
**Category:** Broken Authentication  
**Location:** `AuthService.cs:220`

**Issue:** JWT tokens expire after 7 days (`DateTime.UtcNow.AddDays(7)`). Industry best practice is ≤1 hour for access tokens.

**Impact:** Compromised tokens remain valid for up to 7 days.

**Remediation:**
Change to short-lived tokens:
```csharp
expires: DateTime.UtcNow.AddHours(1),  // Access token: 1 hour
```
Consider implementing refresh token rotation for seamless re-authentication.

---

### 7. No Password Strength Validation
**Severity:** CRITICAL  
**Category:** Broken Authentication  
**Location:** `AuthService.cs:32` (RegisterAsync)

**Issue:** No minimum password complexity requirements. Any password, even empty string, is accepted.

**Remediation:**
Add validation:
```csharp
if (password.Length < 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
{
    return new AuthResult(false, Error: "Password must be at least 8 characters with uppercase, lowercase, and digit");
}
```

---

## High Priority Issues

### 8. No Email Verification Required
**Severity:** HIGH  
**Location:** `AuthService.cs` (RegisterAsync)

**Issue:** New user accounts are immediately active without email verification. This allows fake accounts and spam.

**Remediation:** Require email verification before account activation.

---

### 9. Missing CSRF Protection
**Severity:** HIGH  
**Location:** Next.js + API

**Issue:** No CSRF tokens implemented for state-changing operations.

**Remediation:** Implement SameSite cookies and CSRF tokens.

---

### 10. JWT Secret Too Short
**Severity:** HIGH  
**Location:** `appsettings.Development.json:15`

**Issue:** The JWT secret "dev-super-secret-jwt-key-32-chars-minimum-length-required-here!" is 59 characters but may not meet entropy requirements.

**Remediation:** Use cryptographically secure random secrets (256-bit minimum).

---

### 11. Insufficient Audit Logging
**Severity:** HIGH  
**Location:** Throughout

**Issue:** Security-relevant events (login attempts, password changes, permission changes) are not logged.

**Remediation:** Add structured audit logging for all security events.

---

### 12. Database Connection String in Config
**Severity:** HIGH  
**Location:** `docker-compose.yml:43`

**Issue:** Database password in connection string may be logged or exposed.

**Remediation:** Use environment variables for all connection parameters.

---

### 13. Missing Input Validation on User Input
**Severity:** HIGH  
**Location:** Various controllers

**Issue:** User input fields don't have proper validation attributes.

**Remediation:** Add `[Required]`, `[StringLength]`, `[EmailAddress]` attributes to DTOs.

---

### 14. No Account Lockout Policy
**Severity:** HIGH  
**Location:** `AuthService.cs` (LoginAsync)

**Issue:** No protection against brute force attacks on login.

**Remediation:** Implement account lockout after N failed attempts.

---

### 15. N+1 Query Vulnerability Potential
**Severity:** HIGH  
**Location:** Various repositories

**Issue:** May lead to DoS via excessive database queries.

**Remediation:** Review queries and add pagination/lazy loading safeguards.

---

## Medium Priority Issues

### 16. Error Messages May Leak Information
**Severity:** MEDIUM  
**Location:** Exception handlers

**Issue:** Error responses may expose stack traces or internal details.

**Remediation:** Return generic error messages in production.

---

### 17. CORS Allows All Origins
**Severity:** MEDIUM  
**Location:** `Program.cs`

**Issue:** `AllowAnyOrigin()` configured. Should restrict to known origins.

**Remediation:** Use specific allowed origins from configuration.

---

### 18. Session Management Improvements Needed
**Severity:** MEDIUM  
**Location:** NextAuth config

**Issue:** Consider additional session security options (encrypted token, rotation).

**Remediation:** Enable advanced session security features.

---

### 19. File Upload Security
**Severity:** MEDIUM  
**Location:** File upload endpoint

**Issue:** Ensure file type validation and size limits enforced.

**Remediation:** Validate MIME types and max file size on server side.

---

### 20. Logging Sensitive Data Risk
**Severity:** MEDIUM  
**Location:** Various services

**Issue:** Ensure passwords, tokens, PII not logged inadvertently.

**Remediation:** Audit logging calls for sensitive data exposure.

---

## Low Priority Issues

### 21. Missing Security.txt
**Severity:** LOW  
**Location:** Public endpoint

**Issue:** Consider adding security.txt for vulnerability reporting.

---

### 22. Dependency Updates Needed
**Severity:** LOW  
**Location:** Package.json, .csproj

**Issue:** Check for outdated dependencies.

**Remediation:** Run `npm audit` and `dotnet list package` regularly.

---

### 23. HTTPS Enforcement
**Severity:** LOW  
**Location:** Configuration

**Issue:** Ensure HTTPS enforced in production via redirect.

**Remediation:** Configure HTTPS redirect in production only.

---

## Remediation Roadmap

### Phase 1: Critical Fixes (Blocker)
1. Move secrets to environment variables / `.env.production`
2. Add security headers to ASP.NET Core and Next.js
3. Implement email verification before OAuth account linking
4. Add rate limiting to auth endpoints

### Phase 2: Authentication Hardening
1. Reduce JWT expiration to 1 hour
2. Add password strength validation (min 8 chars, mixed case, numbers, special chars)
3. Consider refresh token rotation

### Phase 3: Additional Security
1. Enable HSTS
2. Configure CSP headers
3. Add comprehensive audit logging
4. Run dependency vulnerability scans

---

**Note:** Critical issues are blockers. Do not proceed to production until Phase 1 issues are resolved.
