using System;

namespace OrvixFlow.Core.Entities;

public class WorkflowPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public string Category { get; set; } = string.Empty; // e.g. "Support"
    public bool AutoExecute { get; set; } = false;
    public decimal ConfidenceThreshold { get; set; } = 0.85m;
    
    // Comma-separated list of keywords that trigger a hold despite high confidence
    public string ExcludedKeywords { get; set; } = string.Empty; 
}
