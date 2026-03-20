using System;

namespace OrvixFlow.Core.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SubTier { get; set; } = "Free"; // e.g., Free, Pro, Enterprise
    public string ApiKeyHash { get; set; } = string.Empty;
}
