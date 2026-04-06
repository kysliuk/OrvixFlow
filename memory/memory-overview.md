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
- Test count increased to 278 (277 + 1 new `EmbeddingMigrationSmokeTests`)
