using System;

namespace OrvixFlow.Core.Entities;

public class CompanyEntitlementOverride
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }

    public int? MaxSeats { get; set; }
    public int? MaxMonthlyTokens { get; set; }
    public int? MaxApiRequestsPerDay { get; set; }
    public int? MaxStorageMb { get; set; }
    public int? MaxKnowledgeBases { get; set; }
    public int? MaxInboxMessages { get; set; }
    public int? MaxMailboxConnections { get; set; }

    public string Note { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant Company { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
