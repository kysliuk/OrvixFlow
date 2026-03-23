namespace OrvixFlow.Api;

/// <summary>
/// Centralized role hierarchy for OrvixFlow.
/// Higher-level roles inherit all permissions of lower-level roles.
/// </summary>
public static class Roles
{
    // Ordered from lowest to highest privilege
    public const string Viewer    = "Viewer";
    public const string Operator  = "Operator";
    public const string Owner     = "Owner";
    public const string Admin     = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    private static readonly HashSet<string> AdminRoles = [Admin, SuperAdmin, Owner];
    private static readonly HashSet<string> ElevatedRoles = [Operator, Owner, Admin, SuperAdmin];

    /// <summary>True for Owner, Admin, and SuperAdmin — can access the admin panel and impersonate tenants.</summary>
    public static bool IsAdmin(string? role) => AdminRoles.Contains(role ?? "");

    /// <summary>True for Operator and above — can trigger workflows and test features.</summary>
    public static bool IsElevated(string? role) => ElevatedRoles.Contains(role ?? "");
}
