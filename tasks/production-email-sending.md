# Production Email Sending — Implementation Plan

## Goal

Replace the current console-log mock with a real transactional email provider that actually delivers emails to users' inboxes. This covers:

- Fixing a known bug in `SmtpEmailService` (always requires credentials, breaks without auth)
- Adding **Resend** as the recommended production provider (HTTP API, generous free tier)
- Adding **Gmail SMTP** as an alternative (pure .NET, no third-party service, free)
- Keeping SMTP as the fallback for any SMTP relay
- Wiring all config cleanly through environment variables

---

## Provider Options

| Provider | Free Tier | Requires signup | Pure .NET |
|---|---|---|---|
| **Gmail SMTP** | 500 emails/day | Google account (already have) | ✅ Yes |
| **Resend** | 3,000/month, 100/day | Resend account | ✅ Yes (HTTP) |
| SendGrid | 100/day | SendGrid account | ✅ Yes (HTTP) |
| Mailgun | 1,000/month | Mailgun account | ✅ Yes (HTTP) |

**Gmail SMTP** — no new service, uses your existing Google account, pure .NET `SmtpClient`. Limit is 500/day which is plenty for early-stage SaaS.

**Resend** — recommended when you need a custom `From` domain, delivery analytics, or higher volume.

---

## Architecture

```
IEmailService
├── ConsoleEmailService (MockEmailService)  → dev stub (current, keep as-is)
├── SmtpEmailService                        → fixed + works with Gmail, Mailhog, any SMTP relay
└── ResendEmailService                      → NEW: HTTP-based sending via Resend API
```

Provider is selected at startup via `Email:Provider` config key: `"Console"`, `"Smtp"`, or `"Resend"`.

---

## Implementation Steps

### Step 1: Fix existing `SmtpEmailService` bug

**File:** `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs`

Current bug: `Credentials` is always set, even when `SmtpUser` is empty → crashes against Mailhog.

**Fix:**
```csharp
if (!string.IsNullOrEmpty(_options.SmtpUser))
{
    client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPass);
}
```

Also: `System.Net.Mail.SmtpClient` is deprecated. Wrap in `#pragma warning disable SYSLIB0021` or migrate to `MailKit` long-term (separate task).

---

### Step 2: Add `ResendEmailService`

**New file:** `OrvixFlow.Infrastructure/Services/ResendEmailService.cs`

Uses Resend's REST API via `HttpClient`. No SDK needed.

```csharp
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly EmailOptions _options;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailOptions> options,
        ILogger<ResendEmailService> logger)
    {
        _http = httpClientFactory.CreateClient("resend");
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var payload = new
        {
            from = $"{_options.FromName} <{_options.FromEmail}>",
            to = new[] { to },
            subject,
            html = body
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("emails", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Resend API error {Status}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Resend API returned {response.StatusCode}");
        }

        _logger.LogInformation("Email sent via Resend to {To}", to);
    }
}
```

---

### Step 3: Update `EmailOptions` to include Resend key

**File:** `OrvixFlow.Infrastructure/Services/EmailOptions.cs`

Add:
```csharp
public string? ResendApiKey { get; set; }
```

---

### Step 4: Register `ResendEmailService` + HttpClient in DI

**File:** `OrvixFlow.Infrastructure/DependencyInjection.cs`

```csharp
// Register named HttpClient for Resend
services.AddHttpClient("resend", client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    var apiKey = configuration["Email:ResendApiKey"]
        ?? throw new InvalidOperationException("Email:ResendApiKey is required when Email:Provider=Resend");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
});

// Switch provider in DI
var emailProvider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Console";
if (emailProvider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
    services.AddScoped<IEmailService, ResendEmailService>();
else if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
    services.AddScoped<IEmailService, SmtpEmailService>();
else
    services.AddScoped<IEmailService, MockEmailService>();
```

> Note: The `AddHttpClient("resend")` registration must happen unconditionally (or guarded) to avoid startup errors when the provider is not Resend.

---

### Step 5: Update `appsettings.json` with Email defaults section

**File:** `OrvixFlow.Api/appsettings.json`

```json
"Email": {
  "Provider": "Console",
  "FromEmail": "noreply@orvixflow.local",
  "FromName": "OrvixFlow",
  "SmtpHost": "",
  "SmtpPort": 587,
  "SmtpUser": "",
  "SmtpPass": "",
  "UseSsl": false,
  "ResendApiKey": ""
}
```

---

### Step 6: Add environment variables to `.env` and `docker-compose.yml`

**`.env`** (gitignored):
```bash
# Email
EMAIL_PROVIDER=Resend
EMAIL_FROM=onboarding@resend.dev        # Resend sandbox domain (no DNS setup needed)
EMAIL_FROM_NAME=OrvixFlow
RESEND_API_KEY=re_your_key_here
```

**`docker-compose.yml`** under `orvix-api`:
```yaml
Email__Provider: ${EMAIL_PROVIDER:-Console}
Email__FromEmail: ${EMAIL_FROM:-noreply@orvixflow.local}
Email__FromName: ${EMAIL_FROM_NAME:-OrvixFlow}
Email__ResendApiKey: ${RESEND_API_KEY}
```

---

### Step 7: Get Resend API key (2 minutes)

1. Go to [https://resend.com](https://resend.com) → Sign up
2. Dashboard → API Keys → Create Key
3. For testing: send from `onboarding@resend.dev` to **your own email only** (sandbox domain — no DNS needed)
4. For production: add your own domain under **Domains** and verify DNS

---

## Alternative: Gmail SMTP (pure .NET, no third-party service)

This uses the **existing `SmtpEmailService`** — no new code beyond the credentials bug fix.

### Setup steps

1. **Enable 2-Factor Authentication** on your Google account (required for App Passwords)
2. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
3. Create an App Password → name it `OrvixFlow` → copy the 16-character key
4. Add to `.env`:

```bash
# Email — Gmail SMTP
EMAIL_PROVIDER=Smtp
EMAIL_SMTP_HOST=smtp.gmail.com
EMAIL_SMTP_PORT=587
EMAIL_SMTP_USER=your.email@gmail.com
EMAIL_SMTP_PASS=xxxx xxxx xxxx xxxx
EMAIL_FROM=your.email@gmail.com
EMAIL_FROM_NAME=OrvixFlow
EMAIL_USE_SSL=false
```

5. Add to `docker-compose.yml` under `orvix-api`:

```yaml
Email__Provider: ${EMAIL_PROVIDER:-Console}
Email__SmtpHost: ${EMAIL_SMTP_HOST}
Email__SmtpPort: ${EMAIL_SMTP_PORT:-587}
Email__SmtpUser: ${EMAIL_SMTP_USER}
Email__SmtpPass: ${EMAIL_SMTP_PASS}
Email__FromEmail: ${EMAIL_FROM:-noreply@orvixflow.local}
Email__FromName: ${EMAIL_FROM_NAME:-OrvixFlow}
Email__UseSsl: ${EMAIL_USE_SSL:-false}
```

### Gotchas

- `From` address must match `SmtpUser` (Gmail enforces this)
- App Password ≠ your Google account password
- Limit: ~500 emails/day via Gmail SMTP (enough for dev and early prod)
- Port 587 + `EnableSsl=false` uses **STARTTLS** (correct for Gmail), not SSL on port 465

### When to switch to Resend instead

- You want to send from a custom domain (`no-reply@yourdomain.com`)
- Volume exceeds Gmail's 500/day limit
- You need delivery analytics or webhooks

---

## Files to Change

| File | Change |
|---|---|
| `OrvixFlow.Infrastructure/Services/SmtpEmailService.cs` | Fix credentials bug |
| `OrvixFlow.Infrastructure/Services/EmailOptions.cs` | Add `ResendApiKey` property |
| `OrvixFlow.Infrastructure/Services/ResendEmailService.cs` | **NEW** — Resend HTTP implementation |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Register ResendEmailService + HttpClient |
| `OrvixFlow.Api/appsettings.json` | Add Email section documentation |
| `.env` | Add email provider vars |
| `docker-compose.yml` | Pass email env vars to `orvix-api` |

---

## Verification Plan

1. Set `EMAIL_PROVIDER=Resend` and `RESEND_API_KEY=re_...` in `.env`
2. `docker compose up --build -d`
3. Register a new user at `http://localhost:3000/register`
4. Check your real inbox (or Resend dashboard's **Emails** tab)
5. Click the verification link → see "Email verified!" screen
6. Log in → success

---

## Acceptance Criteria

- [ ] `SmtpEmailService` works with or without credentials
- [ ] Gmail SMTP config works end-to-end (register → real inbox → verify → login)
- [ ] `ResendEmailService` sends real emails when `Provider=Resend`
- [ ] All email config comes from environment — nothing hardcoded
- [ ] Startup fails with a clear error if `Provider=Resend` but `ResendApiKey` is missing
- [ ] `Provider=Console` still works for local dev without any config
