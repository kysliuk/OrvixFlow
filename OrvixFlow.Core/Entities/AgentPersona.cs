using System;

namespace OrvixFlow.Core.Entities;

public class AgentPersona
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    
    public string Tone { get; set; } = "Professional";
    public string CustomInstructions { get; set; } = string.Empty;
    public string? CustomSignOff { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
