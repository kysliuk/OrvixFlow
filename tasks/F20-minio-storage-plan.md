# F-20: MinIO Local Storage with Department-Level Isolation

**Date:** 2026-04-09 | **Feature:** File Storage Migration | **Status:** Redesigned (v2)

---

## Overview

Implement S3-compatible local storage using MinIO with:
- Department-level isolation
- Role-based access control enforced in API layer
- CompanyAdmin full company-wide access
- Docker-based local deployment (free)
- Proxy-based downloads (no pre-signed URLs — RBAC must not be bypassable)

---

## Issues with Previous Plan (v1)

| # | Issue |
|---|-------|
| C1 | `FileAccessValidator` placed in `Infrastructure/Authorization/` — wrong layer; no HTTP/JWT access there |
| C2 | Download endpoint missing entirely — MinIO objects cannot be served directly to browser |
| C3 | Cross-company isolation not addressed — `StoragePath` alone does not protect against known-ID attacks |
| C4 | `DepartmentId` added without FK, query filter update, or DB migration guidance |
| C5 | `DepartmentManager` scope ignored — can manage multiple departments via `ScopeContext.AllowedDepartmentIds` |
| G1 | No `StorageContext` concept — raw tuple params across all methods is fragile |
| G2 | `ForcePathStyle = true` not mentioned — required for MinIO AWS SDK compatibility |
| G3 | Background job `DepartmentId` source not defined |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Docker Compose                           │
├──────────┬──────────┬──────────┬─────────────┬─────────────┤
│ orvix-api│ orvix-web│ orvix-db │    n8n      │    minio    │
│  (8080)  │  (3000)  │  (5432)  │   (5678)    │ :9000 (API) │
│          │          │          │             │ :9001 (UI)  │
└──────────┴──────────┴──────────┴─────────────┴─────────────┘
       │                                            │
       │    AWSSDK.S3 (ForcePathStyle=true)         │
       └──────────────────────────────┬─────────────┘
                                      │
                              ┌───────▼───────┐
                              │     MinIO     │
                              │   /data vol   │
                              └───────────────┘
```

### Storage Key Structure

```
orvixflow/                                  ← single shared bucket
└── tenants/
    └── {tenantId}/
        └── depts/
            ├── {departmentId}/             ← dept-scoped files
            │   └── docs/
            │       └── {documentId}/
            │           └── {guid}.{ext}
            └── __company__/               ← files with no department (company-wide)
                └── docs/
                    └── {documentId}/
                        └── {guid}.{ext}
```

**`__company__`** is a sentinel string (not a valid UUID) identifying company-wide uploads
(no department). Used when `DepartmentId == null`.

---

## Key Architectural Decisions

### 1. Single Bucket, Prefix-Based Isolation
One bucket (`orvixflow`). No per-tenant buckets.
- MinIO free tier has no multi-user IAM that maps to tenants
- Dynamic bucket creation requires complex lifecycle management
- Security enforced by backend RBAC, not MinIO IAM policies

### 2. Proxy Downloads, Not Pre-Signed URLs
All downloads go through `GET /api/v1/knowledge/documents/{id}/download` — API streams from MinIO.
- Pre-signed URLs bypass all RBAC
- Pre-signed URLs cannot be revoked after department membership changes
- Max file size is 10 MB — streaming overhead is negligible

### 3. `StorageContext` Value Object
Typed record replaces loose `(tenantId, departmentId, documentId, fileName)` tuples:
```csharp
// OrvixFlow.Core/Models/StorageContext.cs
public record StorageContext(
    Guid TenantId,
    Guid? DepartmentId,   // null = company-wide
    Guid DocumentId,
    string OriginalFileName);
```

### 4. Non-Breaking `IFileStorage` Extension
Add new overload; keep existing signature. `LocalFileStorage` implements both.
`MinIOFileStorage` uses `StorageContext` to build the S3 key.

### 5. RBAC Enforced in Controller Only
Authorization logic lives in `FileIngestionController`. Infrastructure has zero auth logic.
Follows the existing project pattern (`AccessResolver` → controller, not service).

---

## RBAC & Isolation Model

```
┌──────────────────────┬──────────────────────────────────────────────────┐
│ Role                 │ File Access Rule                                 │
├──────────────────────┼──────────────────────────────────────────────────┤
│ SuperAdmin           │ Any tenant, any department (IgnoreQueryFilters)  │
│ InternalOperator     │ Read-only across all tenants                     │
│ CompanyOwner         │ All departments in own company                   │
│ CompanyAdmin         │ All departments in own company                   │
│ DepartmentManager    │ AllowedDepartmentIds from ScopeContext only      │
│ Operator             │ Own department only                              │
│ Viewer               │ Own department, read-only                        │
└──────────────────────┴──────────────────────────────────────────────────┘
```

### Controller Helper

```csharp
private bool CanAccessDepartment(Guid? departmentId)
{
    // Company-wide files (null dept): only admins or above
    if (departmentId == null)
        return _scope.HasCompanyWideAccess;

    // Department-scoped files
    return _scope.HasCompanyWideAccess
        || _scope.AllowedDepartmentIds.Contains(departmentId.Value);
}
```

### Download Security Flow

```
1. GET /api/v1/knowledge/documents/{id}/download
2. Load KnowledgeBaseDocument by Id
   └─ EF global query filter enforces TenantId match
3. document == null → 404  (never 403 — do not leak existence)
4. CanAccessDepartment(document.DepartmentId) == false → 403
5. _storage.GetFileAsync(document.StoragePath) → stream to Response
```

---

## Domain Model

### Modified: `KnowledgeBaseDocument`

```csharp
// OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs
public class KnowledgeBaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }       // NEW — null = company-wide

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string SourceType { get; set; } = "Text";
    public string StoragePath { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAtUtc { get; set; }

    public Department? Department { get; set; }   // NEW — navigation property
    public ICollection<KnowledgeBase> Chunks { get; set; } = new List<KnowledgeBase>();
}
```

### New: `StorageContext`

```csharp
// OrvixFlow.Core/Models/StorageContext.cs
namespace OrvixFlow.Core.Models;

public record StorageContext(
    Guid TenantId,
    Guid? DepartmentId,
    Guid DocumentId,
    string OriginalFileName);
```

### `AppDbContext` Changes

```csharp
// In OnModelCreating — add FK and index:
modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasOne(d => d.Department)
    .WithMany()
    .HasForeignKey(d => d.DepartmentId)
    .OnDelete(DeleteBehavior.SetNull);

modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasIndex(d => new { d.TenantId, d.DepartmentId });
```

### Database Migration: `AddDepartmentIdToDocument`

- Add column: `DepartmentId UUID NULL`
- FK: `REFERENCES Departments(Id) ON DELETE SET NULL`
- Composite index: `(TenantId, DepartmentId)`

---

## Application Layer Changes

### Updated `IFileStorage`

```csharp
// OrvixFlow.Core/Interfaces/IFileStorage.cs
public interface IFileStorage
{
    // Existing — unchanged (LocalFileStorage still uses these)
    Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream);
    Task<Stream> GetFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);

    // New — context-aware upload (MinIOFileStorage uses this for key building)
    Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream);
}
```

### Updated Upload Action

```csharp
// FileIngestionController.UploadFile — key changes:
[HttpPost("upload")]
public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] Guid? departmentId)
{
    // ... existing: size, MIME, virus scan (unchanged) ...

    // NEW: validate department access
    if (!CanAccessDepartment(departmentId))
        return Forbid();

    var document = new KnowledgeBaseDocument
    {
        TenantId = tenantId,
        DepartmentId = departmentId,       // NEW
        FileName = file.FileName,
        ContentType = detectedMimeType,
        FileSizeBytes = file.Length,
        Status = "Pending"
    };
    _dbContext.KnowledgeBaseDocuments.Add(document);
    await _dbContext.SaveChangesAsync();

    using var stream = file.OpenReadStream();
    var ctx = new StorageContext(tenantId, departmentId, document.Id, file.FileName); // NEW
    var storagePath = await _storage.SaveFileAsync(ctx, stream);
    document.StoragePath = storagePath;
    await _dbContext.SaveChangesAsync();

    // ... enqueue FileIngestionJob (unchanged) ...
}
```

### New Download Action

```csharp
[HttpGet("documents/{id:guid}/download")]
[RequireModule("knowledge-base")]
public async Task<IActionResult> DownloadDocument(Guid id)
{
    var document = await _dbContext.KnowledgeBaseDocuments
        .FirstOrDefaultAsync(d => d.Id == id); // EF global filter handles TenantId

    if (document == null) return NotFound();
    if (!CanAccessDepartment(document.DepartmentId)) return Forbid();

    var stream = await _storage.GetFileAsync(document.StoragePath);
    return File(stream, document.ContentType, document.FileName);
}
```

### Updated List Action

```csharp
[HttpGet("documents")]
public async Task<IActionResult> GetDocuments(
    [FromQuery] Guid? departmentId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    var query = _dbContext.KnowledgeBaseDocuments.AsQueryable();

    if (departmentId.HasValue)
    {
        if (!CanAccessDepartment(departmentId)) return Forbid();
        query = query.Where(d => d.DepartmentId == departmentId);
    }
    else if (!_scope.HasCompanyWideAccess)
    {
        // Non-admin users: their departments + null-dept files they cannot see anyway
        var allowed = _scope.AllowedDepartmentIds;
        query = query.Where(d =>
            d.DepartmentId != null && allowed.Contains(d.DepartmentId.Value));
    }

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
            departmentId = d.DepartmentId,      // NEW
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

## Infrastructure

### `MinIOFileStorage`

```csharp
// OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Storage;

public class MinIOFileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOFileStorage> _logger;

    public MinIOFileStorage(IAmazonS3 s3, string bucket, ILogger<MinIOFileStorage> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    // Build deterministic, traversal-safe S3 key from typed context
    private static string BuildKey(StorageContext ctx)
    {
        var deptSegment = ctx.DepartmentId.HasValue
            ? ctx.DepartmentId.Value.ToString()
            : "__company__";
        var extension = Path.GetExtension(ctx.OriginalFileName);
        return $"tenants/{ctx.TenantId}/depts/{deptSegment}/docs/{ctx.DocumentId}/{Guid.NewGuid()}{extension}";
    }

    public async Task<string> SaveFileAsync(StorageContext ctx, Stream fileStream)
    {
        var key = BuildKey(ctx);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = fileStream,
            ContentType = "application/octet-stream",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
        });
        _logger.LogInformation("File saved to MinIO: {Key}", key);
        return key; // StoragePath stored in DB = S3 key
    }

    public async Task<Stream> GetFileAsync(string storagePath)
    {
        var response = await _s3.GetObjectAsync(_bucket, storagePath);
        return response.ResponseStream;
    }

    public async Task DeleteFileAsync(string storagePath)
    {
        await _s3.DeleteObjectAsync(_bucket, storagePath);
        _logger.LogInformation("File deleted from MinIO: {Key}", storagePath);
    }

    // Legacy overload — backwards compat, delegates to new method
    public Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream)
        => SaveFileAsync(new StorageContext(tenantId, null, documentId, fileName), fileStream);
}
```

### `MinIOBucketInitializer`

```csharp
// OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Infrastructure.Storage;

public class MinIOBucketInitializer : IHostedService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<MinIOBucketInitializer> _logger;

    public MinIOBucketInitializer(IAmazonS3 s3, string bucket, ILogger<MinIOBucketInitializer> logger)
    {
        _s3 = s3;
        _bucket = bucket;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _s3.DoesS3BucketExistAsync(_bucket);
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket });
                _logger.LogInformation("MinIO bucket '{Bucket}' created.", _bucket);
            }
            else
            {
                _logger.LogInformation("MinIO bucket '{Bucket}' already exists.", _bucket);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MinIO bucket '{Bucket}'. Startup aborted.", _bucket);
            throw; // Fail fast — storage is critical infrastructure
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### DI Registration (`DependencyInjection.cs`)

```csharp
// Replace the existing LocalFileStorage registration block:
var storageProvider = configuration["Storage:Provider"] ?? "Local";

if (storageProvider == "MinIO")
{
    var minioSection = configuration.GetSection("Storage:MinIO");
    var bucket = minioSection["Bucket"] ?? "orvixflow";

    services.AddSingleton<IAmazonS3>(_ =>
    {
        var config = new AmazonS3Config
        {
            ServiceURL = minioSection["Endpoint"],
            ForcePathStyle = true,   // CRITICAL: required for MinIO
            UseHttp = !minioSection.GetValue<bool>("UseSSL")
        };
        return new AmazonS3Client(
            minioSection["AccessKey"],
            minioSection["SecretKey"],
            config);
    });

    services.AddSingleton(sp =>
        new MinIOBucketInitializer(
            sp.GetRequiredService<IAmazonS3>(),
            bucket,
            sp.GetRequiredService<ILogger<MinIOBucketInitializer>>()));
    services.AddHostedService(sp => sp.GetRequiredService<MinIOBucketInitializer>());

    services.AddScoped<IFileStorage>(sp =>
        new MinIOFileStorage(
            sp.GetRequiredService<IAmazonS3>(),
            bucket,
            sp.GetRequiredService<ILogger<MinIOFileStorage>>()));
}
else
{
    services.AddScoped<IFileStorage, LocalFileStorage>();
}
```

> **IMPORTANT:** `ForcePathStyle = true` is **mandatory** for MinIO. Without it the AWS SDK
> generates virtual-hosted-style URLs (`orvixflow.minio:9000`) which fail against a local instance.

---

## Configuration

### `appsettings.json`

```json
"Storage": {
  "Provider": "MinIO",
  "MinIO": {
    "Endpoint": "http://minio:9000",
    "Bucket": "orvixflow",
    "UseSSL": false
  },
  "Local": {
    "BasePath": "/app/uploads"
  }
}
```

> AccessKey and SecretKey are **never** in appsettings. They come from env vars / `.env`.

### `.env` / `.env.example` additions

```bash
# Storage
STORAGE_PROVIDER=MinIO
MINIO_ENDPOINT=http://minio:9000
MINIO_ACCESS_KEY=orvixflow_admin
MINIO_SECRET_KEY=orvixflow_secret_change_me
MINIO_BUCKET=orvixflow
MINIO_USE_SSL=false
```

---

## Docker Setup

### `docker-compose.yml` — add MinIO service

```yaml
  minio:
    image: minio/minio:latest
    container_name: orvix_minio
    command: server /data --console-address ":9001"
    ports:
      - "9000:9000"    # S3 API
      - "9001:9001"    # Web Console (dev only)
    environment:
      MINIO_ROOT_USER: ${MINIO_ACCESS_KEY}
      MINIO_ROOT_PASSWORD: ${MINIO_SECRET_KEY}
    volumes:
      - minio_data:/data
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3
```

Add to `orvix-api.depends_on`:
```yaml
    depends_on:
      - orvix-db
      - n8n
      - minio    # NEW
```

Add to `volumes:` block:
```yaml
volumes:
  pgdata:
  n8n_data:
  uploads_data:
  minio_data:   # NEW
```

---

## API Design

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `POST` | `/api/v1/knowledge/upload?departmentId={guid}` | Authorize + RequireModule | Upload file, optional dept scope |
| `GET` | `/api/v1/knowledge/documents?departmentId={guid}` | Authorize + RequireModule | List (scope-filtered) |
| `GET` | `/api/v1/knowledge/documents/{id}/download` | Authorize + RequireModule | Proxy download |
| `DELETE` | `/api/v1/knowledge/documents/{id}` | Authorize + RequireModule | Delete (scope-validated) |

`departmentId` query param is optional on upload. When omitted, the file is stored as company-wide
and only users with `HasCompanyWideAccess` can see or download it.

---

## Security Considerations

| Risk | Mitigation |
|------|-----------|
| Cross-tenant file access | EF global query filter on `KnowledgeBaseDocument.TenantId` — row is invisible to other tenants |
| Cross-department access | `CanAccessDepartment()` validated before every upload, download, and delete |
| Direct S3 key guessing | MinIO bucket is not publicly accessible; all access proxied through API; keys are GUIDs only |
| `StoragePath` manipulation | `StoragePath` is server-generated (from `BuildKey`), never read from user input |
| Path traversal in S3 key | Key built entirely from server-generated GUIDs — no user-controlled path segments |
| MinIO credentials leak | Secrets only in `.env` (gitignored); never in `appsettings.json` |
| `__company__` sentinel collision | Not a valid UUID — cannot be confused with real Department ID |
| File orphaning | `DeleteAsync` failure logged as Warning (F-14 pattern); orphan cleanup is Phase 4 |
| Background job access | `FileIngestionJob` reads `DepartmentId` from DB record — not from HTTP context |
| Token not refreshed after dept removal | `ScopeContext` re-queries DB on every request — membership change is effective within one request |

---

## Implementation Phases

### Phase 1 — Foundation (Infrastructure + Docker)

| # | Task | File |
|---|------|------|
| 1.1 | Add MinIO service, volume, depends_on | `docker-compose.yml` |
| 1.2 | Add MinIO env vars | `.env`, `.env.example` |
| 1.3 | Add Storage config section | `appsettings.json` |
| 1.4 | Create `StorageContext` record | `OrvixFlow.Core/Models/StorageContext.cs` [NEW] |
| 1.5 | Extend `IFileStorage` with new overload | `OrvixFlow.Core/Interfaces/IFileStorage.cs` |
| 1.6 | Implement new overload in `LocalFileStorage` | `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` |
| 1.7 | Create `MinIOFileStorage` | `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs` [NEW] |
| 1.8 | Create `MinIOBucketInitializer` | `OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs` [NEW] |
| 1.9 | Conditional DI registration | `OrvixFlow.Infrastructure/DependencyInjection.cs` |

**Verification:** `docker compose up -d minio` → MinIO console at `:9001`. API starts without error. Upload a file → S3 key visible in MinIO console with correct key prefix.

---

### Phase 2 — Data Model + RBAC

| # | Task | File |
|---|------|------|
| 2.1 | Add `DepartmentId?` + navigation property | `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` |
| 2.2 | Add FK + composite index in `OnModelCreating` | `OrvixFlow.Infrastructure/Data/AppDbContext.cs` |
| 2.3 | Create EF migration | `OrvixFlow.Infrastructure/Migrations/AddDepartmentIdToDocument.cs` [NEW] |
| 2.4 | Accept `departmentId?` on upload, validate access, pass `StorageContext` | `OrvixFlow.Api/Controllers/FileIngestionController.cs` |
| 2.5 | Add `DownloadDocument` action | `OrvixFlow.Api/Controllers/FileIngestionController.cs` |
| 2.6 | Add scope-based filtering to `GetDocuments` | `OrvixFlow.Api/Controllers/FileIngestionController.cs` |
| 2.7 | Add `CanAccessDepartment` private helper | `OrvixFlow.Api/Controllers/FileIngestionController.cs` |

**Verification:**
- DepartmentManager uploads to their dept → 200
- Same user downloads another dept's file → 403
- CompanyAdmin lists all documents → no filtering applied
- CompanyAdmin downloads any file → 200

---

### Phase 3 — Frontend

| # | Task | File |
|---|------|------|
| 3.1 | Department picker on upload form | `orvixflow-web/app/(dashboard)/knowledge/page.tsx` |
| 3.2 | Department column in file list table | `orvixflow-web/app/(dashboard)/knowledge/page.tsx` |
| 3.3 | Download button → `GET /documents/{id}/download` | `orvixflow-web/app/(dashboard)/knowledge/page.tsx` |

---

### Phase 4 — Production Readiness

| # | Task | File |
|---|------|------|
| 4.1 | `StorageHealthCheck` at `/health/storage` | `OrvixFlow.Api/Health/StorageHealthCheck.cs` [NEW] |
| 4.2 | Storage usage metric via `IUsageService` on upload | `FileIngestionController.cs` |
| 4.3 | One-time migration script: re-upload existing local files to MinIO | `scripts/migrate-uploads-to-minio.sh` [NEW] |
| 4.4 | Update `memory-architecture.md`, `memory-file-map.md` | `memory/` |

---

## Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Core/Models/StorageContext.cs` | Typed value object for storage operations |
| `OrvixFlow.Infrastructure/Storage/MinIOFileStorage.cs` | MinIO IFileStorage implementation |
| `OrvixFlow.Infrastructure/Storage/MinIOBucketInitializer.cs` | IHostedService — bucket auto-creation |
| `OrvixFlow.Infrastructure/Migrations/AddDepartmentIdToDocument.cs` | EF migration |
| `OrvixFlow.Api/Health/StorageHealthCheck.cs` | MinIO health check (Phase 4) |

## Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` | Add `DepartmentId?`, `Department?` nav prop |
| `OrvixFlow.Core/Interfaces/IFileStorage.cs` | Add `SaveFileAsync(StorageContext, Stream)` overload |
| `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs` | Implement new overload (delegates to existing) |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | Add FK + index for DepartmentId |
| `OrvixFlow.Infrastructure/DependencyInjection.cs` | Conditional MinIO/Local DI registration |
| `OrvixFlow.Api/Controllers/FileIngestionController.cs` | Upload + Download + List + CanAccessDepartment |
| `OrvixFlow.Api/appsettings.json` | Add `Storage:` section |
| `docker-compose.yml` | Add `minio` service, volume, api dependency |
| `.env` / `.env.example` | Add MinIO env vars |

---

## Dependencies

- `AWSSDK.S3` NuGet package (S3-compatible; works with MinIO via `ForcePathStyle`)
- `minio/minio` Docker image
- Existing `UserRole` enum, `IScopeContext`, `ITenantProvider`, `RequireModuleAttribute`

---

## Testing Strategy

### Unit Tests

| Test | Scenario |
|------|---------|
| `MinIOFileStorageTests` | Correct S3 key structure for dept / `__company__` sentinel |
| `MinIOFileStorageTests` | Key is GUID-only — no user-controlled segments |
| `FileIngestionControllerTests` | DepartmentManager blocked from other dept upload → 403 |
| `FileIngestionControllerTests` | CompanyAdmin allowed to upload/download all depts → 200 |

**Pattern:** Mock `IAmazonS3` via interface. In-Memory EF for DB. Inject `IScopeContext` mock.

### Integration Tests

| Scenario | Expected |
|----------|---------|
| Upload to own dept → download → verify content | 200 with correct body |
| Upload to dept A, download as dept B user | 403 |
| CompanyAdmin downloads any dept file | 200 |
| Null-dept file (company-wide) listed by DepartmentManager | Not returned |
| Cross-company: valid JWT from Company A, doc ID from Company B | 404 (isolation) |
| Dept membership removed, token not refreshed | 403 (ScopeContext re-queries DB per request) |

---

## Effort Estimate

| Phase | Hours |
|-------|-------|
| Phase 1: Foundation | 2 |
| Phase 2: Data Model + RBAC | 3 |
| Phase 3: Frontend | 2 |
| Phase 4: Production Readiness | 2 |
| **Total** | **9 hours** |

---

## Rollback Plan

- Keep `LocalFileStorage` as active fallback
- Toggle via `STORAGE_PROVIDER=Local` env var
- No breaking changes to existing API contracts

---

## Future Improvements

| Item | Notes |
|------|-------|
| AWS S3 migration | Only requires config change — remove `ForcePathStyle`, change endpoint. No code changes. |
| Pre-signed URL downloads | Add `GetDownloadUrlAsync` to `IFileStorage` for large-file CDN offload |
| Per-download audit trail | Log each `DownloadDocument` call to `AuditTrail` |
| Orphan cleanup job | Hangfire job to delete MinIO objects with no matching DB record |
| TLS for production | Enable SSL on MinIO; rotate access keys per environment |
