using System;

namespace OrvixFlow.Core.Models;

public class AgentResponse
{
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Optional metadata like tokens used, finish reason, etc.
    public object? Metadata { get; set; }
}
