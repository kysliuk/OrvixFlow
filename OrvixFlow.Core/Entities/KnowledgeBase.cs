using System;

namespace OrvixFlow.Core.Entities;

public class KnowledgeBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public string Metadata { get; set; } = "{}"; // JSON string or JSONB
    
    public Pgvector.Vector? EmbeddingVector { get; set; }
}
