# Phase 06 — StoredObject Metadata Entity (File Registry)

> **Obsolete / Historical Migration Plan**
> Superseded by later storage metadata implementation by 2026-06-11.
> Use current code and `memory/memory-feature-map.md` before relying on this phase plan.

## Phase Goal

Introduce a `StoredObject` entity as a platform-wide file metadata registry.  
Every file stored in object storage (MinIO or Azure Blob) gets a corresponding `StoredObject` row that carries:
- Storage provider name and key
- SHA-256 hash for integrity and dedup
- Virus scan status
- Lifecycle status (active/soft-deleted/archived)
- Full tenant + department association
- Who created it and when

---

## Phase Purpose

Without DB metadata, object storage is an opaque blob dump. You lose:
- Authorization verification at key level (not just entity level)
- File integrity guarantee
- Dedup via hash
- Audit trail per file lifecycle event
- Migration observability (which provider holds the file)
- Orphan detection

This entity is the foundation for Phase 09 (migration tool) and Phase 10 (observability).

---

## Scope

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Core/Entities/StoredObject.cs` | New domain entity for file metadata |
| `OrvixFlow.Infrastructure/Migrations/AddStoredObjectTable.cs` | EF migration (auto-generated then reviewed) |

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | Add `DbSet<StoredObject>`, query filter, relationships |
| `OrvixFlow.Api/Controllers/FileIngestionController.cs` | Create `StoredObject` row on every upload |
| `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs` | Create `StoredObject` row when saving images |

---

## Prerequisites

- Phase 04 complete (EF migration infrastructure in place)
- Phase 05 complete (upload flow finalized)
- `dotnet test` passes

---

## Implementation Instructions

### Step 1 — Create `StoredObject` Entity

**File:** `OrvixFlow.Core/Entities/StoredObject.cs` (new file)

```csharp
using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Platform-wide metadata registry for every file stored in object storage.
/// One row per physical object (not per document). Images and documents each have their own row.
/// </summary>
public class StoredObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Tenant context
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }     // null = company-wide

    // Module and entity context
    public string Module { get; set; } = string.Empty;         // e.g. "knowledge-base"
    public string EntityType { get; set; } = string.Empty;     // e.g. "document", "image"
    public Guid EntityId { get; set; }                         // KnowledgeBaseDocument.Id or KnowledgeBaseImage.Id

    // Object storage metadata
    public string StorageProvider { get; set; } = string.Empty;    // "Local" | "MinIO" | "AzureBlob"
    public string ContainerOrBucket { get; set; } = string.Empty;  // bucket/container name
    public string StorageKey { get; set; } = string.Empty;         // object key / path

    // File identity
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;             // hex SHA-256 of raw bytes

    // Virus scan
    public string VirusScanStatus { get; set; } = "Pending";       // Pending | Clean | Infected | Failed

    // Lifecycle
    public string LifecycleStatus { get; set; } = "Active";        // Active | SoftDeleted | Archived
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAtUtc { get; set; }

    // Audit
    public Guid CreatedByUserId { get; set; }
}
```

---

### Step 2 — Register in `AppDbContext`

**File:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs`

Add `DbSet`:
```csharp
public DbSet<StoredObject> StoredObjects => Set<StoredObject>();
```

Add query filter in `OnModelCreating` (alongside other entity filters):
```csharp
modelBuilder.Entity<StoredObject>().HasQueryFilter(s => s.TenantId == _tenantProvider.GetTenantId());
```

Add index for common query patterns:
```csharp
modelBuilder.Entity<StoredObject>()
    .HasIndex(s => new { s.TenantId, s.EntityType, s.EntityId });

modelBuilder.Entity<StoredObject>()
    .HasIndex(s => s.StorageKey);   // for orphan detection queries
```

---

### Step 3 — Generate EF Migration

```bash
dotnet ef migrations add AddStoredObjectTable \
  --project OrvixFlow.Infrastructure \
  --startup-project OrvixFlow.Api
```

Review the generated migration:
- `CreateTable` for `StoredObjects`
- Verify all columns are nullable/not-nullable as defined
- Verify indexes are created

Apply:
```bash
dotnet ef database update \
  --project OrvixFlow.Infrastructure \
  --startup-project OrvixFlow.Api
```

---

### Step 4 — Create `StoredObject` Row on Document Upload

**File:** `OrvixFlow.Api/Controllers/FileIngestionController.cs`

After saving a file to storage and before enqueuing the background job, create a `StoredObject` record:

```csharp
// After: var storagePath = await _storage.SaveFileAsync(ctx, stream);
// After: document.StoragePath = storagePath;
// Add:

var sha256 = ComputeSha256(/* file bytes */);
var storageProvider = _configuration["Storage:Provider"] ?? "Local";

var storedObject = new StoredObject
{
    TenantId = tenantId,
    DepartmentId = departmentId,
    Module = "knowledge-base",
    EntityType = "document",
    EntityId = document.Id,
    StorageProvider = storageProvider,
    ContainerOrBucket = _configuration["Storage:MinIO:Bucket"] ?? "local",
    StorageKey = storagePath,
    OriginalFileName = file.FileName,
    ContentType = detectedMimeType,
    SizeBytes = file.Length,
    Sha256 = sha256,
    VirusScanStatus = "Clean",     // already passed scan above
    LifecycleStatus = "Active",
    CreatedByUserId = _scope.UserId
};
_dbContext.StoredObjects.Add(storedObject);
await _dbContext.SaveChangesAsync();
```

Add a private helper for SHA-256 computation:
```csharp
private static string ComputeSha256(byte[] data)
{
    using var sha = System.Security.Cryptography.SHA256.Create();
    var hash = sha.ComputeHash(data);
    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

> **Important:** The file stream has already been read multiple times (MIME check, virus scan, storage save). You must buffer the upload to compute SHA-256 without re-reading an already-consumed stream. Options:
> 1. Read entire file to `byte[]` once at the start, then use `MemoryStream` for subsequent operations
> 2. Or compute SHA-256 during the storage write (add a SHA-256 computing stream wrapper)
>
> For files up to 20MB (current limit), option 1 is acceptable. Read `file.OpenReadStream()` into `byte[]` once, then all subsequent stream reads use `new MemoryStream(bytes)`.

---

### Step 5 — Create `StoredObject` Row for Images in `IngestionPipelineService`

**File:** `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`

After `_storage.SaveFileAsync(tenantId, document.Id, ...)` call (around line 189), add:

```csharp
// After: var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, ...);

var storedObjectForImage = new StoredObject
{
    TenantId = tenantId,
    DepartmentId = departmentId,            // passed from FileIngestionJob
    Module = "knowledge-base",
    EntityType = "image",
    EntityId = document.Id,                 // parent document
    StorageProvider = "Local",              // will be updated by provider config
    ContainerOrBucket = string.Empty,
    StorageKey = imagePath,
    OriginalFileName = $"img_{imageChunk.Index}_{fileName}",
    ContentType = imageChunk.ContentType,
    SizeBytes = imageChunk.Data.Length,
    Sha256 = ComputeImageSha256(imageChunk.Data),
    VirusScanStatus = "Clean",             // images come from already-scanned documents
    LifecycleStatus = "Active",
    CreatedByUserId = userId ?? Guid.Empty
};
_dbContext.StoredObjects.Add(storedObjectForImage);
```

Note: `IngestionPipelineService` needs to know the current `Storage:Provider` to set `StorageProvider`. Inject `IConfiguration` (already injected in constructor) and read `configuration["Storage:Provider"]`.

---

## Constraints

### Query Filter on StoredObject

`StoredObject` has a global query filter by `TenantId`. This means:
- Regular service code automatically scopes to current tenant
- Admin-facing queries (e.g., orphan detection) must use `IgnoreQueryFilters()` per the project pattern
- When adding admin endpoints to inspect `StoredObjects`, always use `IgnoreQueryFilters()` and document it

### No StoredObject Access in Business Logic

`StoredObject` is purely a metadata/audit side-effect of storage operations. Business logic (RAG search, ingestion, inbox processing) must not depend on `StoredObject` rows. They use `KnowledgeBaseDocument.StoragePath` and `KnowledgeBaseImage.StoragePath` as they always have.

### SHA-256 Buffer Strategy

Files are limited to 20MB. Reading into `byte[]` is acceptable. For future larger files, implement a `CryptoStream` wrapper. Document this decision with a `// TODO: stream-based SHA256 for files > 20MB` comment.

---

## Tests to Write

**File:** `OrvixFlow.Tests/StoredObjectTests.cs` (new file)

```
1. StoredObject has correct TenantId, EntityId, StorageKey after upload
2. StoredObject is NOT created when virus scan fails (current behavior: file rejected, no DB record)
3. StoredObject.Sha256 is non-empty and is valid hex
4. StoredObject.LifecycleStatus = "Active" on creation
5. StoredObject has correct VirusScanStatus = "Clean" after successful upload
6. StoredObject.EntityType distinguishes "document" from "image"
```

---

## Validation Checklist

- [ ] `StoredObjects` table created in PostgreSQL with all columns
- [ ] Every test upload creates exactly one `StoredObject` row with `EntityType = "document"`
- [ ] Image processing creates `StoredObject` rows with `EntityType = "image"`
- [ ] `Sha256` column is populated
- [ ] `VirusScanStatus = "Clean"` on successful uploads
- [ ] `dotnet test` passes

---

## Completion Criteria

- [ ] `StoredObject` entity created in `OrvixFlow.Core/Entities/`
- [ ] `AppDbContext` registers the entity with query filter and indexes
- [ ] EF migration applied
- [ ] Upload flow creates `StoredObject` row
- [ ] Image ingestion creates `StoredObject` row
- [ ] Tests written and passing

---

## Handoff to Phase 07

Phase 07 adapts `FileIngestionJob` to work correctly with object storage keys (MinIO/Azure) instead of assuming local filesystem paths. It also adds retry around object-fetch operations in the job.
