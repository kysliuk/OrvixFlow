using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    // Legacy single-company link kept for compatibility paths.
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"google" | "microsoft" | "local"</summary>
    public string OAuthProvider { get; set; } = "local";

    /// <summary>OAuth subject claim (null for local accounts)</summary>
    public string? ExternalId { get; set; }

    /// <summary>BCrypt hash (null for OAuth accounts)</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Global platform role (SuperAdmin / InternalOperator). Empty for normal users.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>F-33: Email verified via verification token.</summary>
    public bool EmailVerified { get; set; } = false;

    /// <summary>F-33: Verification token (single-use, expires after 48h).</summary>
    public string? VerificationToken { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<UserCompanyMembership> CompanyMemberships { get; set; } = new List<UserCompanyMembership>();
    public ICollection<UserDepartmentMembership> DepartmentMemberships { get; set; } = new List<UserDepartmentMembership>();
    public ICollection<ModuleAssignment> ModuleAssignments { get; set; } = new List<ModuleAssignment>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
