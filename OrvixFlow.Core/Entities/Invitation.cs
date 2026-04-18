using System;

namespace OrvixFlow.Core.Entities;

/// <summary>
/// Tracks a pending user invitation to a company.
/// On acceptance, the invited user's UserCompanyMembership and UserDepartmentMembership
/// are created with the pre-assigned role and department.
/// </summary>
public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Email address of the person being invited.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Company the user is being invited to.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Canonical role assigned at invite time (e.g. "CompanyAdmin", "DepartmentManager").</summary>
    public string AssignedRole { get; set; } = string.Empty;

    /// <summary>Optional: department the user is pre-assigned to.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>SHA-256 hash of the opaque token sent in the invitation email link.</summary>
    public string Token { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending"; // Pending | Accepted | Expired | Revoked

    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid InvitedByUserId { get; set; }

    // Navigation
    public Tenant? Company { get; set; }
    public Department? Department { get; set; }
}
