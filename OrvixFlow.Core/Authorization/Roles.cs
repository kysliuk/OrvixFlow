using System;
using System.Collections.Generic;

namespace OrvixFlow.Core.Authorization;

/// <summary>
/// Canonical role hierarchy for OrvixFlow.
///
/// Platform (internal):
///   SuperAdmin       – full platform control
///   InternalOperator – platform support, cannot change billing/infra
///
/// Company (tenant-side):
///   CompanyOwner     – first signup; full company control
///   CompanyAdmin     – delegated company management
///
/// Department:
///   DepartmentManager – manages/views data within assigned department(s)
///
/// Execution / basic:
///   Operator         – performs work within assigned modules
///   Viewer           – read-only within assigned modules
/// </summary>
public enum UserRole
{
    // Platform
    SuperAdmin       = 1,
    InternalOperator = 2,

    // Company
    CompanyOwner = 10,
    CompanyAdmin = 11,

    // Department
    DepartmentManager = 20,

    // Execution
    Operator = 30,
    Viewer   = 31,
}

public static class UserRoleExtensions
{
    /// <summary>Returns true for any platform-level role.</summary>
    public static bool IsPlatformAdmin(this UserRole role) =>
        role is UserRole.SuperAdmin or UserRole.InternalOperator;

    /// <summary>Returns true for any company-level admin.</summary>
    public static bool IsCompanyAdmin(this UserRole role) =>
        role is UserRole.CompanyOwner or UserRole.CompanyAdmin;

    /// <summary>Returns true for company admin or higher (including platform).</summary>
    public static bool IsCompanyAdminOrAbove(this UserRole role) =>
        role.IsCompanyAdmin() || role.IsPlatformAdmin();

    /// <summary>Returns true for full company-wide data visibility.</summary>
    public static bool HasCompanyWideVisibility(this UserRole role) =>
        role.IsCompanyAdminOrAbove();

    /// <summary>Serialise to the canonical string value stored in DB and JWT.</summary>
    public static string ToClaimValue(this UserRole role) => role.ToString();

    /// <summary>
    /// Parse a JWT/DB string into a <see cref="UserRole"/>.
    /// Maps legacy strings (Owner → CompanyOwner, Member → Operator).
    /// Falls back to <see cref="UserRole.Viewer"/> if unrecognised.
    /// </summary>
    public static UserRole ParseRole(string? value) => value switch
    {
        // Legacy aliases
        "Owner"  => UserRole.CompanyOwner,
        "Member" => UserRole.Operator,
        "Admin"  => UserRole.CompanyAdmin,
        _ => Enum.TryParse<UserRole>(value, ignoreCase: true, out var r)
                 ? r
                 : UserRole.Viewer
    };

    /// <summary>All values, ordered from most to least privileged.</summary>
    public static readonly IReadOnlyList<UserRole> AllRoles =
    [
        UserRole.SuperAdmin, UserRole.InternalOperator,
        UserRole.CompanyOwner, UserRole.CompanyAdmin,
        UserRole.DepartmentManager,
        UserRole.Operator, UserRole.Viewer,
    ];
}
