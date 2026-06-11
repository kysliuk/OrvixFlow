# Phase 07 — FileIngestionJob Adaptation for Object Storage

> **Obsolete / Historical Migration Plan**
> Superseded by later ingestion/storage implementation by 2026-06-11.
> This file is historical and should not be used as the current implementation guide.

## Phase Goal

Verify and harden `FileIngestionJob` to work correctly with object-storage keys (MinIO S3 keys or Azure Blob keys) rather than implicitly relying on filesystem paths.  
Add retry around object-fetch failures (transient network errors to MinIO).  
Ensure `IngestionPipelineService` image saves are consistent with the current storage provider.

---

## Phase Purpose

`FileIngestionJob` currently receives `storagePath` as a raw string parameter. When `LocalFileStorage` was used, this was an absolute filesystem path. When MinIO is active, it will be an S3 key (e.g., `tenants/{uuid}/depts/__company__/docs/{uuid}/{uuid}.pdf`).

The good news: the interface `IFileStorage.GetFileAsync(string storagePath)` is unchanged. MinIO's implementation accepts S3 keys. So **the job itself does not need code changes for the happy path**. But there are edge cases and hardening gaps to address.

---

## Scope

### Files to Review and Possibly Modify

| File | Reason |
|------|--------|
| `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs` | Verify no filesystem assumptions; add retry |
| `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` | Image save uses legacy overload; verify correct DepartmentId flows through |
| `OrvixFlow.Api/Controllers/FileIngestionController.cs` | Verify `departmentId` is passed to `FileIngestionJob` enqueue call |

---

## Prerequisites

- Phase 05 complete (upload flow includes `departmentId`)
- Phase 06 complete (`StoredObject` rows created)
- `dotnet test` passes

---

## Current `FileIngestionJob` Analysis

```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
public async Task ProcessFileAsync(Guid documentId, string storagePath, string fileName, 
    string contentType, Guid? userId = null, Guid? departmentId = null, Guid tenantId = default)
{
    using var scope = _serviceProvider.CreateScope();
    var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipelineService>();
    var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

    using (var stream = await storage.GetFileAsync(storagePath))     // ← this line
    {
        var result = await pipeline.IngestFileAsync(stream, fileName, contentType, documentId, tenantId, userId, departmentId);
    }
}
```

**Assessment:**  
- `storage.GetFileAsync(storagePath)` — works for MinIO. `storagePath` is now an S3 key. `MinIOFileStorage.GetFileAsync(string storagePath)` calls `_s3.GetObjectAsync(_bucket, storagePath)`. This is correct.
- The `Hangfire.AutomaticRetry(Attempts = 3)` attribute means Hangfire retries the whole job on any exception. This is insufficient for granular retry on the storage fetch step.
- No specific exception handling for "file not found in storage" (S3 NoSuchKey) — the job would retry 3 times unnecessarily.

---

## Implementation Instructions

### Step 1 — Add Retry on Storage Fetch

The `FileIngestionJob.ProcessFileAsync` body currently has a plain `storage.GetFileAsync(storagePath)`. Wrap it with retry for transient issues:

```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
public async Task ProcessFileAsync(Guid documentId, string storagePath, string fileName, 
    string contentType, Guid? userId = null, Guid? departmentId = null, Guid tenantId = default)
{
    _logger.LogInformation(
        "Background ingestion started: file={FileName} doc={DocumentId} tenant={TenantId} storage={Key}",
        fileName, documentId, tenantId, storagePath);

    using var scope = _serviceProvider.CreateScope();
    var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipelineService>();
    var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

    Stream? fileStream = null;

    try
    {
        // Retry transient storage fetch errors (network blip to MinIO)
        // Does NOT retry on "NoSuchKey" — that is a permanent error
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                fileStream = await storage.GetFileAsync(storagePath);
                break;
            }
            catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                // Permanent error — file does not exist in storage
                _logger.LogError(
                    "File not found in object storage: key={Key} doc={DocumentId}. Not retrying.",
                    storagePath, documentId);
                // Update document status to Failed, then return
                await UpdateDocumentStatusAsync(scope, documentId, "Failed",
                    $"File not found in storage at key: {storagePath}");
                return;
            }
            catch (Exception ex) when (attempt < 3)
            {
                // Transient error — retry after delay
                _logger.LogWarning(ex,
                    "Transient storage fetch error on attempt {Attempt}/3: key={Key}", attempt, storagePath);
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
            }
        }

        if (fileStream == null)
        {
            _logger.LogError("All storage fetch attempts failed for key={Key}", storagePath);
            throw new InvalidOperationException($"Cannot fetch file from storage key: {storagePath}");
        }

        using (fileStream)
        {
            var result = await pipeline.IngestFileAsync(
                fileStream, fileName, contentType, documentId, tenantId, userId, departmentId);

            if (result.ErrorMessage != null)
            {
                _logger.LogError("Ingestion failed for {FileName}: {Error}", fileName, result.ErrorMessage);
            }
            else
            {
                _logger.LogInformation(
                    "Ingestion complete: {FileName} chunks={Chunks} images={Images}",
                    fileName, result.ChunkCount, result.ImageCount);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error in FileIngestionJob for file={FileName}", fileName);
        throw; // Hangfire will retry via [AutomaticRetry]
    }
}

private static async Task UpdateDocumentStatusAsync(IServiceScope scope, Guid documentId, string status, string error)
{
    var db = scope.ServiceProvider.GetRequiredService<OrvixFlow.Infrastructure.Data.AppDbContext>();
    var doc = await db.KnowledgeBaseDocuments
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(d => d.Id == documentId);
    if (doc != null)
    {
        doc.Status = status;
        doc.ErrorMessage = error;
        await db.SaveChangesAsync();
    }
}
```

---

### Step 2 — Verify IngestionPipelineService Image Save

**File:** `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`, line ~189:

```csharp
var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, $"img_{imageChunk.Index}_{fileName}", imageMs);
```

This calls the **legacy overload** which delegates to `StorageContext` with `DepartmentId = null`.  
This means images are always stored under `__company__` prefix even if the document is department-scoped.

**Decision:** For Phase 07, this is acceptable. Images extracted from documents are knowledge base assets and are always accessed through the tenant+document authorization path, not via department-scoped direct access. Leave this as-is.

**Add a comment to make this explicit:**
```csharp
// Image saved with null DepartmentId (company-wide sentinel) by design.
// Image access is controlled via parent document authorization, not department-level directly.
var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, $"img_{imageChunk.Index}_{fileName}", imageMs);
```

If department-level image isolation is required later, it should be a separate phase with its own EF migration and UI changes.

---

### Step 3 — Verify DepartmentId Flows from Controller to Job

**File:** `OrvixFlow.Api/Controllers/FileIngestionController.cs`

Verify the `_backgroundJobClient.Enqueue` call (from Phase 05) passes `departmentId`:

```csharp
_backgroundJobClient.Enqueue<FileIngestionJob>(job => job.ProcessFileAsync(
    document.Id,
    document.StoragePath,
    document.FileName,
    document.ContentType,
    _scope.UserId == Guid.Empty ? null : _scope.UserId,
    departmentId,     // ← must be present after Phase 05
    tenantId));
```

If this was not done in Phase 05, add it now. The `FileIngestionJob.ProcessFileAsync` signature already has `Guid? departmentId = null` as parameter 6.

---

### Step 4 — Add Logging for Storage Provider in Job

Add to the top of `ProcessFileAsync` to aid debugging in production:

```csharp
_logger.LogInformation(
    "FileIngestionJob starting: doc={DocumentId} storageKey={StorageKey} tenant={TenantId}",
    documentId, storagePath, tenantId);
```

> When MinIO is used, the log will show the S3 key. When LocalFileStorage is used, it will show the local path. This distinction is diagnostic gold in production.

---

## Specific Concerns About Stream Handling

### `IngestionPipelineService` Already Resets Stream

In the current code:
```csharp
fileStream.Position = 0;   // Line 127 — reset before parsing
var parsedDoc = await parser.ParseAsync(fileStream, fileName);
```

This works when `fileStream` is a `MemoryStream` or a `FileStream`. However, `MinIOFileStorage.GetFileAsync` returns `response.ResponseStream`, which is a **non-seekable network stream**. Setting `Position = 0` on a non-seekable stream will throw `NotSupportedException`.

**Fix required in `IngestionPipelineService`:**

```csharp
// After receiving stream from storage — buffer it if not seekable
Stream parserStream = fileStream;
if (!fileStream.CanSeek)
{
    // Buffer the network stream to enable position reset
    var buffered = new MemoryStream();
    await fileStream.CopyToAsync(buffered);
    buffered.Position = 0;
    parserStream = buffered;
}

// Use parserStream for all subsequent operations
parserStream.Position = 0;
var parsedDoc = await parser.ParseAsync(parserStream, fileName);
```

> This is a critical correctness fix. Without it, files > first parse attempt will fail with `NotSupportedException` when `IngestionPipelineService` tries to reset the stream position.  
> The 20MB max file size makes the `MemoryStream` buffer acceptable.

---

## Tests to Write

**File:** `OrvixFlow.Tests/FileIngestionJobTests.cs` (new or extend existing)

```
1. When storage.GetFileAsync returns stream successfully, ingestion runs
2. When storage.GetFileAsync throws NoSuchKey (S3Exception), job marks document as Failed and does NOT throw
3. When storage.GetFileAsync throws transient error, job retries up to 3 times then throws
4. DepartmentId is passed from job to IngestionPipelineService
5. Non-seekable stream is buffered before being passed to parser
```

---

## Constraints

- Do not remove Hangfire `[AutomaticRetry(Attempts = 3)]` — it remains as the outer retry for embedding/pipeline failures
- The inner retry loop for storage fetch is for transient storage access errors only
- `NoSuchKey` from MinIO must NOT trigger Hangfire retry — it is permanent and must fail immediately
- `IngestionPipelineService` changes must not break existing tests (they use `MemoryStream` which is seekable — confirm no regression)

---

## Validation Checklist

- [ ] Upload a file, check Hangfire dashboard — job completes successfully without retries
- [ ] Simulate missing file (delete from MinIO manually, check job status → "Failed" in Hangfire + document.Status = Failed in DB)
- [ ] Confirm `IngestionPipelineService` buffering fix handles non-seekable stream
- [ ] `dotnet test` passes

---

## Completion Criteria

- [ ] `FileIngestionJob` has explicit retry for transient storage errors
- [ ] `NoSuchKey` error results in immediate failure (not retries)
- [ ] `IngestionPipelineService` buffers non-seekable streams
- [ ] `departmentId` flows from controller → job enqueue → pipeline
- [ ] Tests written and passing

---

## Handoff to Phase 08

Phase 08 implements the Azure Blob Storage provider for production. It re-uses the `IFileStorage` interface and `StorageContext` built in previous phases. No business code changes needed — only Infrastructure additions and config.
