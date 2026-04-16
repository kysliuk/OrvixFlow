using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Standard metric type constants for usage tracking.
/// Prevents silent metric type mismatches by providing a single source of truth.
/// </summary>
public static class UsageMetric
{
    public const string AiTokens       = "ai-tokens";
    public const string N8nNodes       = "n8n-nodes";
    public const string StorageMb      = "storage-mb";
    public const string KnowledgeBases = "knowledge-bases";
    public const string InboxMessages  = "inbox-messages";

    /// <summary>All known metric type values — used for validation and documentation.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        AiTokens, N8nNodes, StorageMb, KnowledgeBases, InboxMessages
    ];

    /// <summary>Returns true if <paramref name="metricType"/> is a recognised metric constant.</summary>
    public static bool IsValidMetric(string? metricType) =>
        metricType != null && All.Contains(metricType);
}
