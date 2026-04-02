# RAG Extension — Status & Remaining Work

> This document is the canonical reference for the RAG extension feature across
> sessions. Backend Phases 1–5 are complete and production-hardened.

---

## ✅ What Was Done — Completed Phases

### Phase 1 — Core Ingestion Pipeline

**Goal:** Enable file upload, parsing, chunking, and vector embedding.

**Delivered:**
- `KnowledgeBaseDocument` + `KnowledgeBase` (chunk) entities with full EF Core
  config, pgvector column (`vector(1536)`), GIN full-text search index, and HNSW
  cosine index
- `IDocumentParser` / `IChunker` / `IFileStorage` interfaces
- `PlainTextParser`, `PdfParser` (PdfPig), `DocxParser` (OpenXML)
- `OverlapChunker` — sliding window chunking with configurable size and overlap
  (`AI:Ingestion:ChunkSize`, `AI:Ingestion:ChunkOverlap`)
- `LocalFileStorage` — saves uploaded files to disk under tenant-scoped paths
- `IngestionPipelineService` — orchestrates Parse → Chunk → Embed → Persist
- `FileIngestionJob` — Hangfire background job so uploads return immediately
- `FileIngestionController` — `POST /api/v1/knowledge/upload` (multipart)
- `AddRagExtension` EF Core migration

**Key files:**
`OrvixFlow.Core/Entities/KnowledgeBase.cs`,
`KnowledgeBaseDocument.cs`,
`OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`,
`OrvixFlow.Infrastructure/Ai/Parsers/`,
`OrvixFlow.Infrastructure/Ai/Chunking/OverlapChunker.cs`,
`OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`,
`OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs`,
`OrvixFlow.Api/Controllers/FileIngestionController.cs`

---

### Phase 2 — Hybrid Vector Search & Reranking

**Goal:** Surface the most relevant knowledge chunks for a query.

**Delivered:**
- `HybridVectorSearchService` — combines:
  - Dense vector search via pgvector `CosineDistance`
  - Sparse full-text search via PostgreSQL `to_tsvector` / `plain_to_tsquery`
  - Reciprocal Rank Fusion (RRF, k=60) to merge both score lists
- `LlmScorerReranker` — sends top RRF candidates to LLM for 0–10 relevance
  scoring; falls back to RRF order on failure
- `IReranker` interface for pluggable reranking strategies
- `KnowledgeSnippet` model with `RelatedImages` collection
- `KnowledgeBaseController` — `GET /api/v1/knowledge/search?q=`

**Key files:**
`OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs`,
`OrvixFlow.Infrastructure/Ai/LlmScorerReranker.cs`,
`OrvixFlow.Core/Interfaces/IReranker.cs`,
`OrvixFlow.Core/Models/KnowledgeSnippet.cs`

---

### Phase 3 — Multi-Modal Image Support

**Goal:** Ingest, caption, embed, and retrieve images from documents.

**Delivered:**
- `KnowledgeBaseImage` entity — stores path, MIME type, LLM-generated caption,
  pgvector caption embedding, and links to parent document/chunk
- `ImageFileParser` — handles `image/jpeg`, `image/png`, `image/gif`, `image/webp`
- `IngestionPipelineService` extended — extracts images from PDF/DOCX pages,
  captions each image via `IChatCompletionService`, embeds captions, persists
  `KnowledgeBaseImage` rows
- `ImageResolver` — given a query + document IDs, returns top-N relevant images
  via cosine similarity on caption embeddings (with InMemory fallback for tests)
- `IImageResolver` interface
- `KnowledgeImageRef` model used in search results and draft payloads
- `AppDbContext` updated — `KnowledgeBaseImages` DbSet, global query filter by
  `TenantId`, cascade delete from document

**Key files:**
`OrvixFlow.Core/Entities/KnowledgeBaseImage.cs`,
`OrvixFlow.Infrastructure/Ai/Parsers/ImageFileParser.cs`,
`OrvixFlow.Infrastructure/Ai/ImageResolver.cs`,
`OrvixFlow.Core/Interfaces/IImageResolver.cs`,
`OrvixFlow.Core/Models/KnowledgeImageRef.cs`

---

### Phase 4 — RAG Email Orchestration & n8n Integration

**Goal:** Full end-to-end RAG pipeline from email receipt to structured n8n payload.

**Delivered:**
- `RagEmailService` — main orchestration:
  1. Classify intent (`IIntentClassifierService`)
  2. Hybrid search + rerank
  3. Generate draft (`IDraftGeneratorService`, persona-aware)
  4. Scan draft for `[image:GUID]` citation tags → populate `Images` list
  5. Return structured `N8nEmailPayload`
- `IRagEmailService` interface
- `N8nEmailPayload` — rigid JSON contract for n8n:
  `Email`, `Classification`, `Rag`, `Images`, `Action`, `Flags`, `Audit`
- `RagEmailController` — `POST /api/v1/inbox/rag` (guarded by `[RequireAutomationKey]`)
- `EmailDraftResult` model
- `DraftGeneratorService` extended to accept `AgentPersona?` for tone shaping

**Actions emitted:** `draft_ready`, `human_review_required`,
`insufficient_context`, `escalate`, `spam_detected`

**Key files:**
`OrvixFlow.Infrastructure/Ai/RagEmailService.cs`,
`OrvixFlow.Core/Interfaces/IRagEmailService.cs`,
`OrvixFlow.Core/Models/N8nEmailPayload.cs`,
`OrvixFlow.Core/Models/EmailDraftResult.cs`,
`OrvixFlow.Api/Controllers/RagEmailController.cs`

---

### Phase 5 — Security, Observability & Testing Hardening

**Goal:** Make the pipeline production-safe with monitoring, rate control, and full test coverage.

**Delivered:**

_Security_
- `IVirusScanService` / `NoopVirusScanService` — pluggable scan hook pre-ingestion
- `FileIngestionController` — MIME type whitelist (PDF, DOCX, TXT, images) + 10 MB
  max size enforced before the job is queued
- Native .NET 8+ `FixedWindowLimiter` — 10 uploads/min on `/api/v1/knowledge/upload`;
  returns HTTP 429 on breach

_Observability_
- `IRagMetricsCollector` / `RagMetricsCollector` — records retrieval latency,
  snippet count, image count, and token model as structured JSON in `AuditTrail`
- `RagEmailService` + `IngestionPipelineService` — structured `ILogger` output
  with `[TraceId]` prefix and `IRagMetricsCollector` calls at every key step

_Health Monitoring_
- `RagHealthCheck` — live pgvector query + embedding service round-trip; exposed at
  `GET /health/rag`

_Tests_
- `TenantIsolationTests` extended to cover `KnowledgeBaseDocument` and
  `KnowledgeBaseImage` query filters (226 tests passing, 2 pre-existing failures
  in `OrgHierarchyTests` — see Remaining Work item #2)
- `RagPipelineIntegrationTests` — full mock-based flow from classify → search →
  draft → metrics verification
- `IngestionPipelineServiceTests` — storage mock + multi-parser ingestion test
- `RagEmailServiceTests` — two scenarios: happy path and insufficient context
- `load-test.sh` — 10 concurrent upload stress test

**Key files:**
`OrvixFlow.Core/Interfaces/IVirusScanService.cs`,
`OrvixFlow.Infrastructure/Services/Security/NoopVirusScanService.cs`,
`OrvixFlow.Core/Interfaces/IRagMetricsCollector.cs`,
`OrvixFlow.Infrastructure/Ai/RagMetricsCollector.cs`,
`OrvixFlow.Api/Health/RagHealthCheck.cs`,
`OrvixFlow.Tests/RagPipelineIntegrationTests.cs`,
`OrvixFlow.Tests/TenantIsolationTests.cs`,
`load-test.sh`

---

## Current State Snapshot

| Layer | Status |
|-------|--------|
| Backend API | ✅ Complete — 0 build errors |
| Database schema | ✅ Migration `AddRagExtension` applied |
| Unit + integration tests | ✅ 226 passed / 2 pre-existing failures |
| Frontend (knowledge page) | ⚠️ Text-only ingest; file upload UI missing |
| n8n workflow template | ⚠️ Payload contract ready; workflow JSON not committed |
| Virus scanning | ⚠️ Noop placeholder only |

---

## 🔴 High Priority

### 1. Frontend File Upload UI (`knowledge/page.tsx`)

**Status:** Missing  
**Why:** `FileIngestionController` at `/api/v1/knowledge/upload` accepts PDF, DOCX,
TXT, and image files via multipart form — but the knowledge base page only has a
plain-text ingest box that calls the old `/api/agent/ingest` endpoint. Users
cannot exploit the new multi-format ingestion pipeline from the browser.

**What to build:**
- File `<input type="file" accept=".pdf,.docx,.txt,.png,.jpg,.jpeg,.webp">` with
  drag-and-drop support
- Send as `FormData` to `/api/v1/knowledge/upload` with `Authorization` header
- Show upload progress + status (Pending → Processing → Indexed / Failed)
- Display ingested documents in a table with status badges, file name, size, and
  a delete action
- Wire to `/api/v1/knowledge/documents` for listing (if endpoint exists; add it
  if not)

**Files to modify / create:**
- `orvixflow-web/app/(dashboard)/knowledge/page.tsx` — add upload section
- Potentially `OrvixFlow.Api/Controllers/FileIngestionController.cs` — add
  `GET /api/v1/knowledge/documents` listing endpoint if missing

---

## 🟡 Medium Priority

### 2. Fix Pre-existing `OrgHierarchyTests` Failures

**Status:** 2 tests failing (pre-existing, unrelated to RAG)  
**Tests:**
- `CreateOrganization_AssignsCompanyOwnerRole`
- `CreateOrganization_ReturnsConflict_WhenNameExists`

**Root cause:** `CreateOrganization` in `OrganizationController` (line 86) calls
`_db.Users.AnyAsync(u => u.Id == userId)` before the conflict check, but these
tests do not seed a `User` row — so both return `Unauthorized` instead of their
expected results.

**Fix options (pick one):**
1. Seed a `User` row in the test helpers (`SeedTenantWithOwner` / `BuildController`)
2. Remove the User existence check if it is not a security requirement (JWT
   already proves the user exists)

---

### 3. Migrate Away from Obsolete `ITextEmbeddingGenerationService`

**Status:** 20 compiler warnings  
**Why:** Semantic Kernel deprecated `ITextEmbeddingGenerationService` in favour of
`Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`.

**Affected files:**
- `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs`
- `OrvixFlow.Infrastructure/Ai/ImageResolver.cs`
- `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`
- `OrvixFlow.Infrastructure/Ai/RagHealthCheck.cs` (via Api)
- Associated test mocks in `OrvixFlow.Tests/`

**Action:** Replace every `ITextEmbeddingGenerationService` reference with the new
`IEmbeddingGenerator<string, Embedding<float>>` abstraction and update DI
registration in `DependencyInjection.cs`.

---

### 4. Bump `SixLabors.ImageSharp` to Resolve Known Vulnerabilities

**Status:** Security advisory  
**Action:** Run `dotnet list package --vulnerable` to get current advisory, then
update `SixLabors.ImageSharp` to the patched version in
`OrvixFlow.Infrastructure/OrvixFlow.Infrastructure.csproj`.

---

## 🟢 Low Priority / Nice-to-Have

### 5. Replace `NoopVirusScanService` with Real AV (ClamAV / VirusTotal)

**Status:** Placeholder in production  
**Current:** `NoopVirusScanService` always returns `true` (safe).  
**Action:** Implement a `ClamAvVirusScanService` that streams the file to a
running ClamAV daemon (official `nClam` NuGet) and reads the scan result. Wire
via DI toggle in `appsettings.json`:

```json
"Security": {
  "VirusScan": {
    "Provider": "ClamAV",   // "Noop" | "ClamAV" | "VirusTotal"
    "ClamAvHost": "clamav",
    "ClamAvPort": 3310
  }
}
```

---

### 6. Remove `Console.WriteLine` Debug Logs from `OrganizationController`

**Status:** Noise in production logs  
**Files:** `OrvixFlow.Api/Controllers/OrganizationController.cs` lines ~77, 82,
296, 300, 307, 311, 317, 332  
**Action:** Replace with `ILogger<OrganizationController>` calls at `Debug` level,
or remove entirely. Never use `Console.WriteLine` in production ASP.NET Core code
(see Code Style in `AGENTS.md`).

---

### 7. n8n Workflow Template for RAG Email

**Status:** Documented but not committed  
**Why:** `RagEmailController` produces a structured `N8nEmailPayload` ready for
n8n, but there is no committed n8n flow template that wires this up end-to-end.
**Action:** Export the n8n workflow to `tasks/n8n-rag-email-workflow.json` so it
can be version-controlled and imported during new tenant onboarding.

---

### 8. `GET /api/v1/knowledge/documents` — Document Listing Endpoint

**Status:** Possibly missing  
**Why:** The frontend upload UI (item #1) needs a way to list previously ingested
documents with their status.  
**Action:** Add a controller action to `FileIngestionController` (or a dedicated
`KnowledgeDocumentsController`) that returns paged `KnowledgeBaseDocument`
records for the current tenant, including `Status`, `FileName`, `FileSizeBytes`,
and `CreatedAtUtc`.

---

## Summary

| # | Item | Priority | Effort |
|---|------|----------|--------|
| 1 | Frontend file upload UI | 🔴 High | ~3–4h |
| 2 | Fix OrgHierarchyTests | 🟡 Medium | 30 min |
| 3 | Migrate ITextEmbeddingGenerationService | 🟡 Medium | ~2h |
| 4 | Bump ImageSharp | 🟡 Medium | 15 min |
| 5 | Real virus scanning (ClamAV) | 🟢 Low | ~3h |
| 6 | Remove Console.WriteLine | 🟢 Low | 15 min |
| 7 | n8n workflow template | 🟢 Low | 1h |
| 8 | Document listing endpoint | 🟢 Low | 1h |
