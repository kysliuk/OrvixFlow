# OrvixFlow - Architecture

## Layered Architecture

```
┌─────────────────────────────────────┐
│         orvixflow-web               │  Next.js 16 + NextAuth 5
│    (pages, components, hooks)       │
└──────────────┬──────────────────────┘
               │ HTTP + JWT Bearer
┌──────────────▼──────────────────────┐
│          OrvixFlow.Api              │  Controllers, Filters, Middleware
│    (Program.cs, Controllers/)       │
└──────────────┬──────────────────────┘
               │ DI
┌──────────────▼──────────────────────┐
│         OrvixFlow.Core              │  Entities, Interfaces, Models
│   (Entities/, Interfaces/, Models/) │
└──────────────┬──────────────────────┘
               │ DI
┌──────────────▼──────────────────────┐
│      OrvixFlow.Infrastructure      │  Services, Data, AI
│   (Services/, Data/, Auth/, Ai/)   │
└─────────────────────────────────────┘
```

## Multi-Tenancy

**Implementation:** EF Core Query Filters + TenantProvider

- Every entity has `TenantId` or `CompanyId` (same concept)
- `AppDbContext` applies global query filters (lines 168-185)
- `ITenantProvider` interface extracts tenant from JWT claims
- `TenantProvider` (Api/Services) resolves current tenant

**Tenant Resolution Order:**
1. JWT claim `TenantId` or `ActiveCompanyId`
2. For webhooks: `X-Tenant-ID` header fallback

## Authentication & Authorization

### JWT Claims
```
sub: UserId
email: user@company.com
TenantId: Guid
ActiveCompanyId: Guid
Role: "CompanyOwner" | "CompanyAdmin" | "Manager" | "Member"
Plan: "Free" | "Trialing" | "Pro"
DisplayName: "John Doe"
```

### Role Hierarchy
```
CompanyOwner (full access)
    └── CompanyAdmin
        └── Manager  
            └── Member
```

### Module Permission System

- **ModuleDefinition**: Registered modules (key, name, isActive)
- **ModuleAssignment**: Company/Department/User level assignments
- **ModulePermissionGrant**: Specific permissions (CanView, CanUse, CanTest, CanConfigure, CanManageIntegrations, CanManagePrompts, CanViewLogs, IsAdmin)

**Scopes:** "Company" | "Department" | "User"

**Gating:** `[RequireModule("module-key")]` attribute on controllers/actions

## AI Integration

**Semantic Kernel Setup:**
- Configured in `Infrastructure/DependencyInjection.cs:38-82`
- Supports: OpenAI, Groq, or Mock (for testing)
- Plugins: KnowledgeBaseSearchPlugin, N8nAutomationPlugin

**RAG Ingestion Pipeline (Phase 3):**
- **Abstractions**: `IDocumentParser` (PDF, DOCX, TXT, Image), `IChunker` (Overlap), `IFileStorage` (Local).
- **Service**: `IngestionPipelineService` (Orchestrates Parse -> Chunk -> Embed + Image Extraction).
- **Parsers**: `PdfParser` (PdfPig), `DocxParser` (OpenXML), `ImageFileParser` (ImageSharp).
- **Worker**: `FileIngestionJob` (Hangfire background job).

**Advanced Retrieval (Hybrid Search):**
- **Component**: `HybridVectorSearchService`.
- **Search**: Combines Vector (pgvector) + FTS (Npgsql + english tsvector).
- **Fusion**: Reciprocal Rank Fusion (RRF) for scoring integration.
- **Reranker**: `LlmScorerReranker` (Semantic Kernel scoring).
- **Image Resolution**: `ImageResolver` (Retrieves top relevant images based on snippet context).

**RAG Orchestration Flow (Phase 4):**
- **Service**: `RagEmailService` (The main entry point for automated RAG responses).
- **Logic**: Intent Classify → Hybrid Search → Rerank → Draft Gen → Image Citation Extraction → N8n Payload Assembly.
- **Contract**: `N8nEmailPayload` (A rigid JSON structure for reliable n8n workflow triggers).
- **Citations**: AI-generated drafts are scanned for `[image:GUID]` tags to selectively attach relevant knowledge base images.

**Agent Flow:**
1. User prompt → AgentController
2. AgentService.ProcessInternalAsync (or InboxGuardianService)
3. Semantic Kernel invokes plugins (KnowledgeBaseSearchPlugin)
4. KnowledgeBaseSearchPlugin calls `HybridVectorSearchService`
5. Hybrid Search → RRF → Rerank → Image Resolution
6. Response + relevant images → audit + usage recording

**Inbox Guardian Flow:**
1. InboxEvent ingested → `InboxProcessingJob` (Hangfire)
2. Fetch `AgentPersona` for tenant (tone, custom instructions, sign-off)
3. Classify intent → RAG search → Generate draft (persona-aware)
4. Evaluate `WorkflowPolicy` → auto-execute or hold for approval
5. On approval with edits → `DraftFeedback` → `FeedbackEnrichmentJob` → KB update

### Correlation & Tracing
- `InboxEvent.TraceId` assigned at processing start
- All log messages include `[TraceId]` prefix for full pipeline traceability
- Audit trails include TraceId reference for admin debugging

**Inbox Guardian Flow:**
1. InboxEvent ingested → `InboxProcessingJob` (Hangfire)
2. Fetch `AgentPersona` for tenant (tone, custom instructions, sign-off)
3. Classify intent → RAG search → Generate draft (persona-aware)
4. Evaluate `WorkflowPolicy` → auto-execute or hold for approval
5. On approval with edits → `DraftFeedback` → `FeedbackEnrichmentJob` → KB update

## Background Jobs

**Hangfire** with PostgreSQL storage:
- Dashboard: `/hangfire` (local only)
- `InboxProcessingJob`: Asynchronous email processing (with persona lookup, correlation IDs, webhook retry/backoff)
- `FeedbackEnrichmentJob`: Extracts guidelines from human-edited drafts, enriches KnowledgeBase
- `FileIngestionJob`: Background document parsing and embedding
- n8n provisioning/cleanup jobs in `MailboxConnectionsController`
- Endpoint: `api/inbox/process`, `api/v1/knowledge/upload`

### Webhook Callback Retry
- On callback failure: scheduled retry with exponential backoff (30s, 60s, 120s)
- Max 3 retries, logged with correlation TraceId

### Agent Persona Flow
- `AgentPersona` entity stores per-tenant tone, custom instructions, and sign-off
- Fetched in `InboxProcessingJob` and passed through `InboxGuardianService` → `DraftGeneratorService`
- Injected into LLM prompt for consistent brand voice

### Webhook Callback Retry
- On callback failure: scheduled retry with exponential backoff (30s, 60s, 120s)
- Max 3 retries, logged with correlation TraceId

## Webhook Security

HMAC-SHA256 signature validation:
- Header: `X-Orvix-Signature: sha256=<hex-hmac>`
- Tenant webhook secret stored in `Tenant.WebhookSecret`
- Middleware: `HmacSignatureMiddleware`
- Only applies to `/api/webhook/inbox`
