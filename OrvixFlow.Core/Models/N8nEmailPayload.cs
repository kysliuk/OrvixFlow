using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Models;

public record N8nEmailPayload
{
    public string Version { get; init; } = "1.0";
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string MessageId { get; init; } = string.Empty;
    public long ProcessingTimeMs { get; set; }

    public EmailDraftDetails Email { get; init; } = new();
    public ClassificationDetails Classification { get; init; } = new();
    public RagDetails Rag { get; init; } = new();
    public IReadOnlyList<KnowledgeImageRef> Images { get; init; } = [];

    public string Action { get; init; } = "draft_ready"; // draft_ready, human_review_required, insufficient_context, escalate, spam_detected
    
    public AutomationFlags Flags { get; init; } = new();
    public AuditDetails Audit { get; init; } = new();
}

public record EmailDraftDetails
{
    public string To { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public string? BodyHtml { get; init; }
}

public record ClassificationDetails
{
    public string Category { get; init; } = string.Empty;
    public float ConfidenceScore { get; init; }
    public string? Reasoning { get; init; }
    public bool RequiresHumanReview { get; init; }
    public string? ReasonForReview { get; init; }
}

public record RagDetails
{
    public int SnippetsUsed { get; init; }
    public bool HasContext { get; init; }
    public bool InsufficientContext { get; init; }
}

public record AutomationFlags
{
    public bool AutoSendAllowed { get; init; }
    public bool HumanReviewRequired { get; init; }
    public bool Escalate { get; init; }
}

public record AuditDetails
{
    public Guid TraceId { get; init; }
    public string Model { get; init; } = "gpt-4o";
    public int? EstimatedTokens { get; init; }
}
