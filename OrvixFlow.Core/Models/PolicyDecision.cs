using System;

namespace OrvixFlow.Core.Models;

public enum PolicyDecisionType
{
    AutoExecute,
    HoldForApproval
}

public class PolicyDecision
{
    public PolicyDecisionType Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? ActionRequestId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool ShouldSendCallback { get; set; } = true;
}

public class PolicyEvaluationContext
{
    public string Category { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string? DraftResponse { get; set; }
}
