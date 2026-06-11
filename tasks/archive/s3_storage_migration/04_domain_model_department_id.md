# Phase 04 — Domain Model: DepartmentId on KnowledgeBaseDocument + EF Migration

> **Obsolete / Historical Migration Plan**
> Superseded by later storage and RBAC implementation by 2026-06-11.
> Use current code and `memory/memory-feature-map.md` before relying on this phase plan.

## Phase Goal

Add `DepartmentId?` to `KnowledgeBaseDocument` entity.  
Add FK relationship to `AppDbContext`.  
Create and apply the EF Core migration.  
Do NOT change any controller, service, or storage logic yet — that is Phase 05.

---

## Phase Purpose

Department-scoped file access requires the document entity to know which department it belongs to. This is a prerequisite for RBAC in Phase 05 and for the correct storage key construction in `MinIOFileStorage.BuildKey()`.

This phase separates the domain change (entity + migration) from the application change (RBAC + API) to keep each phase independently reversible.

---

## Scope

### Files to Modify

| File | Change |
|------|--------|
| `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs` | Add `DepartmentId?` and `Department?` navigation property |
| `OrvixFlow.Infrastructure/Data/AppDbContext.cs` | Add FK relationship + composite index for `(TenantId, DepartmentId)` |

### Files to Create

| File | Purpose |
|------|---------|
| `OrvixFlow.Infrastructure/Migrations/AddDepartmentIdToKnowledgeBaseDocument.cs` | EF Core migration (auto-generated, then reviewed) |

---

## Prerequisites

- Phase 03 complete (MinIO wired)
- `dotnet test` passes
- PostgreSQL running (`docker compose up -d orvix-db`)

---

## Implementation Instructions

### Step 1 — Update `KnowledgeBaseDocument` Entity

**File:** `OrvixFlow.Core/Entities/KnowledgeBaseDocument.cs`

Add `DepartmentId` and navigation property after the `TenantId` line:

```csharp
using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class KnowledgeBaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }           // NEW — null = company-wide

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public string SourceType { get; set; } = "Text";
    public string StoragePath { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAtUtc { get; set; }

    public Department? Department { get; set; }        // NEW — navigation property
    public ICollection<KnowledgeBase> Chunks { get; set; } = new List<KnowledgeBase>();
}
```

---

### Step 2 — Update `AppDbContext`

**File:** `OrvixFlow.Infrastructure/Data/AppDbContext.cs`

Find the section `// KnowledgeBaseDocument Relationship` (around line 360). It currently reads:

```csharp
// KnowledgeBaseDocument Relationship
modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasMany(d => d.Chunks)
    .WithOne(c => c.Document)
    .HasForeignKey(c => c.DocumentId)
    .OnDelete(DeleteBehavior.Cascade);
```

Append the following **after** that block (before `KnowledgeBaseImage` relationships):

```csharp
// KnowledgeBaseDocument — Department FK (nullable, set null on dept delete)
modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasOne(d => d.Department)
    .WithMany()
    .HasForeignKey(d => d.DepartmentId)
    .OnDelete(DeleteBehavior.SetNull);

// Composite index for tenant + department scoped queries
modelBuilder.Entity<KnowledgeBaseDocument>()
    .HasIndex(d => new { d.TenantId, d.DepartmentId });
```

> **Why `SetNull`?** If a department is deleted, documents should become company-wide (null `DepartmentId`) rather than cascade-deleted. Documents contain indexed content that may still be valuable organizationally.

---

### Step 3 — Generate EF Migration

Run from the solution root (where `OrvixFlow.sln` lives):

```bash
dotnet ef migrations add AddDepartmentIdToKnowledgeBaseDocument \
  --project OrvixFlow.Infrastructure \
  --startup-project OrvixFlow.Api
```

**Review the generated migration before applying.** Verify:
- `AddColumn` for `DepartmentId UUID NULL`
- `AddForeignKey` to `Departments` table with `onDelete: ReferentialAction.SetNull`
- `CreateIndex` for `(TenantId, DepartmentId)`
- **No data modifications** that could corrupt existing document records

The migration file will appear at:
`OrvixFlow.Infrastructure/Migrations/{timestamp}_AddDepartmentIdToKnowledgeBaseDocument.cs`

---

### Step 4 — Apply Migration

Apply to local dev database:

```bash
dotnet ef database update \
  --project OrvixFlow.Infrastructure \
  --startup-project OrvixFlow.Api
```

Verify with:
```bash
docker compose exec orvix-db psql -U orvix_admin -d orvixflow -c \
  "\d \"KnowledgeBaseDocuments\""
```

Expected: column `DepartmentId uuid` appears in the output.

---

## Constraints / Things Not to Break

### EF Core InMemory Test Handling

EF Core InMemory provider ignores foreign keys and some index constraints. Existing tests use InMemory. After this change:
- Tests that create `KnowledgeBaseDocument` without `DepartmentId` remain valid (nullable column)  
- Tests do NOT need to create `Department` records just because the FK exists — InMemory ignores FKs
- No existing test should break from this entity change

### Global Query Filter

`KnowledgeBaseDocument` already has a query filter by `TenantId`:
```csharp
modelBuilder.Entity<KnowledgeBaseDocument>().HasQueryFilter(d => d.TenantId == _tenantProvider.GetTenantId());
```

Do NOT add a department filter here — department filtering is done in controller logic (Phase 05), not at DB level. The global filter only gates at tenant level.

### Backward-Compatibility of Existing Documents

All existing `KnowledgeBaseDocument` rows will have `DepartmentId = NULL` after migration. This is correct — null means company-wide. They will remain accessible to `CompanyOwner` and `CompanyAdmin` roles, and invisible to `DepartmentManager`/`Operator` roles at department scope (enforced in Phase 05).

---

## Tests to Add

**File:** `OrvixFlow.Tests/KnowledgeBaseDocumentTests.cs` (new file or add to existing)

```csharp
[Fact]
public async Task KnowledgeBaseDocument_CanBeCreated_WithNullDepartmentId_ForCompanyWide()
{
    var db = TestDbContextFactory.Create();
    var tenantId = Guid.NewGuid();

    var doc = new KnowledgeBaseDocument
    {
        TenantId = tenantId,
        DepartmentId = null,   // company-wide
        FileName = "policy.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 1024,
        Status = "Pending"
    };
    db.KnowledgeBaseDocuments.Add(doc);
    await db.SaveChangesAsync();

    var loaded = await db.KnowledgeBaseDocuments.FindAsync(doc.Id);
    loaded.Should().NotBeNull();
    loaded!.DepartmentId.Should().BeNull();
}

[Fact]
public async Task KnowledgeBaseDocument_CanBeCreated_WithDepartmentId()
{
    var db = TestDbContextFactory.Create();
    var tenantId = Guid.NewGuid();
    var deptId = Guid.NewGuid();

    var doc = new KnowledgeBaseDocument
    {
        TenantId = tenantId,
        DepartmentId = deptId,  // dept-scoped
        FileName = "sales-report.pdf",
        Status = "Pending"
    };
    db.KnowledgeBaseDocuments.Add(doc);
    await db.SaveChangesAsync();

    var loaded = await db.KnowledgeBaseDocuments.FindAsync(doc.Id);
    loaded!.DepartmentId.Should().Be(deptId);
}
```

> Note: `TestDbContextFactory` is the pattern used across the existing test suite (unique `Guid.NewGuid()` InMemory DB name per test). Follow the same pattern as `IngestionPipelineServiceTests.cs`.

---

## Validation Checklist

- [ ] `KnowledgeBaseDocument.DepartmentId` is `Guid?` (nullable)
- [ ] `KnowledgeBaseDocument.Department` is `Department?` navigation property
- [ ] `AppDbContext` has the FK and composite index configuration
- [ ] Migration generated and reviewed — no data destructive operations
- [ ] Migration applied to dev PostgreSQL without error
- [ ] `dotnet test` passes (all existing tests still green)
- [ ] New domain-level tests pass

---

## Completion Criteria

- [ ] `KnowledgeBaseDocument` entity updated
- [ ] `AppDbContext` FK and index configured
- [ ] EF migration applied successfully
- [ ] No test regressions

---

## Handoff to Phase 05

Phase 05 wires the upload endpoint to accept `departmentId?`, adds the `CanAccessDepartment` helper, adds the download endpoint, and filters the list endpoint by department scope. All code in Phase 05 reads from the `DepartmentId` field added here.
