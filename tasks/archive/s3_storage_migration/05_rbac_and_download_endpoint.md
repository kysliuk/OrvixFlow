# Phase 05 — RBAC, Download Endpoint, and Department-Scoped File Access

> **Obsolete / Historical Migration Plan**
> Superseded by later storage, RBAC, and download-path implementation by 2026-06-11.
> This document is retained for historical context only.

## Phase Goal

Update `FileIngestionController` to:
1. Accept optional `departmentId` on upload and validate department membership
2. Add `GET /api/v1/knowledge/documents/{id}/download` proxy download endpoint
3. Add department-scoped filtering to the document list endpoint
4. Add department membership check to the delete endpoint
5. Pass `StorageContext` (with `DepartmentId`) to the storage layer

---

## Phase Purpose

Without this phase, there is no download path and no department isolation. Files stored in MinIO cannot be served to the browser. Any user in the company could see and delete any other department's documents by guessing document IDs.

This phase closes gaps: G-S3, G-S4, G-P1, G-P2, G-P3.

---

## Scope

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Api/Controllers/FileIngestionController.cs` | All 4 changes described below |

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Tests/FileIngestionControllerTests.cs` | Department RBAC tests for upload/download/list/delete |

---

## Prerequisites

- Phase 03 complete (MinIO or Local wired, `StorageContext` exists)
- Phase 04 complete (`DepartmentId` on `KnowledgeBaseDocument`, EF migration applied)
- `dotnet test` passes

---

## Role Model Reminder (Critical)

RBAC for file access follows the existing two-layer role system:

| Role | File Access |
|------|------------|
| `SuperAdmin` | Any tenant, any department (platform admin — uses `IgnoreQueryFilters()`) |
| `InternalOperator` | Read-only across all tenants |
| `CompanyOwner` | All departments in own company |
| `CompanyAdmin` | All departments in own company |
| `DepartmentManager` | Only departments in `IScopeContext.AllowedDepartmentIds` |
| `Operator` | Only own department |
| `Viewer` | Own department, read-only |

These roles come from `UserCompanyMembership.CompanyRole`, exposed via `IScopeContext`.  
The `IScopeContext` interface is already injected in `FileIngestionController` as `_scope`.

**Use `IScopeContext`, not raw JWT claims.**  
`IScopeContext.HasCompanyWideAccess` → true for `CompanyOwner` and `CompanyAdmin`.  
`IScopeContext.AllowedDepartmentIds` → set of departments accessible to `DepartmentManager`/`Operator`.

---

## Implementation Instructions

### Step 1 — Add `CanAccessDepartment` Private Helper

Add this private method to `FileIngestionController`:

```csharp
/// <summary>
/// Returns true if the current user has access to files belonging to the given department.
/// - null department = company-wide file; only company-level roles can access
/// - non-null department = must be in user's allowed department set, OR user has company-wide access
/// </summary>
private bool CanAccessDepartment(Guid? departmentId)
{
    // Company-wide files (no department) — only admins/owners
    if (departmentId == null)
        return _scope.HasCompanyWideAccess;

    // Department-scoped: company-wide access OR user is in this specific department
    return _scope.HasCompanyWideAccess
        || _scope.AllowedDepartmentIds.Contains(departmentId.Value);
}
```

---

### Step 2 — Update Upload Action

**Before:** `UploadFile(IFormFile file)` — no department parameter

**After:** Accept optional `departmentId` and validate access:

```csharp
[HttpPost("upload")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] Guid? departmentId = null)
{
    if (file == null || file.Length == 0)
        return BadRequest("No file uploaded.");

    // 1. Validate File Size (unchanged)
    var maxFileSizeMb = _configuration.GetValue("AI:Ingestion:MaxFileSizeMb", 20);
    if (file.Length > maxFileSizeMb * 1024 * 1024)
        return BadRequest($"File size exceeds the limit of {maxFileSizeMb} MB.");

    // 2. Validate MIME type via magic bytes (unchanged)
    string detectedMimeType;
    using (var stream = file.OpenReadStream())
    {
        detectedMimeType = FileSignatureValidator.DetectMimeTypeFromStream(stream) ?? string.Empty;
        if (!FileSignatureValidator.IsAllowedMimeType(detectedMimeType))
            return BadRequest($"File content type is not allowed. Detected: '{detectedMimeType}'.");
    }

    var allowedTypes = _configuration.GetSection("AI:Ingestion:AllowedMimeTypes").Get<string[]>()
                       ?? new[] { "text/plain", "application/pdf", "image/png", "image/jpeg" };
    var clientContentType = file.ContentType.ToLowerInvariant();
    if (!allowedTypes.Contains(clientContentType))
        return BadRequest($"Content type '{clientContentType}' is not in the allowed list.");

    // 3. Virus Scan (unchanged)
    using (var stream = file.OpenReadStream())
    {
        if (!await _virusScanService.IsFileSafeAsync(stream, file.FileName))
            return BadRequest("File failed security scan.");
    }

    // 4. NEW: Department access check
    if (!CanAccessDepartment(departmentId))
        return Forbid();

    var tenantId = _tenantProvider.GetTenantId();

    // 5. Create document record — now with DepartmentId
    var document = new KnowledgeBaseDocument
    {
        TenantId = tenantId,
        DepartmentId = departmentId,            // NEW
        FileName = file.FileName,
        ContentType = detectedMimeType,
        FileSizeBytes = file.Length,
        Status = "Pending"
    };
    _dbContext.KnowledgeBaseDocuments.Add(document);
    await _dbContext.SaveChangesAsync();

    // 6. Save to object storage — NEW: use StorageContext overload
    using (var stream = file.OpenReadStream())
    {
        var ctx = new StorageContext(tenantId, departmentId, document.Id, file.FileName);
        var storagePath = await _storage.SaveFileAsync(ctx, stream);
        document.StoragePath = storagePath;
        await _dbContext.SaveChangesAsync();
    }

    // 7. Enqueue background ingestion job (unchanged — job reads storagePath from DB)
    _backgroundJobClient.Enqueue<FileIngestionJob>(job => job.ProcessFileAsync(
        document.Id,
        document.StoragePath,
        document.FileName,
        document.ContentType,
        _scope.UserId == Guid.Empty ? null : _scope.UserId,
        departmentId,                           // NEW: pass departmentId to ingestion job
        tenantId));

    return Ok(new
    {
        documentId = document.Id,
        departmentId = departmentId,            // NEW: echo back in response
        status = "Processing",
        message = "File uploaded successfully and queued for indexing."
    });
}
```

> Note: The `FileIngestionJob.ProcessFileAsync` signature already has a `Guid? departmentId` parameter (checked in Phase 01 analysis). Confirm the signature matches before passing this parameter.

---

### Step 3 — Add Download Endpoint

Add this new action to `FileIngestionController`:

```csharp
/// <summary>
/// Proxy downloads file from object storage through the API after authorization check.
/// Never exposes direct storage URLs to the client — all access is gated by JWT + RBAC.
/// </summary>
[HttpGet("documents/{id:guid}/download")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> DownloadDocument(Guid id)
{
    // EF global query filter enforces TenantId match — cross-tenant access returns null here
    var document = await _dbContext.KnowledgeBaseDocuments
        .FirstOrDefaultAsync(d => d.Id == id);

    if (document == null)
        return NotFound(); // Never return 403 — do not reveal document existence to unauthorized callers

    if (!CanAccessDepartment(document.DepartmentId))
        return Forbid();

    if (string.IsNullOrEmpty(document.StoragePath))
        return NotFound(new { message = "File not yet available in storage." });

    try
    {
        var stream = await _storage.GetFileAsync(document.StoragePath);
        return File(stream, document.ContentType, document.FileName);
    }
    catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
    {
        return NotFound(new { message = "File not found in storage. It may have been deleted." });
    }
}
```

> **Security invariant:** The response to a cross-tenant request is `NotFound` (same as cross-department unauthorized). Do NOT use `Forbid()` for the tenant check — that would reveal the document exists.

---

### Step 4 — Update List Endpoint

**Before:** Lists all documents for tenant with no department filtering.

**After:** Filters by department scope based on caller's role:

```csharp
[HttpGet("documents")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> GetDocuments(
    [FromQuery] Guid? departmentId = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    if (page < 1) page = 1;
    if (pageSize < 1) pageSize = 20;
    if (pageSize > 100) pageSize = 100;

    var query = _dbContext.KnowledgeBaseDocuments.AsQueryable();
    // Note: EF global query filter already applies TenantId == current tenant

    if (departmentId.HasValue)
    {
        // Explicit department filter — validate access to that specific department
        if (!CanAccessDepartment(departmentId))
            return Forbid();
        query = query.Where(d => d.DepartmentId == departmentId);
    }
    else if (!_scope.HasCompanyWideAccess)
    {
        // Non-admin user: limit to their allowed departments only
        // Company-wide files (null DepartmentId) are NOT returned to department-scoped users
        var allowed = _scope.AllowedDepartmentIds;
        query = query.Where(d =>
            d.DepartmentId != null && allowed.Contains(d.DepartmentId.Value));
    }
    // If HasCompanyWideAccess and no departmentId filter: return all docs for tenant (no filter)

    var total = await query.CountAsync();
    var items = await query
        .OrderByDescending(d => d.CreatedAtUtc)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(d => new
        {
            id = d.Id,
            fileName = d.FileName,
            contentType = d.ContentType,
            fileSizeBytes = d.FileSizeBytes,
            departmentId = d.DepartmentId,   // NEW: expose in response
            status = d.Status,
            createdAtUtc = d.CreatedAtUtc,
            indexedAtUtc = d.IndexedAtUtc,
            errorMessage = d.ErrorMessage
        })
        .ToListAsync();

    return Ok(new { total, page, pageSize, items });
}
```

---

### Step 5 — Update Delete Endpoint

Add department membership check before deletion:

```csharp
[HttpDelete("documents/{id:guid}")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> DeleteDocument(Guid id)
{
    // EF global filter already enforces TenantId
    var document = await _dbContext.KnowledgeBaseDocuments
        .FirstOrDefaultAsync(d => d.Id == id);

    if (document == null)
        return NotFound(new { message = "Document not found." });

    // NEW: department membership check before deletion
    if (!CanAccessDepartment(document.DepartmentId))
        return Forbid();

    if (!string.IsNullOrEmpty(document.StoragePath))
    {
        try
        {
            await _storage.DeleteFileAsync(document.StoragePath);
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices
                .GetService<ILogger<FileIngestionController>>();
            logger?.LogWarning(ex,
                "Failed to delete storage object {StoragePath} for document {DocumentId}. " +
                "Object may be orphaned.", document.StoragePath, document.Id);
        }
    }

    _dbContext.KnowledgeBases.RemoveRange(
        _dbContext.KnowledgeBases.Where(k => k.DocumentId == id));
    _dbContext.KnowledgeBaseImages.RemoveRange(
        _dbContext.KnowledgeBaseImages.Where(i => i.DocumentId == id));
    _dbContext.KnowledgeBaseDocuments.Remove(document);

    await _dbContext.SaveChangesAsync();

    return NoContent();
}
```

---

### Step 6 — Add Using Statements

Add to the top of `FileIngestionController.cs`:
```csharp
using OrvixFlow.Core.Models;   // for StorageContext
```

---

## Tests to Write

**File:** `OrvixFlow.Tests/FileIngestionControllerTests.cs`

Write tests for the following scenarios using in-memory EF + mocked `IFileStorage` + mocked `IScopeContext`:

```
1. DepartmentManager uploads to own department → 200
2. DepartmentManager uploads to another department → 403
3. DepartmentManager uploads with null departmentId (company-wide) → 403
4. CompanyAdmin uploads to any department → 200
5. CompanyAdmin uploads with null departmentId → 200

6. Download: document in own department → 200 + file stream
7. Download: document in another department → 403
8. Download: document from another tenant → 404 (EF global filter)
9. Download: document with empty StoragePath → 404

10. List: DepartmentManager sees only own-department docs
11. List: CompanyAdmin sees all docs including null-dept
12. List: DepartmentManager cannot filter by another dept (returns 403)

13. Delete: document in own dept → 204
14. Delete: document in another dept → 403
```

Refer to test pattern in `IngestionPipelineServiceTests.cs` for EF InMemory setup.  
Inject `IScopeContext` as a mock that returns desired `HasCompanyWideAccess` and `AllowedDepartmentIds`.

---

## Constraints

- **Never bypass EF global query filters** for the `TenantId` check — it is automatic via `AppDbContext`
- **Always return 404 (not 403) for cross-tenant access** — tenant isolation is invisible to callers
- **`_scope.AllowedDepartmentIds`** may be an empty set for users with no department membership — test that case
- `AmazonS3Exception` with `ErrorCode = "NoSuchKey"` is the MinIO 404 equivalent — catch specifically

---

## Validation Checklist

- [ ] Upload with `?departmentId={validDeptId}` → `KnowledgeBaseDocument.DepartmentId` is populated in DB
- [ ] Upload with no `departmentId` param → `DepartmentId` is `null` in DB (company-wide)
- [ ] object key in MinIO console includes department segment or `__company__`
- [ ] `GET /api/v1/knowledge/documents/{id}/download` returns file content
- [ ] Cross-department download → HTTP 403
- [ ] Cross-tenant download → HTTP 404
- [ ] List endpoint for `DepartmentManager` does not return other departments' files
- [ ] Delete from another department → HTTP 403
- [ ] `dotnet test --filter FileIngestionController` passes

---

## Completion Criteria

- [ ] `CanAccessDepartment` helper implemented
- [ ] Upload action accepts `departmentId?`, validates access, uses `StorageContext`
- [ ] Download endpoint implemented with 404/403 split
- [ ] List endpoint filters by department scope
- [ ] Delete endpoint checks department membership
- [ ] Tests written and passing

---

## Handoff to Phase 06

Phase 06 introduces the `StoredObject` DB metadata entity — a cross-cutting file registry that decouples storage keys from individual entities and enables file lifecycle governance. This is independent of the RBAC work done here.
