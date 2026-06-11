# Phase 5 — Observability, Database Backup & Production Operations

> **Status:** Not Started  
> **Estimated effort:** 1–2 weeks  
> **Dependencies:** Phase 0 (n8n secured), Phase 4 (CI/CD pipeline); production infrastructure (server, domain, TLS cert)  
> **Parallel:** Can overlap with Phase 3 (Mailbox OAuth) after Phase 4 is established

---

## Goal

Make OrvixFlow production-grade from an operational perspective. After this phase:
- Every error, exception, and job failure is captured and alertable
- The database has daily automated backups with a tested restore procedure
- Production deployment is fully documented, reproducible, and uses a secure `docker-compose.prod.yml`
- Runbooks exist for the most critical operational scenarios

---

## Why

Code quality and test coverage are strong. But without observability and backup, production is flying blind:

- A failing Hangfire job (e.g., `TrialExpirationJob`) would go unnoticed for hours or days
- A database crash without backups means permanent data loss
- Without structured logging, debugging a production incident requires reading raw Docker logs line by line
- Without a production compose file, every deployment requires manual environment configuration

This phase transforms a working application into a production-operable system.

---

## Scope

**Observability:**
- OpenTelemetry for .NET API (traces + metrics)
- Structured logging sink (replace stdout-only with Seq or Loki)
- Hangfire job failure alerting
- Uptime monitoring for health endpoints
- Sentry for frontend exception tracking

**Database Backup:**
- Automated `pg_dump` cron job (daily, encrypted, uploaded to MinIO or S3)
- Tested restore procedure
- Documented RPO/RTO and retention policy

**Deployment:**
- `docker-compose.prod.yml` — production-specific compose file
- TLS via Traefik or Caddy
- Domain and certificate setup
- Operational runbooks

---

## Out of Scope

- No Kubernetes/Helm (Docker Compose remains the deployment model)
- No multi-region deployment
- No blue-green or canary deployment strategies
- No log aggregation at petabyte scale — Seq or Loki for a single-node deployment is sufficient
- No database migration to managed PostgreSQL (RDS, Cloud SQL) — self-hosted Postgres remains

---

## Dependencies

- **Phase 0 complete** — n8n must be authenticated; env vars must be documented
- **Phase 4 complete** — CI/CD and Dockerfiles must exist before creating `docker-compose.prod.yml`
- **Production infrastructure:**
  - A Linux server (VPS, dedicated, or cloud VM) with Docker and Docker Compose installed
  - A domain name pointed at the server
  - Open ports: 80 (HTTP for TLS challenge), 443 (HTTPS)
  - SSH access for deployment

---

## Files / Components Likely Involved

| File | Task |
|---|---|
| `OrvixFlow.Api/Program.cs` | P5-1: OpenTelemetry registration |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | P5-1, P5-2: logging sink |
| `OrvixFlow.Api/appsettings.json` | P5-1, P5-2: telemetry config |
| `orvixflow-web/` | P5-5: Sentry integration |
| `docker-compose.prod.yml` | NEW — P5-9 |
| `scripts/backup.sh` | NEW — P5-6 |
| `runbooks/` | NEW — P5-11 |
| `.env.example` | MODIFY — add observability config vars |

---

## Implementation Tasks

### P5-1 — Add OpenTelemetry to .NET API

**Read before starting:** `OrvixFlow.Api/Program.cs`, existing service registrations

**Add NuGet packages:**
```bash
dotnet add OrvixFlow.Api package OpenTelemetry.Extensions.Hosting
dotnet add OrvixFlow.Api package OpenTelemetry.Instrumentation.AspNetCore
dotnet add OrvixFlow.Api package OpenTelemetry.Instrumentation.Http
dotnet add OrvixFlow.Api package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add OrvixFlow.Api package OpenTelemetry.Exporter.Otlp
```

**In `Program.cs`, after existing service registrations:**
```csharp
// P5-1: OpenTelemetry — traces and metrics
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["Telemetry:OtlpEndpoint"] ?? "http://localhost:4317")));
```

**In `appsettings.json`:**
```json
"Telemetry": {
  "OtlpEndpoint": ""
}
```

**In `.env.example`:**
```
# OpenTelemetry (optional — leave empty to disable)
OTEL_ENDPOINT=http://your-jaeger-or-grafana-host:4317
```

**In `docker-compose.prod.yml`:** map `Telemetry__OtlpEndpoint: ${OTEL_ENDPOINT:-}`

**Recommended telemetry backend:** Grafana Cloud (free tier) or a self-hosted Grafana + Tempo stack via Docker Compose.

**Verification:** Start API and send a request. Check that traces appear in the telemetry backend.

### P5-2 — Add Structured Logging Sink

**Replace stdout-only logging with Serilog + Seq (or Loki):**

```bash
dotnet add OrvixFlow.Api package Serilog.AspNetCore
dotnet add OrvixFlow.Api package Serilog.Sinks.Seq
dotnet add OrvixFlow.Api package Serilog.Sinks.Console
```

**In `Program.cs`, at the top (before `builder` is used):**
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrvixFlow")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Logging:SeqUrl"] ?? "http://localhost:5341",
        apiKey: context.Configuration["Logging:SeqApiKey"]));
```

**In `appsettings.json`:**
```json
"Logging": {
  "SeqUrl": "",
  "SeqApiKey": ""
}
```

**In `.env.example`:**
```
# Structured Logging — Seq
SEQ_URL=http://localhost:5341
SEQ_API_KEY=
```

**Alternative (if Seq is too heavyweight):** Use Serilog with `Serilog.Sinks.Loki` to send logs to Grafana Loki. Both are acceptable — choose based on the existing monitoring stack.

> ⚠️ **Security:** Do not log JWT secrets, API keys, or provider OAuth tokens. Review all existing `_logger.LogInformation/Warning/Error` calls that include request data and ensure no sensitive headers or body content is captured.

### P5-3 — Hangfire Job Failure Alerting

Hangfire already uses PostgreSQL storage and has a protected dashboard (`/hangfire` — SuperAdmin only). Add failure alerting:

**Option A (Webhook alert on job failure):**

Create `OrvixFlow.Api/Hangfire/JobFailureFilter.cs`:
```csharp
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace OrvixFlow.Api.Hangfire;

public class JobFailureAlertFilter : JobFilterAttribute, IApplyStateFilter
{
    private readonly ILogger<JobFailureAlertFilter> _logger;
    
    public JobFailureAlertFilter(ILogger<JobFailureAlertFilter> logger)
    {
        _logger = logger;
    }
    
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is FailedState failedState)
        {
            _logger.LogCritical(
                failedState.Exception,
                "Hangfire job {JobId} ({JobName}) failed. Reason: {Reason}",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job.Method.Name,
                failedState.Reason);
            
            // If using structured logging → Seq → Seq alert rule → this is sufficient.
            // Add a webhook call here if using PagerDuty/Slack alerting.
        }
    }
    
    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}
```

Register in `Program.cs`:
```csharp
GlobalJobFilters.Filters.Add(new JobFailureAlertFilter(
    app.Services.GetRequiredService<ILogger<JobFailureAlertFilter>>()));
```

**Option B (Simpler — rely on Seq/Loki alert rules):**
If using structured logging (P5-2), configure an alert in Seq or Loki that triggers when `Level=Critical` and `Message` contains `"Hangfire job"`. No code change needed.

**Recommendation:** Implement Option A (structured log) AND configure Option B (alert rule). Belt and suspenders.

### P5-4 — Uptime Monitoring for Health Endpoints

The following health endpoints already exist:
- `GET /health/rag` — checks RAG/pgvector connectivity
- `GET /health/storage` — checks MinIO/object storage connectivity

**Operational task — configure UptimeRobot, BetterStack, or equivalent:**

- [ ] Sign up for BetterStack (free tier) or UptimeRobot
- [ ] Add monitor for `https://your-domain.com/health/rag` — expected response: 200
- [ ] Add monitor for `https://your-domain.com/health/storage` — expected response: 200
- [ ] Add monitor for `https://your-domain.com/api/auth/login` (just ping the route, not a real login) — expected response: 405 (Method Not Allowed for GET) or any non-5xx
- [ ] Configure alert: email + Slack/PagerDuty on consecutive failures
- [ ] Set check interval: 1 minute

**No code change required.** The health check endpoints are already implemented and returning structured JSON responses.

### P5-5 — Sentry for Frontend Exception Tracking

**In `orvixflow-web/`, install Sentry:**
```bash
npx @sentry/wizard@latest -i nextjs
```

The Sentry Next.js wizard automatically:
- Creates `sentry.client.config.ts`, `sentry.server.config.ts`, `sentry.edge.config.ts`
- Updates `next.config.ts` with Sentry source maps
- Adds `instrumentation.ts` for server-side tracking

**After wizard completes:**
- [ ] Set `SENTRY_DSN` in `.env.example`
- [ ] Set `SENTRY_AUTH_TOKEN` as a GitHub Secret for source map uploads
- [ ] Configure alert rules in Sentry dashboard: email on new issue, Slack for P1 errors
- [ ] Add `SENTRY_DSN` to `docker-compose.prod.yml` (as web service env var)

**Verify:** Trigger a deliberate client-side error; confirm it appears in Sentry dashboard.

> ⚠️ **Privacy:** Sentry must be configured to scrub sensitive data (auth tokens, email addresses, passwords). Configure `beforeSend` in `sentry.client.config.ts` to redact sensitive keys.

### P5-6 — Automated Database Backup

Create `scripts/backup.sh`:
```bash
#!/bin/bash
# OrvixFlow — Automated PostgreSQL Backup
# Runs daily via cron. Encrypts and uploads to MinIO.
# Required env: POSTGRES_HOST, POSTGRES_USER, POSTGRES_DB, POSTGRES_PASSWORD,
#               MINIO_ENDPOINT, MINIO_ACCESS_KEY, MINIO_SECRET_KEY, MINIO_BACKUP_BUCKET

set -euo pipefail

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="orvixflow_${TIMESTAMP}.dump"
ENCRYPTED_FILE="${BACKUP_FILE}.gpg"

echo "[backup] Starting backup at ${TIMESTAMP}"

# Dump database
PGPASSWORD="${POSTGRES_PASSWORD}" pg_dump \
    -h "${POSTGRES_HOST}" \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    -Fc \
    -f "/tmp/${BACKUP_FILE}"

echo "[backup] Dump complete: /tmp/${BACKUP_FILE}"

# Encrypt with GPG symmetric encryption
gpg --batch --yes --passphrase "${BACKUP_ENCRYPTION_KEY}" \
    --symmetric --cipher-algo AES256 \
    -o "/tmp/${ENCRYPTED_FILE}" \
    "/tmp/${BACKUP_FILE}"

echo "[backup] Encryption complete"

# Upload to MinIO using mc (MinIO client)
mc alias set orvix "${MINIO_ENDPOINT}" "${MINIO_ACCESS_KEY}" "${MINIO_SECRET_KEY}"
mc cp "/tmp/${ENCRYPTED_FILE}" "orvix/${MINIO_BACKUP_BUCKET}/daily/${ENCRYPTED_FILE}"

echo "[backup] Uploaded to MinIO: ${MINIO_BACKUP_BUCKET}/daily/${ENCRYPTED_FILE}"

# Cleanup temp files
rm "/tmp/${BACKUP_FILE}" "/tmp/${ENCRYPTED_FILE}"

# Prune backups older than 30 days
mc find "orvix/${MINIO_BACKUP_BUCKET}/daily/" \
    --older-than 720h \
    --exec "mc rm {}"

echo "[backup] Old backups pruned. Backup complete."
```

**Cron setup on production server:**
```cron
# Daily backup at 02:00 UTC
0 2 * * * /opt/orvixflow/scripts/backup.sh >> /var/log/orvixflow-backup.log 2>&1
```

**Add to `.env.example`:**
```
# Database Backup
BACKUP_ENCRYPTION_KEY=REPLACE-WITH-A-SECURE-GPG-PASSPHRASE
MINIO_BACKUP_BUCKET=orvixflow-backups
```

**MinIO backup bucket setup:**
- [ ] Create `orvixflow-backups` bucket in MinIO with versioning enabled
- [ ] Set bucket to private (no public access)
- [ ] Consider a separate MinIO instance or an external S3-compatible service for backups (using the same MinIO as the app for backups means a server failure takes both data and backup)

### P5-7 — Test Database Restore

**Run the following restore test on a staging system (NOT production):**

```bash
# 1. Download the latest backup from MinIO
mc cp orvix/orvixflow-backups/daily/orvixflow_LATEST.dump.gpg /tmp/restore.dump.gpg

# 2. Decrypt
gpg --batch --passphrase "${BACKUP_ENCRYPTION_KEY}" \
    --decrypt /tmp/restore.dump.gpg > /tmp/restore.dump

# 3. Create a test database
PGPASSWORD="${POSTGRES_PASSWORD}" createdb -h "${POSTGRES_HOST}" -U "${POSTGRES_USER}" orvixflow_restore_test

# 4. Restore
PGPASSWORD="${POSTGRES_PASSWORD}" pg_restore \
    -h "${POSTGRES_HOST}" \
    -U "${POSTGRES_USER}" \
    -d orvixflow_restore_test \
    --no-owner \
    /tmp/restore.dump

# 5. Verify row counts match production
PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -U "${POSTGRES_USER}" orvixflow_restore_test \
    -c "SELECT relname, n_live_tup FROM pg_stat_user_tables ORDER BY n_live_tup DESC LIMIT 20;"

# 6. Clean up test database
PGPASSWORD="${POSTGRES_PASSWORD}" dropdb -h "${POSTGRES_HOST}" -U "${POSTGRES_USER}" orvixflow_restore_test
```

Document the row counts from step 5 in `runbooks/backup-restore.md` as the verified restore baseline.

### P5-8 — Document RPO/RTO and Retention Policy

Create `runbooks/backup-policy.md`:

```markdown
# OrvixFlow Database Backup Policy

## RPO (Recovery Point Objective)
Maximum acceptable data loss: **24 hours**
Basis: daily backups at 02:00 UTC

## RTO (Recovery Time Objective)
Maximum acceptable recovery time: **4 hours**
Basis: download + decrypt + restore time on production hardware

## Retention
- Daily backups: kept for 30 days
- Weekly backups (manual): keep 4 most recent (run manually on Sunday)
- Monthly backups (manual): keep for 1 year (run on 1st of month)

## Backup Location
MinIO bucket: orvixflow-backups
Path structure: daily/orvixflow_YYYYMMDD_HHMMSS.dump.gpg

## Encryption
Algorithm: AES-256 (GPG symmetric)
Key management: BACKUP_ENCRYPTION_KEY env var, stored in production secret manager
Key rotation: rotate annually, re-encrypt all retained backups after rotation

## Backup Verification
Monthly: restore a backup to a test database and verify row counts
```

### P5-9 — Create docker-compose.prod.yml

Create `docker-compose.prod.yml` for production use. This replaces `docker-compose.yml` on the production server:

```yaml
# OrvixFlow — Production Docker Compose
# Use with: docker compose -f docker-compose.prod.yml up -d
# All secrets must be set in the environment or a .env file on the server.

services:
  traefik:
    image: traefik:v3.0
    command:
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
      - "--certificatesresolvers.le.acme.tlschallenge=true"
      - "--certificatesresolvers.le.acme.email=${ACME_EMAIL}"
      - "--certificatesresolvers.le.acme.storage=/letsencrypt/acme.json"
      - "--entrypoints.web.http.redirections.entrypoint.to=websecure"
      - "--entrypoints.web.http.redirections.entrypoint.scheme=https"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - "/var/run/docker.sock:/var/run/docker.sock:ro"
      - "letsencrypt:/letsencrypt"
    restart: unless-stopped
    networks:
      - external

  orvix-db:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal  # internal only — never expose to external

  orvix-minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: ${MINIO_ACCESS_KEY}
      MINIO_ROOT_PASSWORD: ${MINIO_SECRET_KEY}
    volumes:
      - minio_data:/data
    restart: unless-stopped
    networks:
      - internal  # internal only

  orvix-n8n:
    image: n8nio/n8n:latest
    environment:
      - N8N_ENCRYPTION_KEY=${N8N_ENCRYPTION_KEY}
      - N8N_INSTANCE_OWNER_MANAGED_BY_ENV=true
      - N8N_INSTANCE_OWNER_EMAIL=${N8N_OWNER_EMAIL}
      - N8N_INSTANCE_OWNER_FIRST_NAME=${N8N_OWNER_FIRST_NAME}
      - N8N_INSTANCE_OWNER_LAST_NAME=${N8N_OWNER_LAST_NAME}
      - N8N_INSTANCE_OWNER_PASSWORD_HASH=${N8N_OWNER_PASSWORD_HASH}
      - WEBHOOK_URL=https://${N8N_DOMAIN}/
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.n8n.rule=Host(`${N8N_DOMAIN}`)"
      - "traefik.http.routers.n8n.entrypoints=websecure"
      - "traefik.http.routers.n8n.tls.certresolver=le"
    volumes:
      - n8n_data:/home/node/.n8n
    restart: unless-stopped
    networks:
      - external
      - internal

  orvix-api:
    image: ghcr.io/${GITHUB_REPO_OWNER}/orvixflow-api:latest
    environment:
      ConnectionStrings__DefaultConnection: "Host=orvix-db;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: orvixflow
      Jwt__Audience: orvixflow-web
      Stripe__SecretKey: ${STRIPE_SECRET_KEY}
      Stripe__WebhookSecret: ${STRIPE_WEBHOOK_SECRET}
      Email__Provider: ${EMAIL_PROVIDER:-Resend}
      Email__ResendApiKey: ${EMAIL_RESEND_API_KEY}
      Email__FromEmail: ${EMAIL_FROM_EMAIL}
      Email__FromName: ${EMAIL_FROM_NAME}
      Storage__Provider: MinIO
      Storage__MinIO__Endpoint: orvix-minio:9000
      Storage__MinIO__AccessKey: ${MINIO_ACCESS_KEY}
      Storage__MinIO__SecretKey: ${MINIO_SECRET_KEY}
      Storage__MinIO__Bucket: ${MINIO_BUCKET:-orvixflow}
      Automation__N8nBaseUrl: http://orvix-n8n:5678
      Automation__Key: ${AUTOMATION_KEY}
      Security__VirusScan__Provider: ${VIRUS_SCAN_PROVIDER:-Noop}
      Frontend__BaseUrl: https://${WEB_DOMAIN}
      Telemetry__OtlpEndpoint: ${OTEL_ENDPOINT:-}
      Logging__SeqUrl: ${SEQ_URL:-}
    depends_on:
      orvix-db:
        condition: service_healthy
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`${API_DOMAIN}`)"
      - "traefik.http.routers.api.entrypoints=websecure"
      - "traefik.http.routers.api.tls.certresolver=le"
      - "traefik.http.services.api.loadbalancer.server.port=5000"
    restart: unless-stopped
    networks:
      - external
      - internal

  orvix-web:
    image: ghcr.io/${GITHUB_REPO_OWNER}/orvixflow-web:latest
    environment:
      NEXTAUTH_URL: https://${WEB_DOMAIN}
      NEXTAUTH_SECRET: ${NEXTAUTH_SECRET}
      AUTH_SECRET: ${AUTH_SECRET}
      NEXT_PUBLIC_API_BASE_URL: https://${API_DOMAIN}
      GOOGLE_CLIENT_ID: ${GOOGLE_CLIENT_ID}
      GOOGLE_CLIENT_SECRET: ${GOOGLE_CLIENT_SECRET}
      AZURE_AD_CLIENT_ID: ${AZURE_AD_CLIENT_ID}
      AZURE_AD_CLIENT_SECRET: ${AZURE_AD_CLIENT_SECRET}
      SENTRY_DSN: ${SENTRY_DSN:-}
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.web.rule=Host(`${WEB_DOMAIN}`)"
      - "traefik.http.routers.web.entrypoints=websecure"
      - "traefik.http.routers.web.tls.certresolver=le"
      - "traefik.http.services.web.loadbalancer.server.port=3000"
    restart: unless-stopped
    networks:
      - external

volumes:
  postgres_data:
  minio_data:
  n8n_data:
  letsencrypt:

networks:
  external:
    driver: bridge
  internal:
    driver: bridge
    internal: true  # No external access to internal network
```

**Add to `.env.example`:**
```
# Production Deployment
ACME_EMAIL=admin@yourdomain.com  # Let's Encrypt email
API_DOMAIN=api.yourdomain.com
WEB_DOMAIN=app.yourdomain.com
N8N_DOMAIN=n8n.yourdomain.com
GITHUB_REPO_OWNER=your-github-username
```

### P5-10 — Document Domain Setup, TLS, and Port Mapping

Create `runbooks/production-setup.md`:

```markdown
# OrvixFlow Production Setup Runbook

## Prerequisites
- Ubuntu 22.04+ server (2+ CPU, 4GB+ RAM recommended)
- Docker and Docker Compose installed
- Domain pointed at server IP:
  - api.yourdomain.com → server IP
  - app.yourdomain.com → server IP
  - n8n.yourdomain.com → server IP (optional)

## First Deployment
1. SSH into production server
2. Clone repo: `git clone https://github.com/your-org/orvixflow.git /opt/orvixflow`
3. Create `/opt/orvixflow/.env` with all required vars (copy from .env.example)
4. `cd /opt/orvixflow`
5. `docker compose -f docker-compose.prod.yml up -d`
6. Wait for Traefik to obtain TLS certs (up to 5 minutes)
7. Verify: `curl https://api.yourdomain.com/health/rag`

## EF Core Migrations
Migrations run automatically on API startup (`db.Database.Migrate()` in Program.cs).
If a migration fails: check API logs: `docker compose -f docker-compose.prod.yml logs orvix-api`

## Updating to a New Version
```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d --remove-orphans
docker system prune -f
```

## TLS Certificate Renewal
Automatic via Traefik + Let's Encrypt. Traefik renews 30 days before expiry.
To verify cert: `curl -v https://api.yourdomain.com 2>&1 | grep expire`
```

### P5-11 — Create Operational Runbooks

Create the following in `runbooks/`:

**`runbooks/rollback.md`** — How to roll back a deployment
**`runbooks/backup-restore.md`** — How to restore from a database backup (see P5-7)
**`runbooks/migration-production.md`** — How to run EF migrations manually in production
**`runbooks/hangfire-stuck-job.md`** — How to recover from a stuck Hangfire job
**`runbooks/backup-policy.md`** — RPO/RTO and retention policy (see P5-8)

**`runbooks/rollback.md` template:**
```markdown
# Rollback Runbook

## Rollback via Docker image tag (recommended)
```bash
# On production server
cd /opt/orvixflow
# Pull a specific commit SHA
docker pull ghcr.io/your-org/orvixflow-api:<previous-sha>
docker pull ghcr.io/your-org/orvixflow-web:<previous-sha>

# Update compose to use specific tags (edit docker-compose.prod.yml)
# Then restart:
docker compose -f docker-compose.prod.yml up -d
```

## Rollback via git revert
If images are not available, revert the commit and let CI/CD redeploy.
```

---

## Architecture Rules

- `docker-compose.prod.yml` must use `internal: true` network for all non-public services (Postgres, MinIO)
- n8n must be behind Traefik authentication and require owner login via `N8N_INSTANCE_OWNER_*`
- All services must have `restart: unless-stopped`
- The `orvix-db` service must NOT expose any ports to the host — it is internal only
- OpenTelemetry exporter must use OTLP (not Jaeger direct) for vendor neutrality
- Sentry must scrub sensitive fields before sending events (configure `beforeSend`)
- Backup encryption key must be stored separately from the backup files — if using MinIO for backups, store the encryption key in a separate location

---

## Security Requirements

- Database port (5432) must NOT be exposed to the internet — use the internal Docker network only
- MinIO admin UI (port 9001) must NOT be exposed via Traefik without authentication
- All TLS certificates must be renewed automatically via Let's Encrypt
- n8n admin UI must require authentication via owner login bootstrap
- Backup encryption key must not be stored in the same location as the backup files

---

## Tests Required

### Manual Validation

All validation for this phase is operational and manual:

- [ ] P5-1: Send a test API request; confirm trace appears in Grafana/Jaeger
- [ ] P5-2: Trigger an error; confirm structured log entry appears in Seq/Loki with correct level and context fields
- [ ] P5-3: Manually fail a Hangfire job (from dashboard); confirm alert is triggered
- [ ] P5-4: Verify uptime monitor shows green for both health endpoints
- [ ] P5-5: Trigger a frontend error; confirm it appears in Sentry dashboard
- [ ] P5-6: Run `scripts/backup.sh` manually; confirm backup file appears in MinIO
- [ ] P5-7: Run the full restore test (see P5-7); confirm row counts match
- [ ] P5-9: `docker compose -f docker-compose.prod.yml up -d` must start all services successfully
- [ ] P5-9: `https://app.yourdomain.com` must load and show a valid TLS certificate
- [ ] P5-9: `https://api.yourdomain.com/health/rag` must return 200
- [ ] P5-9: Internal services (Postgres, MinIO) must NOT be accessible from the internet
- [ ] P5-10: TLS certificate is valid and not self-signed

### Backend Tests (existing — must still pass)

```bash
dotnet test
```

---

## Validation Checklist

- [ ] OpenTelemetry traces visible in telemetry backend
- [ ] Structured logs (JSON) visible in Seq or Loki
- [ ] Hangfire job failure triggers structured log + alert
- [ ] UptimeRobot/BetterStack monitors show green for health endpoints
- [ ] Sentry captures frontend exceptions
- [ ] Daily backup runs successfully at 02:00 UTC
- [ ] Backup restore test completes without errors
- [ ] `docker-compose.prod.yml` starts all services with TLS
- [ ] `api.yourdomain.com` returns valid HTTPS response
- [ ] `app.yourdomain.com` returns valid HTTPS with Next.js app
- [ ] Postgres port NOT accessible from internet
- [ ] MinIO admin UI NOT accessible without authentication
- [ ] n8n admin UI requires owner login
- [ ] All runbooks exist and are accurate
- [ ] `dotnet test` still passes (0 failures)

---

## Definition of Done

1. Every application error and job failure generates a structured log event and an alert
2. Daily database backups run automatically and have been tested with a successful restore
3. `docker-compose.prod.yml` starts all services with TLS, internal networking, and authenticated admin UIs
4. All six runbooks exist and are accurate
5. Uptime monitoring is active for health endpoints
6. All backend and frontend tests still pass

---

## Common Mistakes

1. **Exposing Postgres on 0.0.0.0:5432** — never map the database port to the host in production. Use Docker internal network only.
2. **Using the same MinIO for app data and backups** — if the server dies, both data and backup are lost. Use an external S3 bucket or a separate MinIO instance for backups.
3. **Not testing the backup restore** — a backup that cannot be restored is worthless. Test it monthly.
4. **Forgetting `restart: unless-stopped`** — without this, services do not restart after server reboot
5. **Not scrubbing sensitive data in Sentry** — without `beforeSend` filtering, auth tokens or email addresses could appear in Sentry event data
6. **Traefik on the internal network** — Traefik must be on the external network to receive internet traffic; move it to internal only if you add a separate external load balancer

---

## Handoff — Phase 5 Completes the Production Launch Track

After Phase 5:
- Update `tasks/production/progress.md` with completion dates for all phases
- The production environment is fully operational
- Phases 3 (Mailbox OAuth) can continue in parallel or sequentially

**The minimum viable production launch requires Phases 0, 1, 2, 4, and 5.**  
**Phase 3 (Mailbox OAuth) is a feature track that can ship after initial production launch.**
