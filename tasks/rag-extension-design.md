# RAG Extension Design — OrvixFlow Email Assistant with n8n Integration

**Author:** Senior AI Systems Engineer  
**Date:** 2026-03-31  
**Status:** Design Proposal

---

## 1. Findings — Current-State Assessment

### 1.1 What Exists

| Component | Status | Notes |
|-----------|--------|-------|
| `KnowledgeBase` entity | ✅ Exists | `Id`, `TenantId`, `RawContent`, `Metadata (JSON)`, `EmbeddingVector` (pgvector) |
| `IngestionService` | ⚠️ Minimal | Only text strings; no file parsing, no chunking, no deduplication |
| `HybridVectorSearchService` | ⚠️ Partial | L2-distance only; no cosine, no BM25 keyword fallback, similarity threshold hard-coded |
| `KnowledgeBaseSearchPlugin` | ✅ Works | Used by Semantic Kernel agent; returns top-3 raw snippets |
| `IntentClassifierService` | ✅ Solid | JSON-schema classification with human-review escalation |
| `DraftGeneratorService` | ✅ Solid | Prompt-injection-safe, fallback marker, XML-escaped email context |
| `InboxGuardianService` | ✅ Orchestrates | Classifier → hybrid search → draft generator → n8n trigger |
| `N8nAutomationPlugin` | ⚠️ Minimal | Sends a plain `{data: string}` payload; no structured contract |
| `AgentController` / `InboxController` | ✅ Secured | Module-gated (`RequireModule`), JWT-tenant-scoped |
| pgvector in PostgreSQL | ✅ Deployed | `ankane/pgvector` Docker image with EF Core |
| Image support | ❌ Missing | No image storage, no image retrieval, no image metadata in KB |
| File upload endpoint | ❌ Missing | No multipart/form-data endpoint; ingestion is text-only |
| Reranking | ❌ Missing | No cross-encoder or LLM-based reranking step |
| Chunking strategy | ❌ Missing | Full documents stored as single `RawContent` blobs |
| n8n structured output | ❌ Missing | n8n receives unstructured `{data: string}` |

### 1.2 Critical Gaps for Email RAG Use-Case

1. **No file ingestion** — Users cannot upload PDFs, DOCX, or images to feed the knowledge base.
2. **No chunking** — Large documents stored as a single vector sacrifice recall precision.
3. **No image relevance** — Knowledge snippets may reference images, but there is no mechanism to retrieve or attach them.
4. **Weak retrieval** — Pure L2/cosine vector search misses keyword-dense queries; no reranking means noisy top-k results bleed into the LLM prompt.
5. **Unstructured n8n payload** — `{data: string}` is unusable for downstream automation (e.g., send-email node needs `to`, `subject`, `body`, `attachments` as distinct fields).
6. **No observability** — No trace IDs on RAG calls; latency and retrieval quality go unmonitored.

---

## 2. Proposed Design

### 2.1 Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     n8n (orix-n8n:5678)                      │
│  Webhook Trigger → RAG Node → Structured Output Webhook     │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTP POST /api/v1/inbox/rag
                             │ (AutomationKey header)
┌────────────────────────────▼────────────────────────────────┐
│                   OrvixFlow.Api                              │
│   RagEmailController  ← new                                  │
│   FileIngestionController ← new (multipart upload)          │
└────────┬────────────────────────────┬────────────────────────┘
         │ IIngestionPipelineService  │ IRagEmailService
         ▼                            ▼
┌─────────────────┐       ┌──────────────────────────────────┐
│  File Parsers   │       │   RAG Email Pipeline              │
│  (PDF/DOCX/     │       │   1. Classify (existing)          │
│   Image/TXT)    │       │   2. HybridSearch (improved)      │
│                 │       │   3. Rerank                       │
│  → Chunker      │       │   4. ImageResolver  ← new         │
│  → Embedder     │       │   5. DraftGenerator (improved)    │
│  → DB insert    │       │   6. Build n8n payload            │
└─────────────────┘       └──────────────────────────────────┘
         │                            │
         ▼                            ▼
┌──────────────────────────────────────────────────────────────┐
│  PostgreSQL + pgvector                                        │
│  knowledge_bases (extended)                                   │
│  knowledge_base_images  ← new                                 │
└──────────────────────────────────────────────────────────────┘
```

### 2.2 File Ingestion Pipeline

#### 2.2.1 New Entity: `KnowledgeBaseDocument`

```csharp
// OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs
public class KnowledgeBaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; // MIME type
    public long FileSizeBytes { get; set; }

    // "Text" | "PDF" | "DOCX" | "Image"
    public string SourceType { get; set; } = "Text";

    public string StoragePath { get; set; } = string.Empty; // relative path if stored on disk/blob

    public string Status { get; set; } = "Pending"; // Pending | Processing | Indexed | Failed
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAtUtc { get; set; }

    // Navigation: chunks produced from this document
    public ICollection<KnowledgeBase> Chunks { get; set; } = [];
}
```

#### 2.2.2 Extended `KnowledgeBase` (chunk-level metadata)

Add the following columns to the existing `KnowledgeBase` entity:

```csharp
// Additions to OrvixFlow.Core/Entities/KnowledgeBase.cs
public Guid? DocumentId { get; set; }          // FK → KnowledgeBaseDocument
public int ChunkIndex { get; set; } = 0;       // position within doc
public string ChunkType { get; set; } = "text"; // "text" | "image_caption"
public string Title { get; set; } = string.Empty; // document title / heading
```

#### 2.2.3 New Entity: `KnowledgeBaseImage`

```csharp
// OrvixFlow.Core/Entities/KnowledgeBaseImage.cs
public class KnowledgeBaseImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public Guid? DocumentId { get; set; }          // FK → KnowledgeBaseDocument
    public Guid? ChunkId { get; set; }             // FK → KnowledgeBase (nearest text chunk)

    public string StoragePath { get; set; } = string.Empty; // disk/blob path
    public string ContentType { get; set; } = "image/png";
    public string AltText { get; set; } = string.Empty;     // LLM-generated caption
    public string? Caption { get; set; }                    // original doc caption if any

    // Caption embedding for semantic image search
    public Pgvector.Vector? CaptionEmbedding { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
```

#### 2.2.4 File Parser Abstraction

```csharp
// OrvixFlow.Core/Interfaces/IDocumentParser.cs
public interface IDocumentParser
{
    bool CanParse(string contentType);
    Task<ParsedDocument> ParseAsync(Stream content, string fileName);
}

public record ParsedDocument(
    string Title,
    IReadOnlyList<TextChunk> TextChunks,
    IReadOnlyList<ImageChunk> ImageChunks
);

public record TextChunk(int Index, string Content, string? Heading);
public record ImageChunk(int Index, byte[] Data, string ContentType, string? Caption);
```

**Implementations in `OrvixFlow.Infrastructure/Ai/Parsers/`:**

| Class | NuGet | Content-types handled |
|-------|-------|----------------------|
| `PlainTextParser` | (none) | `text/plain`, `text/html` |
| `PdfParser` | `PdfPig` | `application/pdf` |
| `DocxParser` | `DocumentFormat.OpenXml` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` |
| `ImageParser` | `SixLabors.ImageSharp` | `image/png`, `image/jpeg`, `image/webp` |

For **images**, the `ImageParser` calls the LLM (`IChatCompletionService` with vision support if available, or `text-embedding-3-small` caption fallback) to produce an `AltText` description stored in `KnowledgeBaseImage.AltText` and used as the chunk content for embedding.

#### 2.2.5 Chunking Strategy

```
Fixed-size overlap chunking (default):
  - chunk_size    = 800 tokens (~3200 chars)
  - chunk_overlap = 150 tokens (~600 chars)
  - Paragraph-aware: never split mid-paragraph

Heading-aware (PDF/DOCX):
  - Detect H1/H2 headings → reset chunk boundary
  - Store heading in chunk Metadata so retrieval can prioritize heading matches
```

Implemented in `OrvixFlow.Infrastructure/Ai/Chunking/OverlapChunker.cs`.

#### 2.2.6 Ingestion Pipeline Service

```csharp
// OrvixFlow.Core/Interfaces/IIngestionPipelineService.cs
public interface IIngestionPipelineService
{
    Task<IngestionResult> IngestFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid? userId = null,
        Guid? departmentId = null);
}
```

**Pipeline steps** (`OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`):
1. Validate MIME type / size (max configurable, default 20 MB)
2. Persist raw file to `{tenantId}/{documentId}/{fileName}` on local disk (or pluggable `IFileStorage`)
3. Detect parser → `ParseAsync()`
4. Chunk text chunks
5. Batch-embed text chunks via `ITextEmbeddingGenerationService` (parallel, batch size 50)
6. For each `ImageChunk`: generate caption → embed caption → store `KnowledgeBaseImage`
7. Persist all `KnowledgeBase` chunks + `KnowledgeBaseDocument` in a single `SaveChangesAsync()`
8. Track storage usage via `IUsageService`
9. Return `IngestionResult { DocumentId, ChunkCount, ImageCount, ErrorMessage? }`

### 2.3 Embeddings & Indexing

| Concern | Current | Proposed |
|---------|---------|----------|
| Model | `text-embedding-3-small` (1536-dim) | Same default; add config `AI:OpenAI:EmbeddingModel` |
| Vector index | None (sequential scan) | `CREATE INDEX CONCURRENTLY ON knowledge_bases USING hnsw (embedding_vector vector_cosine_ops)` |
| Distance metric | L2 | Cosine (better for semantic search; pgvector `<=>` operator) |
| Batch size | 1 per call | 50 per batch (drastically reduces API latency) |
| Image captions | ❌ | Stored in `KnowledgeBaseImage.CaptionEmbedding` (same model) |

**Migration (EF Core):**
```sql
-- In new migration AddRagExtension
ALTER TABLE knowledge_bases
    ADD COLUMN document_id uuid,
    ADD COLUMN chunk_index int NOT NULL DEFAULT 0,
    ADD COLUMN chunk_type varchar(20) NOT NULL DEFAULT 'text',
    ADD COLUMN title text NOT NULL DEFAULT '';

CREATE TABLE knowledge_base_documents ( ... );
CREATE TABLE knowledge_base_images ( ... );

-- HNSW index for cosine similarity
CREATE INDEX CONCURRENTLY knowledge_bases_vector_cosine_idx
    ON knowledge_bases USING hnsw (embedding_vector vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

### 2.4 Retrieval & Reranking

#### 2.4.1 Improved `HybridVectorSearchService`

Replace pure L2 with a hybrid approach:

```
HybridRetrieval(query):
    1. Dense:   cosine vector search  → top-K*3 candidates (K=5, over-fetch=15)
    2. Sparse:  PostgreSQL full-text  → tsvector GIN index, ts_rank
    3. Merge:   Reciprocal Rank Fusion (RRF) of dense + sparse rankings
    4. Rerank:  LLM cross-attention reranker OR score cutoff (configurable)
    5. Return:  top-K snippets with final score, source doc, chunk type
```

**Interface extension:**
```csharp
// Extended return type
public record KnowledgeSnippet
{
    public Guid Id { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string ChunkType { get; init; } = "text";
    public float SimilarityScore { get; init; }
    public string? Metadata { get; init; }
    public Guid? DocumentId { get; init; }
    // New:
    public IReadOnlyList<KnowledgeImageRef> RelatedImages { get; init; } = [];
}

public record KnowledgeImageRef(Guid ImageId, string AltText, string StoragePath);
```

#### 2.4.2 Reranking Options (configurable via `AI:Reranker`)

| Mode | Description | When to use |
|------|-------------|------------|
| `None` | Use RRF score as-is | Dev / cost-sensitive |
| `LlmScorer` | Ask LLM "Rate 1-10: does this snippet answer X?" | Small top-k sets, high accuracy |
| `CrossEncoder` | Call a Cohere/Jina reranker API | Production; best precision |

Default: `LlmScorer` (uses existing Kernel, no new API key required).

### 2.5 Answer Generation

Extend `DraftGeneratorService` to:
1. Accept `IReadOnlyList<KnowledgeImageRef>` alongside text snippets
2. Include image captions in the knowledge context block
3. Return a structured `EmailDraftResult` instead of a raw string

```csharp
// OrvixFlow.Core/Models/EmailDraftResult.cs
public record EmailDraftResult(
    string DraftBody,
    bool IsInsufficientContext,
    IReadOnlyList<KnowledgeImageRef> RelevantImages,
    float DraftConfidence
);
```

The LLM prompt adds:
```
IMAGES AVAILABLE:
[1] diagram-architecture.png — "System architecture showing components A → B → C"
[2] pricing-table.png        — "Pricing tiers: Starter $9/mo, Pro $29/mo, Enterprise custom"

If any image is directly relevant to your reply, reference it by [image:1] marker.
The downstream system will attach the actual image file.
```

The service parses `[image:N]` markers from the LLM response and resolves them to `KnowledgeImageRef` objects for inclusion in the n8n payload.

### 2.6 Image Relevance & Inclusion Logic

```
ImageResolver algorithm:
    1. For each text snippet in retrieved top-K:
          query DB: SELECT img FROM knowledge_base_images
                    WHERE chunk_id = snippet.Id OR document_id = snippet.DocumentId
                    ORDER BY caption_embedding <=> query_embedding
                    LIMIT 2
    2. Deduplicate images across snippets (by ImageId)
    3. Score each image: cosine(caption_embedding, query_embedding)
    4. Include images with score >= IMAGE_THRESHOLD (default 0.65)
    5. Cap total images at MAX_IMAGES (default 3, configurable)
    6. LLM marks [image:N] → only those images are attached in n8n payload
```

This means images are **always candidate-resolved** but **only attached** if the LLM explicitly cites them — avoiding irrelevant image spam.

### 2.7 Structured n8n Output Contract

> See Section 3 for the full JSON contract.

The new `N8nAutomationPlugin` sends a typed payload instead of `{data: string}`. n8n workflows receive a flat, action-specific structure.

### 2.8 New API Endpoints

#### File Upload Endpoint

```
POST /api/v1/knowledge/upload
Authorization: Bearer <jwt>
Content-Type: multipart/form-data
RequireModule: knowledge-base

Body:
  file       (required) binary
  title      (optional) string
  tags       (optional) string (comma-separated)
```

Response:
```json
{
  "documentId": "uuid",
  "status": "Processing",
  "chunkCount": 0,
  "message": "File queued for indexing"
}
```

Processing runs as a **Hangfire background job** to avoid HTTP timeout on large files.

#### RAG Email Endpoint (n8n-facing)

```
POST /api/v1/inbox/rag
X-Automation-Key: <key>
Content-Type: application/json

Body: { see n8n input contract in Section 3 }
```

#### Image Retrieval Endpoint

```
GET /api/v1/knowledge/images/{imageId}
Authorization: Bearer <jwt>
```

Returns the raw image bytes (for frontend preview or n8n to fetch before sending).

---

## 3. Output Contract for n8n

### 3.1 n8n → OrvixFlow Request (inbound)

```json
{
  "tenantId": "uuid",
  "messageId": "unique-email-id",
  "threadId": "thread-uuid-or-null",
  "senderEmail": "customer@example.com",
  "senderName": "Jane Doe",
  "subject": "Question about pricing",
  "bodyText": "Hi, I wanted to know...",
  "attachmentUrls": [],
  "webhookCallbackPath": "reply-ready"
}
```

### 3.2 OrvixFlow → n8n Response (structured payload)

When processing completes, OrvixFlow calls the n8n webhook at `POST /webhook/{webhookCallbackPath}` with:

```json
{
  "version": "1.0",
  "requestId": "uuid",
  "tenantId": "uuid",
  "messageId": "original-message-id",
  "processingTimeMs": 1240,

  "email": {
    "to": "customer@example.com",
    "from": "support@company.com",
    "subject": "Re: Question about pricing",
    "bodyText": "Hi Jane,\n\nThank you for...",
    "bodyHtml": "<p>Hi Jane,</p><p>Thank you for...</p>"
  },

  "classification": {
    "category": "Sales",
    "confidenceScore": 0.92,
    "reasoning": "Pricing inquiry with clear purchase intent",
    "requiresHumanReview": false,
    "reasonForReview": null
  },

  "rag": {
    "snippetsUsed": 3,
    "hasContext": true,
    "retrievalScores": [0.91, 0.87, 0.74],
    "insufficientContext": false
  },

  "images": [
    {
      "imageId": "uuid",
      "altText": "Pricing tiers table",
      "fetchUrl": "http://orvix-api:8080/api/v1/knowledge/images/{uuid}",
      "contentType": "image/png"
    }
  ],

  "action": "draft_ready",
  "flags": {
    "autoSendAllowed": false,
    "humanReviewRequired": false,
    "escalate": false
  },

  "audit": {
    "traceId": "uuid",
    "model": "gpt-4o",
    "estimatedTokens": 1842
  }
}
```

### 3.3 Action Values

| `action` | Meaning | n8n behaviour |
|----------|---------|-|
| `draft_ready` | Draft generated, awaiting send/review | Route to "Send Email" or "Human Review" node |
| `human_review_required` | Classifier or rules flagged for review | Route to Slack/Teams notification node |
| `insufficient_context` | No relevant KB snippets found | Route to "Forward to Support Inbox" node |
| `escalate` | High-risk keywords detected | Route to "Escalation Alert" node |
| `spam_detected` | Spam classification | Route to "Archive" node |

### 3.4 n8n Workflow Topology (recommended)

```
Webhook Trigger (POST /webhook/email-inbound)
    │
    ▼
HTTP Request Node → POST /api/v1/inbox/rag
    │
    ▼
Switch Node (action field)
    ├─ draft_ready          → Gmail/Outlook "Send Email" node (if autoSendAllowed)
    │                         OR Slack "Request Approval" node
    ├─ human_review_required→ Slack/MS Teams notification
    ├─ insufficient_context → Forward to support ticket system
    ├─ escalate             → PagerDuty / Manager alert
    └─ spam_detected        → Archive / Ignore
```

---

## 4. Implementation Phases

### Phase 1 — Foundation: File Ingestion Pipeline (Week 1–2)

**Goal:** Users can upload files that get chunked, embedded, and stored.

**Deliverables:**
- [ ] `KnowledgeBaseDocument` entity + EF migration
- [ ] `KnowledgeBase` entity extended (DocumentId, ChunkIndex, ChunkType, Title)
- [ ] `IDocumentParser` abstraction + `PlainTextParser` + `PdfParser` (PdfPig)
- [ ] `OverlapChunker` with paragraph-awareness
- [ ] `IngestionPipelineService` wiring parsers → chunker → embedder → DB
- [ ] `FileIngestionController` (multipart, Hangfire-backed)
- [ ] Storage config (`IFileStorage` abstraction with `LocalFileStorage` impl)
- [ ] Usage tracking (`storage-mb` metric updated)
- [ ] Unit tests: `IngestionPipelineServiceTests.cs`, `OverlapChunkerTests.cs`

**NuGet additions:**
```xml
<PackageReference Include="PdfPig" Version="0.1.9" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.5" />
```

---

### Phase 2 — Improved Retrieval (Week 2–3)

**Goal:** Retrieval quality leaps from pure vector search to hybrid + reranked.

**Deliverables:**
- [ ] Change distance metric L2 → Cosine in `HybridVectorSearchService`
- [ ] Add PostgreSQL full-text column `content_tsv tsvector GENERATED ALWAYS AS (to_tsvector('english', raw_content)) STORED` via migration
- [ ] Add GIN index on `content_tsv`
- [ ] Implement RRF merge in `HybridVectorSearchService`
- [ ] Implement `LlmScorerReranker` (calls LLM to score relevance 1-10)
- [ ] HNSW index migration for cosine (`vector_cosine_ops`)
- [ ] Update `KnowledgeSnippet` model with `Title`, `ChunkType`, `DocumentId`
- [ ] Unit tests: `HybridSearchTests.cs`

---

### Phase 3 — Image Support (Week 3–4)

**Goal:** Images in documents are indexed and can be attached to email replies.

**Deliverables:**
- [ ] `KnowledgeBaseImage` entity + migration
- [ ] `ImageParser` (extracts embedded images from PDF/DOCX; stores standalone images)
- [ ] LLM-based caption generation (`GenerateCaptionAsync`)
- [ ] Caption embedding storage (`CaptionEmbedding` vector)
- [ ] `ImageResolver` service (candidate retrieval + score threshold)
- [ ] `GET /api/v1/knowledge/images/{id}` endpoint
- [ ] `DraftGeneratorService` updated to accept image context + parse `[image:N]` markers
- [ ] Unit tests: `ImageResolverTests.cs`, `DraftGeneratorWithImagesTests.cs`

---

### Phase 4 — Structured n8n Integration (Week 4)

**Goal:** n8n receives a rich, typed payload it can act on without string parsing.

**Deliverables:**
- [ ] `N8nEmailPayload` model (`OrvixFlow.Core/Models/N8nEmailPayload.cs`)
- [ ] `RagEmailService` (replaces `InboxGuardianService.ProcessIncomingMessageAsync` for n8n flow)
- [ ] `RagEmailController` (`POST /api/v1/inbox/rag`, AutomationKey-secured)
- [ ] Updated `N8nAutomationPlugin` — sends structured JSON payload
- [ ] `WebhookCallbackService` updated to use new payload contract
- [ ] Integration tests: `RagEmailControllerIntegrationTests.cs`
- [ ] Sample n8n workflow JSON (committed to `tasks/n8n-sample-workflow.json`)

---

### Phase 5 — Observability, Testing & Security Hardening (Week 5)

**Goal:** Production-ready quality gate.

**Deliverables:**
- [ ] Structured logging: `traceId` propagated through all RAG services
- [ ] `IRagMetricsCollector` → log to `AuditTrail`: retrieval latency, chunk count, image count, model, tokens
- [ ] Health check: `GET /health/rag` (DB reachability, embedding API, pgvector index status)
- [ ] File size / MIME type allowlist validation (configurable)
- [ ] Antivirus hook stub (`IVirusScanService` → noop by default, ClamAV integration optional)
- [ ] Content Security Policy on image fetch endpoint (signed URL or JWT verification)
- [ ] Rate limiting on upload endpoint (`AspNetCoreRateLimit`)
- [ ] xUnit tests: full pipeline mock-to-end integration test
- [ ] Tenant isolation test for all new entities (`TenantIsolationTests.cs` extended)
- [ ] Load test: 100 concurrent ingestion requests

---

## 5. Architecture Fit Inside the Current Project

### 5.1 Layer Placement

Following the `memory-risks.md` rule "Do NOT cross architecture boundaries":

```
OrvixFlow.Core/
├── Entities/
│   ├── KnowledgeBaseDocument.cs   ← NEW
│   └── KnowledgeBaseImage.cs      ← NEW
├── Interfaces/
│   ├── IDocumentParser.cs         ← NEW
│   ├── IIngestionPipelineService.cs ← NEW
│   ├── IImageResolver.cs          ← NEW
│   ├── IRagEmailService.cs        ← NEW
│   └── IFileStorage.cs            ← NEW
├── Models/
│   ├── EmailDraftResult.cs        ← NEW
│   ├── N8nEmailPayload.cs         ← NEW
│   └── KnowledgeSnippet.cs        ← EXTENDED

OrvixFlow.Infrastructure/
├── Ai/
│   ├── Parsers/
│   │   ├── PlainTextParser.cs     ← NEW
│   │   ├── PdfParser.cs           ← NEW
│   │   ├── DocxParser.cs          ← NEW
│   │   └── ImageParser.cs         ← NEW
│   ├── Chunking/
│   │   └── OverlapChunker.cs      ← NEW
│   ├── IngestionPipelineService.cs ← NEW (replaces thin IngestionService)
│   ├── ImageResolver.cs           ← NEW
│   ├── RagEmailService.cs         ← NEW
│   ├── HybridVectorSearchService.cs ← MODIFIED (hybrid + rerank)
│   └── DraftGeneratorService.cs   ← MODIFIED (image markers)
├── Storage/
│   └── LocalFileStorage.cs        ← NEW
└── DependencyInjection.cs         ← MODIFIED (register new services)

OrvixFlow.Api/
├── Controllers/
│   ├── FileIngestionController.cs ← NEW
│   └── RagEmailController.cs      ← NEW
└── Jobs/
    └── FileIngestionJob.cs        ← NEW (Hangfire background job)

OrvixFlow.Tests/
├── IngestionPipelineServiceTests.cs ← NEW
├── OverlapChunkerTests.cs          ← NEW
├── HybridSearchTests.cs            ← NEW
├── ImageResolverTests.cs           ← NEW
├── DraftGeneratorWithImagesTests.cs ← NEW
└── RagEmailControllerIntegrationTests.cs ← NEW
```

### 5.2 DI Registration (additions to `DependencyInjection.cs`)

```csharp
// File parsers — registered as IEnumerable<IDocumentParser>
services.AddScoped<IDocumentParser, PlainTextParser>();
services.AddScoped<IDocumentParser, PdfParser>();
services.AddScoped<IDocumentParser, DocxParser>();
services.AddScoped<IDocumentParser, ImageParser>();

// New pipeline services
services.AddScoped<IIngestionPipelineService, IngestionPipelineService>();
services.AddScoped<IImageResolver, ImageResolver>();
services.AddScoped<IRagEmailService, RagEmailService>();
services.AddScoped<IFileStorage, LocalFileStorage>();

// Reranker (configurable)
var rerankerMode = configuration["AI:Reranker"] ?? "LlmScorer";
if (rerankerMode == "LlmScorer")
    services.AddScoped<IReranker, LlmScorerReranker>();
else
    services.AddScoped<IReranker, NoopReranker>();
```

### 5.3 Multi-Tenancy Compliance

All new entities **must** include `TenantId` and be registered in `AppDbContext.OnModelCreating` with `HasQueryFilter`:

```csharp
modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasQueryFilter(d => d.TenantId == _tenantProvider.GetTenantId());
modelBuilder.Entity<KnowledgeBaseImage>()
    .HasQueryFilter(i => i.TenantId == _tenantProvider.GetTenantId());
```

### 5.4 Module Gating

New endpoints must be gated:
```csharp
[RequireModule("knowledge-base")]  // FileIngestionController
[RequireModule("inbox-guardian")]  // RagEmailController
```

---

## 6. Testing Strategy

### 6.1 Unit Tests

| Test class | Coverage target |
|-----------|----------------|
| `OverlapChunkerTests` | Chunk boundaries, overlap, edge cases (empty, single-char) |
| `PlainTextParserTests` | TXT, HTML stripping |
| `PdfParserTests` | Heading detection, image extraction |
| `HybridSearchTests` | RRF merge, score fusion, threshold enforcement |
| `ImageResolverTests` | Score threshold, max images cap, deduplication |
| `DraftGeneratorWithImagesTests` | `[image:N]` marker parsing, fallback with no images |
| `IngestionPipelineServiceTests` | Mocked parsers, verifies chunk count, usage tracking calls |

### 6.2 Integration Tests

| Test class | What it proves |
|-----------|---------------|
| `RagEmailControllerIntegrationTests` | Full pipeline: ingest text → query → n8n payload shape matches contract |
| `TenantIsolationTests (extended)` | Cross-tenant KB documents/images never leak |
| `FileIngestionJobTests` | Hangfire job executes parser → embedder → DB correctly |

### 6.3 Observability

```
Structured log fields added to every RAG operation:
  trace_id           : Guid (propagated via HttpContext)
  tenant_id          : Guid
  operation          : "ingest" | "search" | "rerank" | "draft" | "n8n_call"
  duration_ms        : int
  chunk_count        : int?
  image_count        : int?
  similarity_max     : float?
  model              : string
  tokens_estimated   : int?
  action             : string (n8n action field)
```

Log sink: existing structured logging (Serilog recommended addition; currently uses `ILogger<T>`).

`AuditTrail` records every RAG email answer with `action = "rag.email.answered"` and `decisionDetails` JSON including the above fields.

---

## 7. Security Considerations

| Risk | Mitigation |
|------|-----------|
| File upload malware | MIME type allowlist; optional ClamAV scan via `IVirusScanService` |
| Path traversal in file storage | `StoragePath` is GUID-based, no user input in path |
| Image exfiltration via `fetchUrl` | GET endpoint requires valid JWT or HMAC-signed URL; same tenant check |
| Prompt injection via uploaded documents | Document content treated as DATA, not instructions; same XML-escape pattern as existing `DraftGeneratorService` |
| n8n SSRF | Existing `N8nAutomationPlugin` path validation retained; payload fields validated server-side |
| Large file DoS | File size limit enforced before parsing (configurable, default 20 MB); rate limiting on upload endpoint |
| Embedding API cost explosion | Batch size cap (50); usage tracked via `IUsageService`; plan entitlement `IsWithinStorageLimitAsync` checked before ingestion |

---

## 8. Configuration Reference

All new settings go under existing `appsettings.json` sections:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "...",
      "ModelId": "gpt-4o",
      "EmbeddingModel": "text-embedding-3-small"
    },
    "Reranker": "LlmScorer",
    "Ingestion": {
      "MaxFileSizeMb": 20,
      "AllowedMimeTypes": [
        "text/plain", "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "image/png", "image/jpeg", "image/webp"
      ],
      "ChunkSize": 800,
      "ChunkOverlap": 150,
      "EmbeddingBatchSize": 50
    },
    "ImageResolver": {
      "SimilarityThreshold": 0.65,
      "MaxImagesPerResponse": 3
    }
  },
  "Storage": {
    "Type": "Local",
    "Local": {
      "BasePath": "/app/uploads"
    }
  }
}
```

---

## 9. Created Files Summary

This design specifies creation/modification of the following files:

### New Files

| Path | Purpose |
|------|---------|
| `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` | Document-level metadata entity |
| `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs` | Image storage + caption entity |
| `OrvixFlow.Core/Interfaces/IDocumentParser.cs` | File parser abstraction |
| `OrvixFlow.Core/Interfaces/IIngestionPipelineService.cs` | Pipeline orchestrator interface |
| `OrvixFlow.Core/Interfaces/IImageResolver.cs` | Image relevance resolver interface |
| `OrvixFlow.Core/Interfaces/IRagEmailService.cs` | RAG email orchestrator interface |
| `OrvixFlow.Core/Interfaces/IFileStorage.cs` | File storage abstraction |
| `OrvixFlow.Core/Interfaces/IReranker.cs` | Reranker abstraction |
| `OrvixFlow.Core/Models/EmailDraftResult.cs` | Structured draft output |
| `OrvixFlow.Core/Models/N8nEmailPayload.cs` | Typed n8n JSON contract |
| `OrvixFlow.Infrastructure/Ai/Parsers/PlainTextParser.cs` | TXT/HTML parser |
| `OrvixFlow.Infrastructure/Ai/Parsers/PdfParser.cs` | PDF parser (PdfPig) |
| `OrvixFlow.Infrastructure/Ai/Parsers/DocxParser.cs` | DOCX parser (OpenXml) |
| `OrvixFlow.Infrastructure/Ai/Parsers/ImageParser.cs` | Image + LLM-captioning parser |
| `OrvixFlow.Infrastructure/Ai/Chunking/OverlapChunker.cs` | Paragraph-aware overlap chunker |
| `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` | Full ingestion orchestrator |
| `OrvixFlow.Infrastructure/Ai/ImageResolver.cs` | Image retrieval + threshold filtering |
| `OrvixFlow.Infrastructure/Ai/RagEmailService.cs` | RAG email pipeline (replaces InboxGuardian for n8n use-case) |
| `OrvixFlow.Infrastructure/Ai/Rerankers/LlmScorerReranker.cs` | LLM-based reranker |
| `OrvixFlow.Infrastructure/Ai/Rerankers/NoopReranker.cs` | Passthrough reranker |
| `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` | Disk-based file storage |
| `OrvixFlow.Api/Controllers/FileIngestionController.cs` | Multipart upload endpoint |
| `OrvixFlow.Api/Controllers/RagEmailController.cs` | n8n-facing RAG endpoint |
| `OrvixFlow.Api/Jobs/FileIngestionJob.cs` | Hangfire background indexing job |
| `OrvixFlow.Infrastructure/Migrations/AddRagExtension.cs` | DB migration (new tables + indexes) |
| `OrvixFlow.Tests/IngestionPipelineServiceTests.cs` | Pipeline unit tests |
| `OrvixFlow.Tests/OverlapChunkerTests.cs` | Chunker unit tests |
| `OrvixFlow.Tests/HybridSearchTests.cs` | Retrieval unit tests |
| `OrvixFlow.Tests/ImageResolverTests.cs` | Image resolver unit tests |
| `OrvixFlow.Tests/DraftGeneratorWithImagesTests.cs` | Draft generator unit tests |
| `OrvixFlow.Tests/RagEmailControllerIntegrationTests.cs` | Integration tests |
| `tasks/n8n-sample-workflow.json` | Sample n8n workflow to import |

### Modified Files

| Path | Change |
|------|--------|
| `OrvixFlow.Core/Entities/KnowledgeBase.cs` | Add `DocumentId`, `ChunkIndex`, `ChunkType`, `Title` |
| `OrvixFlow.Core/Models/KnowledgeSnippet.cs` | Add `Title`, `ChunkType`, `DocumentId`, `RelatedImages` |
| `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs` | Hybrid (dense+sparse) + RRF + reranker |
| `OrvixFlow.Infrastructure/Ai/DraftGeneratorService.cs` | Image context in prompt + `[image:N]` parsing |
| `OrvixFlow.Infrastructure/Ai/Plugins/N8nAutomationPlugin.cs` | Structured payload, new `SendEmailDraftAsync` function |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | Register new DbSets + query filters |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Register all new services |

---

## 10. Quick-Start Implementation Order

For a developer starting Phase 1 today:

```
1. Add NuGet packages to OrvixFlow.Infrastructure.csproj
2. Create KnowledgeBaseDocument.cs and KnowledgeBaseImage.cs (Core)
3. Extend KnowledgeBase.cs with new columns
4. Run: dotnet ef migrations add AddRagExtension --project OrvixFlow.Infrastructure
5. Run: dotnet ef database update --project OrvixFlow.Infrastructure
6. Implement OverlapChunker.cs (no dependencies — easy to TDD first)
7. Implement PlainTextParser.cs
8. Implement IFileStorage + LocalFileStorage
9. Implement IngestionPipelineService.cs
10. Add FileIngestionController.cs + FileIngestionJob.cs
11. Wire DI in DependencyInjection.cs
12. Write and run OverlapChunkerTests.cs + IngestionPipelineServiceTests.cs
```
