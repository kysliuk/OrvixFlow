# OrvixFlow - Project Overview

## Solution Structure

```
OrvixFlow/
├── OrvixFlow.sln
├── OrvixFlow.Api/           # ASP.NET Core Web API
├── OrvixFlow.Core/          # Domain entities, interfaces, models
├── OrvixFlow.Infrastructure/# Data access, services, AI integration
├── OrvixFlow.Tests/         # xUnit tests
└── orvixflow-web/           # Next.js 16 frontend
```

## Technology Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Backend | .NET | 9.0 |
| Frontend | Next.js | 16.2.1 |
| Auth | NextAuth | 5.0.0-beta.30 |
| Database | PostgreSQL + pgvector | Latest |
| AI | Semantic Kernel | Latest |
| Background Jobs | Hangfire | PostgreSQL storage |
| ORM | Entity Framework Core | 9.0 |

## Docker Services

| Service | Port | Purpose |
|---------|------|---------|
| orix-db | 5432 | PostgreSQL with pgvector |
| orix-n8n | 5678 | n8n automation engine |
| orix-api | 8080 | .NET API |
| orix-web | 3000 | Next.js frontend |

## Development Commands

```bash
# Backend
dotnet build
dotnet run --project OrvixFlow.Api

# Frontend
cd orvixflow-web && npm run dev

# Run tests
dotnet test

# Database migrations
dotnet ef migrations list --project OrvixFlow.Infrastructure
dotnet ef database update --project OrvixFlow.Infrastructure
```

## Key Configuration

Environment variables (or appsettings.json):
- `Jwt:Secret` - JWT signing key (min 32 chars)
- `Jwt:Issuer` - Default: "orvixflow"
- `Jwt:Audience` - Default: "orvixflow-web"
- `AI:Provider` - "OpenAI", "Mock", or "Groq"
- `Automation:N8nBaseUrl` - n8n instance URL
- `ConnectionStrings:DefaultConnection` - PostgreSQL connection

- `AI:Ingestion:ChunkSize` - Default: 800
- `AI:Ingestion:ChunkOverlap` - Default: 150

## RAG Assistant Extension (Status: Phase 5 Completed — Production Ready)
Fully instrumented, secured, and tested multi-modal ingestion and hybrid retrieval pipeline:
- PDF (PdfPig), DOCX (OpenXML), Image (ImageSharp) parsing with background ingestion via Hangfire
- Hybrid Search (pgvector + FTS) with Reciprocal Rank Fusion (RRF) and LLM reranking
- Image-aware RAG: extracting, indexing, and retrieving relevant images with citation tags
- Semantic Kernel integration with resilient InMemory fallbacks
- Multi-tenant file storage for documents and images
- **Phase 5 Hardening:**
  - Native .NET 8+ rate limiting on `/api/v1/knowledge/upload` (10 req/min)
  - MIME type & file size (10MB) validation + pluggable `IVirusScanService` hook
  - `IRagMetricsCollector` persisting retrieval analytics to `AuditTrail`
  - Dedicated `/health/rag` endpoint for pgvector + embedding service monitoring

## Recent Development Activity (Last 7 Days)

### Plan & Subscription System (Completed)
- Full billing/subscription system with 5-tier plan catalog (Free → Enterprise)
- Plan entitlements (tokens, API requests, storage, KBs) with per-company overrides
- Module access control via plan inclusions with per-company grant/suppress overrides
- Trial expiration job (Hangfire, every 6 hours) — auto-downgrades to Free
- Seat limit enforcement on plan changes and user invites
- Admin panel: company detail with 3 tabs (Overview, Entitlements, Modules), audit log, module definitions CRUD
- Company actions: cancel subscription, change plan, reactivate
- **Bug fix:** Added `IgnoreQueryFilters()` to `CompanySubscriptionService` and `EntitlementResolver` — admin queries were blocked by tenant query filter when viewing other companies

### n8n Provisioning (Completed)
- `N8nProvisioningService` — auto-provisions workflows and credentials in n8n
- OAuth setup alert and provisioning status tracking in inbox settings UI
- Background jobs for provisioning and cleanup

### UI/UX Improvements
- Dark mode support for select/option elements in global CSS
- Company name editing in settings
- Global role field conditionally rendered only when user has a non-empty global role
- Admin plan detail page with full plan management modal

### Database & Auth
- Database initialization with seeded plan catalog, entitlements, module inclusions
- Global vs company role formalization — `User.Role` only for platform admins, company roles in `UserCompanyMembership.CompanyRole`
- Profile update endpoint (`UpdateProfile`)
- Improved auth error handling and configuration

### RAG Cleanup & Maintenance (Completed)
- Removed all `Console.WriteLine` debug noise from `OrganizationController.cs` (8 instances → `ILogger<OrganizationController>.LogDebug`)
- Bumped `SixLabors.ImageSharp` from 3.1.5 → 3.1.12, resolving CVE warnings (NU1903/NU1902)
- Created `MockEmbeddingGenerator` implementing `IEmbeddingGenerator<string, Embedding<float>>` for Phase C migration prep
- Test count increased to 314 (all tests passing)

### Document Ingestion Pipeline Fixes (2026-04-08)
- **Tenant Context Fix:** Created `ITenantProviderFactory` to properly set tenant context in background jobs
- **Duplicate Prevention:** Pipeline now fetches existing document by ID before processing
- **Concurrency Fix:** Changed from navigation property `document.Chunks.Add()` to direct `DbSet.Add()` to prevent UPDATE vs INSERT conflicts
- **Chunk Cleanup:** Added existing chunk cleanup before re-processing (handles retries/duplicates)
- **Logging:** Comprehensive logging throughout ingestion pipeline for debugging
- **Error Handling:** Fixed error handling with fresh queries in catch blocks

### Security Hardening Phase 4 (2026-04-14)
### Security Hardening Phase 4 (2026-04-14)
- **F-05:** Invite token no longer returned in API response (sent via email instead)
- **F-13:** Text ingestion now validates max length (100,000 characters)
- **F-18:** Production startup warning when virus scanning is disabled (Noop provider)
- **F-26:** Audit trail already implemented for destructive admin actions (Cancel, Suspend, Reactivate)
- **Test count:** 314 tests passing

### Security Hardening (2026-04-09)
- **Phase 1 Remediation Complete:**
  - F-09: Removed `X-Tenant-ID` header fallback from TenantProvider (verified already fixed)
  - F-10: Added audit logging for admin impersonation via `X-Impersonate-Tenant` header
  - F-15: Moved all secrets from docker-compose.yml to `.env` file (gitignored), created `.env.example`
  - F-17: Removed JWT secret placeholder from `appsettings.json` (must use env var)
  - F-31: Removed Groq API key from `appsettings.Development.json` (must use env var)
  - F-02: Fixed OAuth email linking to reject conflicts (verified already fixed)

- **Phase 2 Remediation Complete:**
  - F-32: HTTP security headers added to API (middleware) and Frontend (next.config.ts)
  - F-11: File MIME validation via magic bytes (`FileSignatureValidator`)
  - F-12: Filename sanitization with GUID naming + path traversal prevention
  - F-14: File deletion error logged (no longer silent)
  - F-22: Hangfire dashboard protected with SuperAdmin JWT authorization filter
  - F-01: JWT lifetime shortened from 7 days to 60 minutes
  - F-03: Login rate limiting (5 attempts/min per IP)
  - F-28: Failed login logging elevated to Warning level
  - F-16: AutomationKey comparison uses FixedTimeEquals
  - F-08: Invitation role ceiling check (CompanyAdmin cannot invite CompanyOwner)
  - F-19: WebhookSecret removed from AdminController company response

- **Docker Volume Mounts Added:**
  - `uploads_data` volume mounted to `/app/uploads` for file persistence
  - Files persist across container restarts (temporary solution before F-20 MinIO)

### Billing System Phase 1 Improvements (2026-04-15)

Critical billing security and consistency fixes implemented:

#### Security Fixes
- **Stripe webhook protection:** Changed from `[AllowAnonymous]` to `[Authorize(Policy = "SuperAdminOnly")]`
- **Subscription status gate:** Cancelled/suspended companies now get zero entitlements and blocked from module access
- **Seat limit enforcement:** `CheckLimitAsync("seats")` now counts actual active memberships

#### Data Consistency
- **Tenant sync helper:** `SyncTenantDenormalizationAsync()` syncs `Tenant.Plan` and `Tenant.SubscriptionStatus` on all lifecycle operations (suspend, cancel, reactivate, plan change)

#### Enforcement Improvements
- **Effective entitlements in limit checks:** All `IsWithin*Async` methods now respect admin overrides via `GetEffectiveEntitlementsAsync`
- **GetUsage fix:** Billing API now uses `GetEffectiveEntitlementsAsync` instead of hardcoded plan→limit map

#### New Tests
- 15 new tests in `BillingPhase1Tests.cs` covering:
  - Tenant sync on all lifecycle operations
  - Subscription status gate behavior
  - Admin override respect in limit checks
  - Seat limit actual count verification

### Billing System Phase 3 Improvements (2026-04-16)

Usage tracking and enforcement improvements implemented:

#### UsageMetric Constants
- Added `UsageMetric` static class in `OrvixFlow.Core/Entities/UsageMetric.cs`
- Standard metric type constants: `AiTokens`, `N8nNodes`, `StorageMb`, `KnowledgeBases`, `InboxMessages`
- Prevents silent metric type mismatches across the codebase

#### UsagePeriodRolloverJob
- New Hangfire job in `OrvixFlow.Api/Jobs/UsagePeriodRolloverJob.cs`
- Runs daily at midnight to advance expired billing periods
- Syncs `CurrentPeriodStart` → `CurrentPeriodEnd`, then extends `CurrentPeriodEnd` by interval (30/365 days)
- Records audit events for each period rollover
- Registered in `Program.cs` with cron schedule `0 0 * * *`

#### Entitlements Already Period-Aware
- `EntitlementResolver.GetEntitlementsAsync` already uses `subscription.CurrentPeriodStart` for filtering usage events
- Phase 1 already fixed to use `GetEffectiveEntitlementsAsync` in all `IsWithin*Async` methods

#### Tests Added
- 4 new tests in `BillingPhase3Tests.cs` covering:
  - UsageMetric constants verification
  - CurrentPeriodStart usage in entitlements
  - Period rollover simulation
  - Gateway blocks cancelled subscriptions

