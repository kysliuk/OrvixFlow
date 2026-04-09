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

    /// <summary>
    /// F-08 Fix: Returns true if <paramref name="this"/> (the caller's role)
    /// has lower privilege than <paramref name="other"/> (the target role),
    /// i.e., the caller cannot assign the other role.
    ///
    /// Platform roles (SuperAdmin, InternalOperator) can assign any role.
    /// Company roles use their numeric enum value — lower number = more privilege.
    /// CompanyOwner(10) > CompanyAdmin(11), so CompanyAdmin cannot assign CompanyOwner.
    /// </summary>
    /// <param name="this">The caller's role.</param>
    /// <param name="other">The role being assigned.</param>
    /// <returns>True if the caller cannot assign the target role.</returns>
    public static bool IsHigherThan(this UserRole @this, UserRole other)
    {
        // Platform admins can assign any company role
        if (@this.IsPlatformAdmin())
            return false;

        // For company roles, lower enum value = higher privilege
        // SuperAdmin(1) < InternalOperator(2) < CompanyOwner(10) < CompanyAdmin(11) < ... < Viewer(31)
        // So CompanyAdmin(11).IsHigherThan(CompanyOwner(10)) = 11 > 10 = true
        return (int)@this > (int)other;
    }
}
