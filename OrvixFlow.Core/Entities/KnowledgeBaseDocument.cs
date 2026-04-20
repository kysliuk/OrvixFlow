using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class KnowledgeBaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public string SourceType { get; set; } = "Text"; // Text, PDF, DOCX, Image
    public string StoragePath { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // Pending, Processing, Indexed, Failed
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAtUtc { get; set; }

    public Department? Department { get; set; }
    public ICollection<KnowledgeBase> Chunks { get; set; } = new List<KnowledgeBase>();
}
