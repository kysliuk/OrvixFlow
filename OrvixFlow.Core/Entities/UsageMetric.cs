using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Standard metric type constants for usage tracking.
/// Prevents silent metric type mismatches by providing a single source of truth.
/// </summary>
public static class UsageMetric
{
    public const string AiTokens = "ai-tokens";
    public const string N8nNodes = "n8n-nodes";
    public const string StorageMb = "storage-mb";
    public const string KnowledgeBases = "knowledge-bases";
    public const string InboxMessages = "inbox-messages";
}
