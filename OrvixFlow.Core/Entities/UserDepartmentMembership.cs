using System;

namespace OrvixFlow.Core.Entities;

public class UserDepartmentMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid DepartmentId { get; set; }
    public string DepartmentRole { get; set; } = "Member";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Tenant? Company { get; set; }
    public Department? Department { get; set; }
}
