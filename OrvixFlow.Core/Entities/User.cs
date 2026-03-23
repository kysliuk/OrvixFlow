using System;

namespace OrvixFlow.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"google" | "microsoft" | "local"</summary>
    public string OAuthProvider { get; set; } = "local";

    /// <summary>OAuth subject claim (null for local accounts)</summary>
    public string? ExternalId { get; set; }

    /// <summary>BCrypt hash (null for OAuth accounts)</summary>
    public string? PasswordHash { get; set; }

    /// <summary>"Owner" | "Member"</summary>
    public string Role { get; set; } = "Owner";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Tenant? Tenant { get; set; }
}
