using System;

namespace OrvixFlow.Core.Entities;

public class PlanEntitlements
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanTemplateId { get; set; }
    public int MaxMonthlyTokens { get; set; } = 100000;
    public int MaxApiRequestsPerDay { get; set; } = 1000;
    public int MaxStorageMb { get; set; } = 500;
    public int MaxKnowledgeBases { get; set; } = 5;
    
    /// <summary>
    /// Maximum inbox messages per month. 0 = unlimited.
    /// </summary>
    public int MaxInboxMessagesPerMonth { get; set; } = 0;
    
    /// <summary>
    /// Maximum mailbox connections (email integrations).
    /// </summary>
    public int MaxMailboxConnections { get; set; } = 1;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlanTemplate PlanTemplate { get; set; } = null!;
}
