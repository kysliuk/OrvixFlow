# F-20: MinIO Local Storage with Department-Level Isolation

**Date:** 2026-04-09 | **Feature:** File Storage Migration | **Status:** Planned

---

## Overview

Implement S3-compatible local storage using MinIO with:
- Department-level isolation
- Role-based access control
- CompanyAdmin full access
- Docker-based local deployment (free)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Docker Compose                           │
├──────────────┬──────────────┬──────────────┬────────────────┤
│  orvix-api   │  orvix-web   │  orvix-db    │  minio         │
│   (8080)     │   (3000)     │   (5432)     │  :9000 (API)   │
│              │              │              │  :9001 (UI)    │
└──────────────┴──────────────┴──────────────┴────────────────┘
       │                                        │
       │    AWS SDK (S3-compatible)             │
       └──────────────┬────────────────────────┘
                      │
              ┌───────▼───────┐
              │  MinIO        │
              │  /data        │
              │  (volume)     │
              └───────────────┘
```

### Storage Key Structure

```
orvixflow/
└── companies/
    └── {companyId}/
        └── departments/
            └── {departmentId}/
                └── documents/
                    └── {documentId}/
                        └── {guid}.{extension}
```

---

## Implementation Steps

### Phase 1: Infrastructure Setup

| # | Task | File(s) | Description |
|---|------|---------|-------------|
| 1.1 | Add MinIO to docker-compose | `docker-compose.yml` | Add minio service, ports 9000/9001, volume |
| 1.2 | Add .env variables | `.env`, `.env.example` | MinIO access key, secret key, endpoint |
| 1.3 | Create bucket on startup | New: `MinIOBucketInitializer.cs` | Auto-create `orvixflow` bucket |

### Phase 2: Core Storage Implementation

| # | Task | File(s) | Description |
|---|------|---------|-------------|
| 2.1 | Add DepartmentId to document | `KnowledgeBaseDocument.cs` | Add nullable `DepartmentId` field |
| 2.2 | Create IFileStorage v2 | `IFileStorage.cs` | Add department-aware methods |
| 2.3 | Implement MinIOFileStorage | New: `MinIOFileStorage.cs` | AWS SDK S3 client implementation |
| 2.4 | Add DI registration | `Program.cs` | Register MinIO storage based on config |

### Phase 3: Access Control

| # | Task | File(s) | Description |
|---|------|---------|-------------|
| 3.1 | Add department access check | `FileAccessValidator.cs` | Validate user can access department's files |
| 3.2 | Update FileIngestionController | `FileIngestionController.cs` | Accept DepartmentId, validate access |
| 3.3 | Return department in list API | `KnowledgeBaseController.cs` | Include DepartmentId in document listing |

### Phase 4: Frontend (Optional for MVP)

| # | Task | File(s) | Description |
|---|------|---------|-------------|
| 4.1 | Add department picker | Upload UI | User selects department before upload |
| 4.2 | Show department in file list | Knowledge base UI | Display department column |

---

## Database Migration

```csharp
// Add to KnowledgeBaseDocument
public Guid? DepartmentId { get; set; }

// Add index
HasIndex(x => new { x.TenantId, x.DepartmentId });
```

---

## Access Control Rules

```
┌─────────────────────┬──────────────────────────────────────────┐
│ Role                │ Access                                   │
├─────────────────────┼──────────────────────────────────────────┤
│ SuperAdmin          │ All companies, all departments           │
│ InternalOperator    │ Read-only, all companies                 │
│ CompanyOwner        │ All departments in their company         │
│ CompanyAdmin        │ All departments in their company         │
│ DepartmentManager   │ Assigned departments only                │
│ Operator            │ Own department only                      │
│ Viewer              │ Own department read-only                 │
└─────────────────────┴──────────────────────────────────────────┘
```

---

## Configuration

### Environment Variables

```bash
# MinIO (S3-compatible)
STORAGE_PROVIDER=MinIO                    # Local | MinIO
MINIO_ENDPOINT=minio:9000
MINIO_ACCESS_KEY=orvixflow_access
MINIO_SECRET_KEY=orvixflow_secret
MINIO_BUCKET=orvixflow
MINIO_USE_SSL=false

# Fallback (dev without MinIO)
STORAGE__LOCAL__BASEPATH=/app/uploads
```

### appsettings.json

```json
"Storage": {
  "Provider": "MinIO",
  "MinIO": {
    "Endpoint": "minio:9000",
    "Bucket": "orvixflow"
  },
  "Local": {
    "BasePath": "/app/uploads"
  }
}
```

---

## Files to Create

| File | Path |
|------|------|
| MinIOBucketInitializer.cs | `OrvixFlow.Infrastructure/Storage/` |
| MinIOFileStorage.cs | `OrvixFlow.Infrastructure/Storage/` |
| FileAccessValidator.cs | `OrvixFlow.Infrastructure/Authorization/` |

## Files to Modify

| File | Change |
|------|--------|
| `KnowledgeBaseDocument.cs` | Add `DepartmentId?` |
| `IFileStorage.cs` | Add department-aware methods |
| `docker-compose.yml` | Add minio service |
| `.env` | Add minio credentials |
| `FileIngestionController.cs` | Add DepartmentId, access validation |
| `Program.cs` | Add MinIO DI registration |

---

## Effort Estimate

| Phase | Hours |
|-------|-------|
| Infrastructure (MinIO) | 1 |
| Storage Implementation | 2 |
| Access Control | 2 |
| Migration + Testing | 1 |
| **Total** | **6 hours** |

---

## Rollback Plan

- Keep `LocalFileStorage` as fallback
- Toggle via `STORAGE_PROVIDER=Local`
- No breaking changes to existing API contracts

---

## Dependencies

- AWS SDK for .NET (`AWSSDK.S3`)
- MinIO Docker image (`minio/minio`)
- Existing `KnowledgeBaseDocument` entity
- Existing `UserRole` enum and extensions
