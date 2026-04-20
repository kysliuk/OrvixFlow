using System;

namespace OrvixFlow.Core.Models;

/// <summary>
/// Typed context for a storage operation. Replaces loose tuple parameters
/// while keeping the storage abstraction provider-neutral.
/// </summary>
public record StorageContext(
    Guid TenantId,
    Guid? DepartmentId,
    Guid DocumentId,
    string OriginalFileName);
