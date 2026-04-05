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
- `AppDbContext` applies global query filters
- `ITenantProvider` interface extracts tenant from JWT claims
- `TenantProvider` (Api/Services) resolves current tenant

**Tenant Resolution Order:**
1. JWT claim `TenantId` or `ActiveCompanyId`
2. For webhooks: `X-Tenant-ID` header fallback

### Override Hierarchy (Phase 2)

**Entitlements:**
```
CompanyEntitlementOverride (per-company custom limits)
      ↓
PlanEntitlements (plan defaults)
```

**Modules:**
```
CompanyModuleOverride (grant/suppress per company)
      ↓
PlanModuleInclusion (plan default modules)
```

Resolution is handled by `EntitlementResolver.GetEffectiveEntitlementsAsync()` and `CanUseModuleWithOverridesAsync()`.

**Tenant.Plan Sync:** `Tenant.Plan` string is denormalized and synced with `CompanySubscription.PlanTemplate.Slug` whenever a plan is assigned via `CompanySubscriptionService.AssignPlanAsync()`.

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

**RAG Security & Observability (Phase 5):**
- **Rate Limiting**: Native .NET 8+ `FixedWindowLimiter` on upload endpoint (10 req/min, 429 on breach).
- **Validation**: MIME type whitelist + 10MB size cap enforced pre-ingestion.
- **Virus Scan Hook**: `IVirusScanService` / `NoopVirusScanService` — swap for ClamAV without pipeline changes.
- **Metrics**: `IRagMetricsCollector` records retrieval latency, chunk counts, image refs, and token usage as structured JSON in `AuditTrail`.
- **Health Check**: `RagHealthCheck` verifies pgvector connectivity and embedding service availability via `/health/rag`.

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

### Trial Expiration Flow
- `TrialExpirationJob` runs every 6 hours via Hangfire recurring schedule
- Finds all `Trialing` subscriptions where `TrialEndsAt <= now`
- Downgrades to Free plan, syncs `Tenant.Plan`, writes `AuditTrail` entry
- Prevents free-tier abuse by enforcing trial deadlines

### Query Filter Bypass for Admin Operations
When admin endpoints need to query data across companies, services must use `.IgnoreQueryFilters()` on EF Core queries. The global query filter `s.CompanyId == _tenantProvider.GetTenantId()` returns the admin's own company ID, not the target company's ID.

**Required `.IgnoreQueryFilters()` in admin-facing service methods:**
- `CompanySubscriptionService.GetSubscriptionAsync()`
- `CompanySubscriptionService.AssignPlanAsync()` (existing subscription check)
- `EntitlementResolver.GetSubscriptionAsync()`
- `EntitlementResolver.GetEntitlementOverrideAsync()`
- `EntitlementResolver.GetModuleOverridesAsync()`

This is safe because admin endpoints enforce their own authorization (`IsSuperAdmin()`, `IsGlobalAdmin()`).

### InternalOperator Enforcement
- `IsGlobalAdmin()` allows both `SuperAdmin` and `InternalOperator`
- GET endpoints use `IsGlobalAdmin()` (read-only access)
- POST/PUT/DELETE endpoints use `IsSuperAdmin()` (mutations only)
- Policies registered in `Program.cs`: `SuperAdminOnly` and `PlatformAdmin`

### Company Actions
- `POST /api/admin/companies/{id}/cancel` — Cancel subscription
- `POST /api/admin/companies/{id}/change-plan` — Change plan with immediate effect
- `POST /api/admin/companies/{id}/reactivate` — Reactivate suspended subscription
- Frontend: Action buttons on company detail page with error handling

## Role System (Two-Layer)

### Global (platform-level) — `User.Role`
Stored in `User.Role`. Only for platform staff. Normal users have `User.Role = ""`.

| Role | Description |
|------|-------------|
| `SuperAdmin` | Full platform control |
| `InternalOperator` | Platform support, read-only admin |
| `""` (empty) | Normal users — no global role |

### Company (organization-level) — `UserCompanyMembership.CompanyRole`
Stored in `UserCompanyMembership.CompanyRole`. One per user-company relationship.

| Role | Description |
|------|-------------|
| `CompanyOwner` | Full control within their company |
| `CompanyAdmin` | Delegated company management |
| `DepartmentManager` | Manages within assigned department(s) |
| `Operator` | Performs work within assigned modules |
| `Viewer` | Read-only within assigned modules |

### JWT `Role` Claim
- Platform admins (`SuperAdmin`, `InternalOperator`): JWT contains the global role from `User.Role`
- Normal users: JWT contains their `UserCompanyMembership.CompanyRole`
- Logic in `MintJwtAsync`: if `User.Role` is a platform admin role, use it; otherwise use `CompanyRole`

### Access Resolution
- `AccessResolver` reads `UserCompanyMembership.CompanyRole` for permission checks
- `ScopeContext` reads JWT `Role` claim — works because platform roles pass `IsCompanyAdminOrAbove()`
- User's effective access = company plan modules + company module overrides, filtered by role permissions

### Critical Rules
- Never set `User.Role` to a company role value (e.g., `CompanyOwner`, `Operator`)
- Never compare `User.Role` against company roles
- `User.Role` defaults to `string.Empty` — only populated for platform admins

## Webhook Security

HMAC-SHA256 signature validation:
- Header: `X-Orvix-Signature: sha256=<hex-hmac>`
- Tenant webhook secret stored in `Tenant.WebhookSecret`
- Middleware: `HmacSignatureMiddleware`
- Only applies to `/api/webhook/inbox`
