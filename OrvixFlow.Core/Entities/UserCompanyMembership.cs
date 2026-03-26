using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class UserCompanyMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyRole { get; set; } = "Member";
    public string Status { get; set; } = "Active";
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    public DateTime? JoinedAt { get; set; }
    public Guid? InvitedByUserId { get; set; }

    public User? User { get; set; }
    public Tenant? Company { get; set; }
    public ICollection<UserDepartmentMembership> DepartmentMemberships { get; set; } = new List<UserDepartmentMembership>();
}
