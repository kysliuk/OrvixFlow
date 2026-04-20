# Phase 01 — Current State Analysis and Gap Registry

## Phase Goal

This is a **read-only analysis phase**. No code changes occur here.  
Document every gap between the current codebase and the migration target so future phases operate on verified facts rather than assumptions.

---

## Phase Purpose

Before writing any code, the execution agent must have a precise map of:
- What already exists and works
- What exists but has bugs
- What is missing entirely
- What the confirmed dependency graph looks like

This prevents phases from conflicting with each other or making wrong assumptions.

---

## Confirmed Current State (Pre-Migration)

### Storage Interface

**File:** `OrvixFlow.Core/Interfaces/IFileStorage.cs`

```csharp
public interface IFileStorage
{
    Task<string> SaveFileAsync(Guid tenantId, Guid documentId, string fileName, Stream fileStream);
    Task<Stream> GetFileAsync(string storagePath);
    Task DeleteFileAsync(string storagePath);
}
```

**Gap:** Interface has no concept of `DepartmentId` or `StorageContext`. The `F20-minio-storage-plan.md` design adds a new overload `SaveFileAsync(StorageContext ctx, Stream fileStream)` which does not yet exist.

---

### LocalFileStorage

**File:** `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`

**What it does:**
- Creates directory at `_basePath/{tenantId}/{documentId}/`
- Generates random GUID filename locally (filename sanitization exists from F-12 fix)
- Path traversal checks are in place
- Returns `resolvedPath` (absolute filesystem path) as the `storagePath`
- `GetFileAsync` reads file stream from disk
- `DeleteFileAsync` deletes from disk

**Gaps:**
- No department awareness
- Returns absolute filesystem path as `StoragePath` — incompatible with object storage key
- `StoragePath` from DB references local disk paths; these cannot be used by MinIO/Azure
- No SHA-256 hash computation at write time
- Registered via DI but no config switch exists yet (`Storage:Provider` config key not yet wired)

---

### FileIngestionController

**File:** `OrvixFlow.Api/Controllers/FileIngestionController.cs`

**What it does:**
- `POST /api/v1/knowledge/upload` — validates size, MIME, virus scan, saves to storage, creates DB record, enqueues `FileIngestionJob`
- `GET /api/v1/knowledge/documents` — lists documents for tenant (paginated)
- `DELETE /api/v1/knowledge/documents/{id}` — deletes from storage + DB records

**Gaps:**
- **No download endpoint** — `GET /api/v1/knowledge/documents/{id}/download` does not exist
- No `departmentId` parameter on upload
- No `CanAccessDepartment` authorization helper
- List endpoint has no department filtering
- Delete endpoint does not check department membership before deleting

---

### IngestionPipelineService

**File:** `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`

**What it does:**  
- Calls `_storage.SaveFileAsync(tenantId, document.Id, imageFileName, imageMs)` for extracted images only
- The main document file is stored prior to this service being called (by the controller)

**Gap:**  
- Images go through `LocalFileStorage` → local path stored in `KnowledgeBaseImage.StoragePath`  
- When MinIO is activated, image paths must also be object-storage keys, not filesystem paths  
- `KnowledgeBaseImage.StoragePath` will need same migration as `KnowledgeBaseDocument.StoragePath`

---

### FileIngestionJob

**File:** `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs`

**What it does:**
```csharp
using (var stream = await storage.GetFileAsync(storagePath))
{
    var result = await pipeline.IngestFileAsync(stream, fileName, contentType, documentId, tenantId, userId, departmentId);
}
```

**Gap:**  
- Receives `storagePath` as a string parameter from the controller at enqueue time
- Currently this is a local filesystem path  
- When MinIO is activated, `storagePath` will be an S3 key — the call signature works but the stored value in DB must be the key, not a local path
- No retry on storage failures (only on AI/embedding failures)

---

### KnowledgeBaseDocument Entity

**File:** `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs`

**Current fields:**
```csharp
public Guid Id { get; set; }
public Guid TenantId { get; set; }
public string FileName { get; set; }        // display name
public string ContentType { get; set; }
public long FileSizeBytes { get; set; }
public string SourceType { get; set; }
public string StoragePath { get; set; }     // absolute local path currently
public string Status { get; set; }
public string? ErrorMessage { get; set; }
public DateTime CreatedAtUtc { get; set; }
public DateTime? IndexedAtUtc { get; set; }
public ICollection<KnowledgeBase> Chunks { get; set; }
```

**Gaps:**
- No `DepartmentId` — needed for RBAC
- No `Sha256` hash — needed for migration verification and dedup
- No `StorageProvider` field — needed to identify which provider holds the file during/after migration
- No `LifecycleStatus` field — needed for soft delete / archive

The `F20-minio-storage-plan.md` design adds `DepartmentId?`. The broader migration plan also suggests `StorageProvider`, `Sha256`, and `LifecycleStatus`.

---

### KnowledgeBaseImage Entity

**File:** `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs`

**Current fields (storage-relevant):**
```csharp
public string StoragePath { get; set; }   // absolute local path currently
public string ContentType { get; set; }
```

**Gap:** Same lifecycle problem as `KnowledgeBaseDocument.StoragePath` — currently a local path, must become an object-storage key.

---

### Virus Scanning

**Configuration (appsettings.json):**
```json
"Security": {
  "VirusScan": {
    "Provider": "Noop",
    "ClamAv": { "Host": "localhost", "Port": 3310 }
  }
}
```

**DI Registration (DependencyInjection.cs lines 146–174):**
```csharp
// Lines 146-156: correct conditional ClamAV registration
var virusScanProvider = configuration["Security:VirusScan:Provider"] ?? "Noop";
if (virusScanProvider == "ClamAv")
{
    services.Configure<ClamAvOptions>(...);
    services.AddScoped<IClamAvClient, NclamClient>();
    services.AddScoped<IVirusScanService, ClamAvVirusScanService>();  // correct
}
else
{
    services.AddScoped<IVirusScanService, NoopVirusScanService>();   // correct
}

// ... more registrations ...

// Line 174: BUG — unconditionally overrides whatever was registered above
services.AddScoped<IVirusScanService, NoopVirusScanService>();       // BUG
```

**Effect:** ClamAV is never active regardless of configuration. The code exists and is correct, but this duplicate registration kills it.

**ClamAV in docker-compose:** Not present. Service `clamav-daemon` must be added.

---

### Docker Compose

**File:** `docker-compose.yml`

**Current services:** `orvix-db`, `n8n`, `orvix-api`, `orvix-web`

**`orvix-api` volumes:**
```yaml
volumes:
  - uploads_data:/app/uploads
```

**Gaps:**
- No MinIO service
- No ClamAV daemon service  
- `uploads_data` volume exists for `LocalFileStorage` — will become legacy after MinIO

---

## Gap Registry (All Gaps, By Category)

### Security Gaps

| ID | Gap | Severity | Blocking Production? |
|----|-----|----------|---------------------|
| G-S1 | `NoopVirusScanService` DI bug overrides ClamAV registration | Critical | Yes |
| G-S2 | ClamAV daemon not in docker-compose | Critical | Yes |
| G-S3 | No download endpoint — files cannot be served (or served unsafely) | High | Yes |
| G-S4 | Delete endpoint has no department membership check | High | Yes |

### Storage Architecture Gaps

| ID | Gap | Severity |
|----|-----|----------|
| G-A1 | `LocalFileStorage` returns absolute filesystem paths as `StoragePath` | Critical |
| G-A2 | No MinIO service or implementation | Critical |
| G-A3 | No `StorageContext` value object | High |
| G-A4 | No `Storage:Provider` config switch in DI | High |
| G-A5 | No Azure Blob Storage provider | Medium |
| G-A6 | No SHA-256 hash at write time | Medium |
| G-A7 | No `StoredObject` metadata entity for cross-cutting file governance | Medium |

### Domain Gaps

| ID | Gap | Severity |
|----|-----|----------|
| G-D1 | `KnowledgeBaseDocument` has no `DepartmentId` | High |
| G-D2 | `KnowledgeBaseImage` paths will break when MinIO activated | High |
| G-D3 | No EF migration for `DepartmentId` on `KnowledgeBaseDocument` | High |
| G-D4 | No `StorageProvider` field on documents/images | Medium |

### API Gaps

| ID | Gap | Severity |
|----|-----|----------|
| G-P1 | No `GET /documents/{id}/download` endpoint | Critical |
| G-P2 | Upload endpoint has no `departmentId` parameter | High |
| G-P3 | List endpoint has no department-scoped filtering | High |
| G-P4 | No storage health check endpoint | Medium |

### Infrastructure Gaps

| ID | Gap | Severity |
|----|-----|----------|
| G-I1 | No MinIO in docker-compose | Critical |
| G-I2 | No ClamAV daemon in docker-compose | Critical |
| G-I3 | No MinIO bucket initializer | High |
| G-I4 | No MinIO NuGet package (AWSSDK.S3) | High |
| G-I5 | No Azure.Storage.Blobs NuGet for production | Medium |

### Test Gaps

| ID | Gap | Severity |
|----|-----|----------|
| G-T1 | Zero unit tests for storage layer | High |
| G-T2 | Zero tests for department-level file RBAC | High |
| G-T3 | No integration test for download endpoint | High |
| G-T4 | No ClamAV DI bug regression test | Medium |

---

## Phase Dependencies Map

```
Phase 02 (Storage Abstraction)
  └─ requires: nothing — pure interface and value object work

Phase 03 (MinIO Implementation)
  └─ requires: Phase 02 (StorageContext exists in IFileStorage)

Phase 04 (Domain: DepartmentId)
  └─ requires: Phase 02 (StorageContext model)
  └─ requires: nothing from Phase 03 (domain work is independent)

Phase 05 (RBAC + Download Endpoint)
  └─ requires: Phase 04 (DepartmentId on entity)
  └─ requires: Phase 03 (MinIO registered so download works end-to-end)

Phase 06 (StoredObject metadata)
  └─ requires: Phase 04 (enough DB migration foundation)

Phase 07 (FileIngestionJob adaptation)
  └─ requires: Phase 03 (MinIO wired)

Phase 08 (Azure Blob)
  └─ requires: Phase 03 (same IFileStorage interface used)

Phase 09 (Legacy file migration)
  └─ requires: Phase 03 + 06 (MinIO + StoredObject entity exist)

Phase 10 (Tests + health checks)
  └─ requires: all functional phases complete

Phase 11 (Cleanup)
  └─ requires: Phase 10 (all tests passing)
```

---

## Completion Criteria

This phase is complete when the execution agent has read:
- [ ] `OrvixFlow.Infrastructure/Storage/LocalFileStorage.cs`
- [ ] `OrvixFlow.Core/Interfaces/IFileStorage.cs`
- [ ] `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs`
- [ ] `OrvixFlow.Core/Entities/KnowledgeBaseImage.cs`
- [ ] `OrvixFlow.Api/Controllers/FileIngestionController.cs`
- [ ] `OrvixFlow.Infrastructure/Ai/IngestionPipelineService.cs`
- [ ] `OrvixFlow.Infrastructure/Ai/Jobs/FileIngestionJob.cs`
- [ ] `OrvixFlow.Infrastructure/DependencyInjection.cs`
- [ ] `docker-compose.yml`
- [ ] `memory/memory-risks.md`

And confirmed the gaps listed in this document match the actual code.

---

## Handoff to Phase 02

Phase 02 starts with the ClamAV DI bug fix and the storage abstraction upgrade.  
All gaps G-S1 through G-A4 are targeted across Phases 02–03.
