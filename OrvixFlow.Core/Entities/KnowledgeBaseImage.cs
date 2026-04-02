using System;

namespace OrvixFlow.Core.Entities;

public class KnowledgeBaseImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public Guid? DocumentId { get; set; }
    public KnowledgeBaseDocument? Document { get; set; }

    public Guid? ChunkId { get; set; }
    public KnowledgeBase? Chunk { get; set; }

    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
    public string AltText { get; set; } = string.Empty; // LLM-generated caption
    public string? Caption { get; set; } // original doc caption if any

    // Caption embedding for semantic image search
    public Pgvector.Vector? CaptionEmbedding { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
