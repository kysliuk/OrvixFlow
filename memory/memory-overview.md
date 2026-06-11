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

## RAG Assistant Extension (Status: Feature-complete, production validation still pending)
The RAG stack is materially implemented and instrumented, but it should not be treated as fully production-ready until the broader release, deploy, and runtime blockers documented in `tasks/production/current-state-audit.md` are resolved.

Current implementation includes:
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

## Memory System Map

This memory system is meant to map the **current state of the world**, not the historical implementation sequence. 

- **architecture**: `memory-architecture.md` (Roles, Entitlements, DB Boundaries)
- **security rules**: `memory-security.md` (Isolation invariants, Role layers, Webhooks)
- **fragility points**: `memory-risks.md` (Known weak points, edge cases, anti-patterns)
- **feature locations**: `memory-feature-map.md` (What controller/service owns what)
- **auth flow**: `auth.md` (Deep dive on identity)
- **file inventory**: `memory-file-map.md` (Complete file listing)
