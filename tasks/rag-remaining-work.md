# RAG Extension — Status & Remaining Work
<!-- CANONICAL REFERENCE: AI agents should read this file at the start of any RAG work session. -->
<!-- TOOL CONTEXT: SK version = 1.73.0, .NET 9.0, EF Core 9, pgvector, Next.js 16 (React 19, TypeScript) -->
<!-- TDD POLICY: Write failing test first → implement → verify green → commit. -->

> Backend Phases 1–5 are complete and production-hardened.
> This document is the execution plan for all remaining items.

---

## Completed Phases (Read-Only Summary)

- **Phase 1** — Core ingestion pipeline (parse → chunk → embed → persist via Hangfire)
- **Phase 2** — Hybrid vector search (pgvector + FTS + RRF + LLM reranker)
- **Phase 3** — Multi-modal image support (extract captions, embed, resolve)
- **Phase 4** — RAG email orchestration (classify → search → draft → n8n payload)
- **Phase 5** — Security/observability hardening (rate limiting, virus scan hook, metrics, health check)
- **Phase A** — Quick wins: Console.WriteLine cleanup, ImageSharp CVE fix, MockEmbeddingGenerator prep

Test baseline: **278 passed / 0 failures**.

---

## Execution Plan

Work is grouped into 5 phases. Execute **in order** — each phase is independent once
the previous is green. Use TDD for every item with a backend change.

---

## Phase A — Quick Wins

**Estimated effort:** ~45 min  
**Goal:** Clean build baseline with 0 debug noise and 0 CVEs.  
**Run after:** `dotnet build` (confirm 0 errors baseline)

---

### A1 — Remove `Console.WriteLine` from OrganizationController

**File:** `OrvixFlow.Api/Controllers/OrganizationController.cs`

**Lines to fix:** 77, 82, 295, 299, 306, 310, 316, 331

**Steps:**
1. Open the file. Confirm `ILogger<OrganizationController>` is injected; if not, add it:
   ```csharp
   private readonly ILogger<OrganizationController> _logger;
   // in constructor: _logger = logger;
   ```
2. Replace every `Console.WriteLine(...)` with `_logger.LogDebug(...)` using the same message content.
3. `dotnet build` → 0 errors; grep confirms no `Console.WriteLine` in the file.

**No test needed** — this is a style/dev-ops fix. Verified by grep + build.

```
Verify: grep -n "Console.WriteLine" OrvixFlow.Api/Controllers/OrganizationController.cs
Expected: no output
```

---

### A2 — Bump SixLabors.ImageSharp

**File:** `OrvixFlow.Infrastructure/OrvixFlow.Infrastructure.csproj`

**Current:** `3.1.5` (CVE: High + Moderate)

**Steps:**
```bash
dotnet add OrvixFlow.Infrastructure package SixLabors.ImageSharp
dotnet list OrvixFlow.Infrastructure package --vulnerable
```
Expected after: `SixLabors.ImageSharp` not listed in vulnerable packages.

**Then:** `dotnet build` → 0 errors, NU1903/NU1902 warnings gone.

---

## Phase B — Fix Failing Tests

**Estimated effort:** ~30 min  
**Goal:** All 8 `OrgHierarchyTests` passing.

**Failing tests:**
- `CreateOrganization_AssignsCompanyOwnerRole`
- `CreateOrganization_ReturnsConflict_WhenNameExists`

**Root cause:** `OrganizationController.CreateOrganization` calls
`_db.Users.AnyAsync(u => u.Id == userId)` as an existence guard. The test DB has no
`User` row, so it returns `Unauthorized`.

**The fix is already in the test file** (lines 248–250 and 283–284 seed a `User` row).
The tests just need the controller to have the user-existence guard present.

**Steps:**
1. Open `OrvixFlow.Api/Controllers/OrganizationController.cs`, find `CreateOrganization`.
2. Confirm user-existence check is present:
   ```csharp
   var userExists = await _db.Users.AnyAsync(u => u.Id == userId);
   if (!userExists) return Unauthorized();
   ```
   If missing → add it before the name-conflict check.
3. Confirm tests already seed a `User` (see `OrvixFlow.Tests/OrgHierarchyTests.cs` L248-250):
   ```csharp
   _db.Users.Add(new User { Id = userId, Email = "test@example.com", PasswordHash = "hashed" });
   await _db.SaveChangesAsync();
   ```
4. Run:
   ```bash
   dotnet test --filter "FullyQualifiedName~OrgHierarchyTests"
   ```
   Expected: **8/8 passed**.

---

## Phase C — Migrate ITextEmbeddingGenerationService → IEmbeddingGenerator

**Estimated effort:** ~2h  
**SK version:** 1.73.0 — `AddOpenAIEmbeddingGenerator` is confirmed available.  
**Goal:** Zero `CS0618` obsolete warnings; all existing tests still green.

### API Reference (SK 1.73.0)

| | Old (obsolete) | New |
|---|---|---|
| Interface | `Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService` | `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>` |
| DI registration | `kernelBuilder.Services.AddSingleton<ITextEmbeddingGenerationService, ...>()` | See per-case below |
| OpenAI registration | `kernelBuilder.AddOpenAITextEmbeddingGeneration(modelId, apiKey)` | `kernelBuilder.AddOpenAIEmbeddingGenerator(modelId, apiKey)` |
| Method | `GenerateEmbeddingsAsync(IList<string>)` → `IList<ReadOnlyMemory<float>>` | `GenerateAsync(IEnumerable<string>)` → `GeneratedEmbeddings<Embedding<float>>` |
| Single embed | `GenerateEmbeddingAsync(string s)` | `(await GenerateAsync([s]))[0].Vector` |
| using | `using Microsoft.SemanticKernel.Embeddings;` | `using Microsoft.Extensions.AI;` |

---

### TDD Steps

**Step C0 — Write test guard (before any production changes)**

In `OrvixFlow.Tests/EmbeddingMigrationSmokeTests.cs` (new file):
```csharp
// Verifies the new IEmbeddingGenerator<string, Embedding<float>> mock works correctly.
// This test must FAIL until the mock is implemented, then go GREEN.

using Microsoft.Extensions.AI;
using Xunit;

namespace OrvixFlow.Tests;

public class EmbeddingMigrationSmokeTests
{
    [Fact]
    public async Task MockEmbeddingGenerator_Returns1536DimVector()
    {
        var generator = new OrvixFlow.Infrastructure.Ai.Mock.MockEmbeddingGenerator();
        var results = await generator.GenerateAsync(["hello world"]);
        Assert.Single(results);
        Assert.Equal(1536, results[0].Vector.Length);
    }
}
```
Run → **RED** (MockEmbeddingGenerator doesn't exist yet).

---

**Step C1 — Create MockEmbeddingGenerator**

Create `OrvixFlow.Infrastructure/Ai/Mock/MockEmbeddingGenerator.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace OrvixFlow.Infrastructure.Ai.Mock;

public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata => new("mock", null, "mock-embed", 1536);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(text =>
        {
            var vector = new float[1536];
            var val = string.IsNullOrEmpty(text) ? 0.0f : (float)text[0] / 255.0f;
            for (int i = 0; i < vector.Length; i++) vector[i] = val;
            return new Embedding<float>(vector);
        }).ToList();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public TService? GetService<TService>(object? key = null) where TService : class
        => this as TService;

    public void Dispose() { }
}
```
Run smoke test → **GREEN**.

---

**Step C2 — Update DependencyInjection.cs**

File: `OrvixFlow.Infrastructure/DependencyInjection.cs`

Replace all `ITextEmbeddingGenerationService` registrations:

```csharp
// REMOVE (2 places):
kernelBuilder.Services.AddSingleton<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService,
    OrvixFlow.Infrastructure.Ai.Mock.MockTextEmbeddingGenerationService>();

// ADD (Mock path):
kernelBuilder.Services.AddSingleton<Microsoft.Extensions.AI.IEmbeddingGenerator<string,
    Microsoft.Extensions.AI.Embedding<float>>,
    OrvixFlow.Infrastructure.Ai.Mock.MockEmbeddingGenerator>();

// REMOVE (OpenAI/Groq fallback):
kernelBuilder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey, httpClient: ...);
// (same pattern with or without httpClient)

// ADD (OpenAI/Groq fallback):
kernelBuilder.AddOpenAIEmbeddingGenerator("text-embedding-3-small", apiKey, httpClient: ...);
// (keep conditional httpClient logic the same)
```

Add `using Microsoft.Extensions.AI;` at top of file.

---

**Step C3 — Update production services**

For each file, replace:
```csharp
private readonly ITextEmbeddingGenerationService _embeddingService;
// ctor: ITextEmbeddingGenerationService embeddingService
```
with:
```csharp
private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
// ctor: IEmbeddingGenerator<string, Embedding<float>> embeddingService
```
Add `using Microsoft.Extensions.AI;` and remove `using Microsoft.SemanticKernel.Embeddings;`.

**Files:**
- `OrvixFlow.Infrastructure/Ai/HybridVectorSearchService.cs` — line 23+32
- `OrvixFlow.Infrastructure/Ai/ImageResolver.cs` — line 19+23
- `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` — line 24+38
- `OrvixFlow.Infrastructure/Ai/IngestionService.cs` — line 13+19
- `OrvixFlow.Infrastructure/Ai/Plugins/KnowledgeBaseSearchPlugin.cs` — line 16+20

**Call site changes (`GenerateEmbeddingsAsync` → `GenerateAsync`):**

```csharp
// OLD:
var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { query });
var vector = embeddings[0];

// NEW:
var embeddings = await _embeddingService.GenerateAsync(new[] { query });
var vector = embeddings[0].Vector;
```

```csharp
// OLD (single):
var embedding = await _embeddingService.GenerateEmbeddingAsync(content);

// NEW:
var embeddings = await _embeddingService.GenerateAsync([content]);
var embedding = embeddings[0].Vector;
```

Exact call sites:
- `HybridVectorSearchService.cs:45` — `GenerateEmbeddingsAsync(new[] { query })` → update vector extraction
- `ImageResolver.cs:37` — same pattern
- `IngestionPipelineService.cs:110` — chunks: `embeddings[0].Vector`
- `IngestionPipelineService.cs:142` — captions: `captionEmbeddings[0].Vector`
- `IngestionService.cs:37` — `GenerateEmbeddingAsync` → single GenerateAsync
- `KnowledgeBaseSearchPlugin.cs:31` — `GenerateEmbeddingAsync` → single GenerateAsync

---

**Step C4 — Update test mocks**

Files:
- `OrvixFlow.Tests/IngestionServiceTests.cs:23`
- `OrvixFlow.Tests/IngestionPipelineServiceTests.cs:24+37`
- `OrvixFlow.Tests/RagImageSupportTests.cs:94`

Replace:
```csharp
var mockEmbeddingService = new Mock<ITextEmbeddingGenerationService>();
mockEmbeddingService
    .Setup(s => s.GenerateEmbeddingsAsync(It.IsAny<IList<string>>(), null, default))
    .ReturnsAsync(new List<ReadOnlyMemory<float>> { new float[1536] });
```
with:
```csharp
var mockEmbeddingService = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
mockEmbeddingService
    .Setup(s => s.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
    .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
        [new Embedding<float>(new float[1536])]));
```
Add `using Microsoft.Extensions.AI;` to each test file.

---

**Step C5 — Verify**
```bash
dotnet build
# Expected: 0 errors, CS0618 warnings gone (was ~20)

dotnet test
# Expected: all previously passing tests green, smoke test green
```

---

## Phase D — Document Listing Endpoint + File Upload UI

**Estimated effort:** ~4h  
**Goal:** Users can upload files and view document status in the browser.

---

### D1 — TDD: Document Listing Endpoint

**File:** `OrvixFlow.Api/Controllers/FileIngestionController.cs`

**Step D1a — Write failing test first**

In `OrvixFlow.Tests/FileIngestionControllerTests.cs` (new file):
```csharp
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class FileIngestionControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ITenantProvider> _tenantMock;
    private readonly Guid _tenantId = Guid.NewGuid();

    public FileIngestionControllerTests()
    {
        _tenantMock = new Mock<ITenantProvider>();
        _tenantMock.Setup(t => t.GetTenantId()).Returns(_tenantId);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options, _tenantMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private FileIngestionController BuildController()
    {
        var claims = new List<Claim>
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("TenantId", _tenantId.ToString()),
            new Claim("ActiveCompanyId", _tenantId.ToString()),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var controller = new FileIngestionController(
            _db,
            Mock.Of<IFileStorage>(),
            _tenantMock.Object,
            Mock.Of<IScopeContext>(),
            Mock.Of<Hangfire.IBackgroundJobClient>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<IVirusScanService>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    [Fact]
    public async Task GetDocuments_ReturnsTenantDocuments_Paged()
    {
        // Arrange: seed 3 documents for this tenant
        _db.KnowledgeBaseDocuments.AddRange(
            new KnowledgeBaseDocument { TenantId = _tenantId, FileName = "a.pdf", ContentType = "application/pdf", Status = "Indexed" },
            new KnowledgeBaseDocument { TenantId = _tenantId, FileName = "b.txt", ContentType = "text/plain", Status = "Pending" },
            new KnowledgeBaseDocument { TenantId = _tenantId, FileName = "c.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Status = "Failed" }
        );
        await _db.SaveChangesAsync();

        var ctrl = BuildController();

        // Act
        var result = await ctrl.GetDocuments(page: 1, pageSize: 10) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        var body = result!.Value!;
        var total = (int)body.GetType().GetProperty("total")!.GetValue(body)!;
        var items = (IEnumerable<object>)body.GetType().GetProperty("items")!.GetValue(body)!;
        Assert.Equal(3, total);
        Assert.Equal(3, items.Count());
    }

    [Fact]
    public async Task GetDocuments_IsolatesTenants()
    {
        // Other tenant's document — must NOT appear
        var otherId = Guid.NewGuid();
        _db.KnowledgeBaseDocuments.Add(
            new KnowledgeBaseDocument { TenantId = otherId, FileName = "other.pdf", ContentType = "application/pdf", Status = "Indexed" });
        await _db.SaveChangesAsync();

        var ctrl = BuildController();
        var result = await ctrl.GetDocuments(1, 10) as OkObjectResult;
        var body = result!.Value!;
        var total = (int)body.GetType().GetProperty("total")!.GetValue(body)!;
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task DeleteDocument_HardDeletesCascade()
    {
        // Arrange
        var doc = new KnowledgeBaseDocument
        {
            TenantId = _tenantId,
            FileName = "del.pdf",
            ContentType = "application/pdf",
            Status = "Indexed",
            StoragePath = "/some/path"
        };
        _db.KnowledgeBaseDocuments.Add(doc);
        await _db.SaveChangesAsync();

        var storageMock = new Mock<IFileStorage>();
        storageMock.Setup(s => s.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        var ctrl = new FileIngestionController(
            _db, storageMock.Object, _tenantMock.Object,
            Mock.Of<IScopeContext>(), Mock.Of<Hangfire.IBackgroundJobClient>(),
            Mock.Of<IConfiguration>(), Mock.Of<IVirusScanService>());
        ctrl.ControllerContext = BuildController().ControllerContext;

        // Act
        var result = await ctrl.DeleteDocument(doc.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.Equal(0, await _db.KnowledgeBaseDocuments.CountAsync());
        storageMock.Verify(s => s.DeleteFileAsync("/some/path"), Times.Once);
    }
}
```

Run → **RED** (GetDocuments and DeleteDocument don't exist yet).

**Step D1b — Implement `GetDocuments` + `DeleteDocument` in FileIngestionController.cs**

Add below the existing `UploadFile` method:

```csharp
[HttpGet("/api/v1/knowledge/documents")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> GetDocuments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
{
    var tenantId = _tenantProvider.GetTenantId();
    var query = _dbContext.KnowledgeBaseDocuments
        .Where(d => d.TenantId == tenantId)
        .OrderByDescending(d => d.CreatedAtUtc);

    var total = await query.CountAsync();
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(d => new
        {
            d.Id,
            d.FileName,
            d.ContentType,
            d.FileSizeBytes,
            d.Status,
            d.ErrorMessage,
            d.CreatedAtUtc,
            d.IndexedAtUtc
        })
        .ToListAsync();

    return Ok(new { total, page, pageSize, items });
}

[HttpDelete("/api/v1/knowledge/documents/{id:guid}")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> DeleteDocument(Guid id)
{
    var tenantId = _tenantProvider.GetTenantId();
    var doc = await _dbContext.KnowledgeBaseDocuments
        .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

    if (doc == null) return NotFound();

    // Hard delete: remove file from storage if present
    if (!string.IsNullOrEmpty(doc.StoragePath))
    {
        await _storage.DeleteFileAsync(doc.StoragePath);
    }

    _dbContext.KnowledgeBaseDocuments.Remove(doc);
    await _dbContext.SaveChangesAsync();

    return NoContent();
}
```

Add required `using Microsoft.EntityFrameworkCore;` if not already present.

**Note:** `IFileStorage` must expose `DeleteFileAsync(string path)`. Check `OrvixFlow.Core/Interfaces/IFileStorage.cs` — if missing, add:
```csharp
Task DeleteFileAsync(string storagePath);
```
And implement in `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`:
```csharp
public Task DeleteFileAsync(string storagePath)
{
    if (File.Exists(storagePath)) File.Delete(storagePath);
    return Task.CompletedTask;
}
```

Run tests → **GREEN**.

---

### D2 — Frontend File Upload UI

**File:** `orvixflow-web/app/(dashboard)/knowledge/page.tsx`

**Strategy:** Replace the entire page. Keep the existing "Direct Ingestion" text panel as a secondary tab. Add a primary "Documents" tab with the upload zone and document table.

The new page has:
- **Tab bar:** "Documents" (new, default) | "Text Snippets" (existing list)
- **Upload zone:** drag-and-drop or click, shows preview, submits to `/api/v1/knowledge/upload`
- **Document table:** lists `KnowledgeBaseDocument` records from `/api/v1/knowledge/documents`, polls status
- **Delete:** calls `DELETE /api/v1/knowledge/documents/{id}`, optimistic removal

**Key implementation details:**

```typescript
// Types
interface Document {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: 'Pending' | 'Processing' | 'Indexed' | 'Failed';
  errorMessage?: string;
  createdAtUtc: string;
  indexedAtUtc?: string;
}

// Upload: use XMLHttpRequest for upload progress
const uploadFile = (file: File, token: string, headers: Record<string,string>) => {
  return new Promise<{documentId: string}>((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) setUploadProgress(Math.round((e.loaded / e.total) * 100));
    };
    xhr.onload = () => {
      if (xhr.status === 200) resolve(JSON.parse(xhr.responseText));
      else reject(new Error(`Upload failed: ${xhr.status}`));
    };
    xhr.onerror = () => reject(new Error('Network error'));
    xhr.open('POST', `${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/upload`);
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    const form = new FormData();
    form.append('file', file);
    xhr.send(form);
  });
};

// Status polling: after upload, poll the document every 3s until Indexed or Failed (max 10 polls)
const pollStatus = async (docId: string, token: string) => {
  for (let i = 0; i < 10; i++) {
    await new Promise(r => setTimeout(r, 3000));
    const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/v1/knowledge/documents?page=1&pageSize=100`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    const data = await res.json();
    const doc = data.items.find((d: Document) => d.id === docId);
    if (!doc || doc.status === 'Indexed' || doc.status === 'Failed') break;
    setDocuments(prev => prev.map(d => d.id === docId ? { ...d, status: doc.status } : d));
  }
  await fetchDocuments(); // final refresh
};

// Accepted MIME types (must match backend whitelist)
const ACCEPTED = '.pdf,.docx,.txt,.png,.jpg,.jpeg,.webp';

// Status badge colors
const statusBadge = (status: string) => ({
  Pending:    'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
  Processing: 'bg-blue-500/10 text-blue-400 border-blue-500/20',
  Indexed:    'bg-green-500/10 text-green-400 border-green-500/20',
  Failed:     'bg-red-500/10 text-red-400 border-red-500/20',
}[status] ?? 'bg-gray-500/10 text-gray-400');

// Size formatter
const fmtSize = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
};

// MIME → display type
const fmtType = (ct: string) => (
  ct.includes('pdf') ? 'PDF' :
  ct.includes('word') ? 'DOCX' :
  ct.includes('text') ? 'TXT' :
  ct.includes('image') ? 'Image' : 'File'
);
```

**State vars needed:**
```typescript
const [tab, setTab] = useState<'documents' | 'snippets'>('documents');
const [documents, setDocuments] = useState<Document[]>([]);
const [docsLoading, setDocsLoading] = useState(true);
const [docsError, setDocsError] = useState<string | null>(null);
const [dragOver, setDragOver] = useState(false);
const [selectedFile, setSelectedFile] = useState<File | null>(null);
const [uploadProgress, setUploadProgress] = useState(0);
const [uploading, setUploading] = useState(false);
const [uploadResult, setUploadResult] = useState<{ok: boolean; msg: string} | null>(null);
```

**UI structure (JSX outline):**
```
<div> (main container)
  <header> (title + tab bar)
  <tabs>
    [tab === 'documents']
      <div grid cols-3>
        <div col-span-2> (document table with status badges + delete)
        <div col-span-1> (upload zone + file preview + progress bar)
    [tab === 'snippets']
      <existing text ingest section>
```

**Drag-and-drop zone:**
```tsx
<div
  id="upload-zone"
  className={`border-2 border-dashed rounded-xl p-8 text-center transition-all cursor-pointer
    ${dragOver ? 'border-primary bg-primary/5' : 'border-white/10 hover:border-primary/40'}`}
  onDragOver={(e) => { e.preventDefault(); setDragOver(true); }}
  onDragLeave={() => setDragOver(false)}
  onDrop={(e) => { e.preventDefault(); setDragOver(false); handleFileDrop(e.dataTransfer.files[0]); }}
  onClick={() => fileInputRef.current?.click()}
>
  <UploadCloud className="w-10 h-10 mx-auto mb-3 text-muted" />
  <p className="text-sm text-white/70">Drop file here or <span className="text-primary">browse</span></p>
  <p className="text-xs text-muted mt-1">PDF, DOCX, TXT, PNG, JPG, WEBP · max 20 MB</p>
</div>
<input ref={fileInputRef} type="file" accept={ACCEPTED} className="hidden"
  onChange={(e) => e.target.files?.[0] && setSelectedFile(e.target.files[0])} />
```

**Verification (manual after docker up):**
```
1. Navigate to /knowledge
2. Drag a PDF onto the upload zone → file appears in preview
3. Click "Upload" → progress bar animates
4. Document appears in table with status=Pending → transitions to Indexed
5. Click delete → document disappears (confirmed via API)
6. Tab "Text Snippets" → existing ingest UI is unchanged
```

---

## Phase E — Real Virus Scanning (ClamAV)

**Estimated effort:** ~3h  
**Goal:** Replace `NoopVirusScanService` with a real ClamAV integration.

### E1 — TDD: ClamAV Service Tests

Create `OrvixFlow.Tests/ClamAvVirusScanServiceTests.cs`:
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Moq;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Services.Security;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for ClamAvVirusScanService — uses a mock IClamAvClient so no running daemon needed.
/// </summary>
public class ClamAvVirusScanServiceTests
{
    [Fact]
    public async Task IsFileSafeAsync_WhenClamReturnsClean_ReturnsTrue()
    {
        var clientMock = new Mock<IClamAvClient>();
        clientMock
            .Setup(c => c.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(ClamScanResult.Clean);

        var svc = new ClamAvVirusScanService(clientMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await svc.IsFileSafeAsync(stream, "test.pdf");

        Assert.True(result);
    }

    [Fact]
    public async Task IsFileSafeAsync_WhenClamReturnsInfected_ReturnsFalse()
    {
        var clientMock = new Mock<IClamAvClient>();
        clientMock
            .Setup(c => c.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(ClamScanResult.Infected);

        var svc = new ClamAvVirusScanService(clientMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await svc.IsFileSafeAsync(stream, "eicar.txt");

        Assert.False(result);
    }

    [Fact]
    public async Task IsFileSafeAsync_WhenClamThrows_ReturnsFalseAndLogsError()
    {
        var clientMock = new Mock<IClamAvClient>();
        clientMock
            .Setup(c => c.ScanAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("ClamAV daemon unreachable"));

        var svc = new ClamAvVirusScanService(clientMock.Object);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await svc.IsFileSafeAsync(stream, "file.pdf");

        Assert.False(result); // fail-safe: reject file if scan unavailable
    }
}
```
Run → **RED**.

---

### E2 — Implement ClamAV Integration

**Add NuGet:**
```bash
dotnet add OrvixFlow.Infrastructure package nClam
```

**Create** `OrvixFlow.Core/Interfaces/IClamAvClient.cs`:
```csharp
using System.IO;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public enum ClamScanResult { Clean, Infected, Error }

public interface IClamAvClient
{
    Task<ClamScanResult> ScanAsync(Stream fileStream, string fileName);
}
```

**Create** `OrvixFlow.Infrastructure/Services/Security/NclamClient.cs`:
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services.Security;

public class ClamAvOptions
{
    public string Host { get; set; } = "clamav";
    public int Port { get; set; } = 3310;
}

public class NclamClient : IClamAvClient
{
    private readonly ClamAvOptions _opts;
    private readonly ILogger<NclamClient> _logger;

    public NclamClient(IOptions<ClamAvOptions> opts, ILogger<NclamClient> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task<ClamScanResult> ScanAsync(Stream fileStream, string fileName)
    {
        var clam = new ClamClient(_opts.Host, _opts.Port);
        var result = await clam.SendAndScanFileAsync(fileStream);

        return result.Result switch
        {
            ClamScanResults.Clean => ClamScanResult.Clean,
            ClamScanResults.VirusDetected => { 
                _logger.LogWarning("Virus detected in {FileName}: {Virus}", fileName, result.InfectedFiles?.FirstOrDefault()?.VirusName);
                return ClamScanResult.Infected;
            },
            _ => ClamScanResult.Error
        };
    }
}
```

**Create** `OrvixFlow.Infrastructure/Services/Security/ClamAvVirusScanService.cs`:
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Services.Security;

public class ClamAvVirusScanService : IVirusScanService
{
    private readonly IClamAvClient _client;
    private readonly ILogger<ClamAvVirusScanService>? _logger;

    public ClamAvVirusScanService(IClamAvClient client, ILogger<ClamAvVirusScanService>? logger = null)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> IsFileSafeAsync(Stream fileStream, string fileName)
    {
        try
        {
            var result = await _client.ScanAsync(fileStream, fileName);
            return result == ClamScanResult.Clean;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ClamAV scan failed for {FileName} — rejecting as fail-safe", fileName);
            return false; // fail-safe: reject on scanner error
        }
    }
}
```

Run tests → **GREEN**.

---

### E3 — DI Registration with Config Toggle

In `OrvixFlow.Infrastructure/DependencyInjection.cs`, replace:
```csharp
services.AddScoped<IVirusScanService, NoopVirusScanService>();
```
with:
```csharp
var virusScanProvider = configuration["Security:VirusScan:Provider"] ?? "Noop";

if (virusScanProvider == "ClamAV")
{
    services.Configure<ClamAvOptions>(configuration.GetSection("Security:VirusScan"));
    services.AddScoped<IClamAvClient, NclamClient>();
    services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
}
else
{
    services.AddScoped<IVirusScanService, NoopVirusScanService>();
}
```

**appsettings.json** (add under `AI` section):
```json
"Security": {
  "VirusScan": {
    "Provider": "Noop",
    "Host": "clamav",
    "Port": 3310
  }
}
```

**docker-compose.yml** — add ClamAV service:
```yaml
orvix-clamav:
  image: clamav/clamav:stable
  container_name: orvix_clamav
  ports:
    - "3310:3310"
  restart: unless-stopped
```

To enable in production:
```json
"Security": { "VirusScan": { "Provider": "ClamAV", "Host": "orvix-clamav", "Port": 3310 } }
```

---

## Phase F — n8n Workflow Template

**Estimated effort:** ~1h  
**Goal:** Commit a reusable n8n workflow JSON for the RAG email pipeline.

### F1 — Export workflow

1. Start n8n: `docker compose up -d orvix-n8n`
2. Open http://localhost:5678
3. Create a new workflow with these nodes:
   - **Webhook Trigger** — listens on `/webhook/rag-inbox`
   - **HTTP Request** — `POST {{$env.ORVIX_API_URL}}/api/v1/inbox/rag` with body `{{ $json }}` and header `X-Automation-Key: {{$env.AUTOMATION_KEY}}`
   - **Switch** — routes on `{{ $json.Action }}`:
     - `draft_ready` → **Send Email** (or Slack notify)
     - `human_review_required` → **Create ActionRequest** (HTTP to `/api/actions`)
     - `insufficient_context` → **Log + Notify**
     - `escalate` → **PagerDuty / Slack alert**
     - `spam_detected` → **Discard / Archive**
4. Set webhook node to `Respond Immediately`.
5. Export: ··· menu → Download → save as `tasks/n8n-rag-email-workflow.json`

### F2 — Commit

```bash
git add tasks/n8n-rag-email-workflow.json
git add tasks/rag-remaining-work.md
git commit -m "docs: add n8n RAG email workflow template"
```

---

## Progress Tracker

| # | Item | Phase | Status |
|---|------|-------|--------|
| 1 | Frontend file upload UI | D | ⬜ Todo |
| 2 | Fix OrgHierarchyTests | B | ⬜ Todo |
| 3 | Migrate ITextEmbeddingGenerationService | C | ⬜ Todo |
| 4 | Bump ImageSharp | A | ⬜ Todo |
| 5 | ClamAV virus scanning | E | ⬜ Todo |
| 6 | Remove Console.WriteLine | A | ⬜ Todo |
| 7 | n8n workflow template | F | ⬜ Todo |
| 8 | Document listing endpoint | D | ⬜ Todo |

Mark items `✅ Done` as you complete each phase. Run `dotnet test` after every phase.

---

## Final Verification Checklist

```bash
# After all phases:
dotnet build
# → 0 errors, ~0 CS0618 warnings

dotnet test
# → all tests pass (at minimum 226 + new tests)

grep -rn "Console.WriteLine" OrvixFlow.Api/Controllers/OrganizationController.cs
# → no output

dotnet list OrvixFlow.Infrastructure package --vulnerable
# → no output (ImageSharp clean)
```

Manual:
- [ ] `/health/rag` returns healthy
- [ ] Navigate to `/knowledge` → upload a PDF → document appears with status polling
- [ ] Delete a document → removed from table and disk
- [ ] Run with `Provider=ClamAV` config → clean file passes, EICAR test file blocked
