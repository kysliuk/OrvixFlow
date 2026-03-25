using System;

namespace OrvixFlow.Core.Models;

public class EmailClassification
{
    public string Category { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public string? ReasonForReview { get; set; }
}

public class EmailProcessingResult
{
    public EmailClassification Classification { get; set; } = new();
    public string DraftResponse { get; set; } = string.Empty;
    public bool HasContextFromKnowledgeBase { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class KnowledgeSnippet
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public float SimilarityScore { get; set; }
}
