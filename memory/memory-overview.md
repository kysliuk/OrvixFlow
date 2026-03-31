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

## RAG Assistant Extension (Status: Phase 1 Completed)
Implemented file ingestion pipeline:
- PDF, DOCX, TXT support
- Paragraph-aware overlap chunking
- Background ingestion with Hangfire
- Local multi-tenant file storage
