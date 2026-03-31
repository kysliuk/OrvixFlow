using System;

namespace OrvixFlow.Core.Entities;

public class KnowledgeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public string Metadata { get; set; } = "{}"; // JSON string or JSONB
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public Pgvector.Vector? EmbeddingVector { get; set; }

    public Guid? DocumentId { get; set; }
    public KnowledgeBaseDocument? Document { get; set; }

    public int ChunkIndex { get; set; } = 0;
    public string ChunkType { get; set; } = "text"; // "text", "image_caption"
    public string Title { get; set; } = string.Empty;
}
