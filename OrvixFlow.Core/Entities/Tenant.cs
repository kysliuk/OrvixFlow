using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Free | Starter | Pro | Enterprise</summary>
    public string Plan { get; set; } = "Free";

    /// <summary>Active | Trialing | Cancelled</summary>
    public string SubscriptionStatus { get; set; } = "Trialing";

    public string ApiKeyHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
    public ICollection<UserCompanyMembership> UserMemberships { get; set; } = new List<UserCompanyMembership>();
    public ICollection<UserDepartmentMembership> UserDepartmentMemberships { get; set; } = new List<UserDepartmentMembership>();
    public ICollection<ModuleAssignment> ModuleAssignments { get; set; } = new List<ModuleAssignment>();
}
