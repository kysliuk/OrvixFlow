using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Platform-wide metadata registry for every file stored in object storage.
/// One row per physical object, including documents and generated images.
/// </summary>
public class StoredObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }

    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    public string StorageProvider { get; set; } = string.Empty;
    public string ContainerOrBucket { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;

    public string VirusScanStatus { get; set; } = "Pending";

    public string LifecycleStatus { get; set; } = "Active";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAtUtc { get; set; }

    public Guid CreatedByUserId { get; set; }
}
