using System;
using System.Collections.Generic;
using System.Linq;

namespace OrvixFlow.Core.Authorization;

/// <summary>
/// Canonical role hierarchy for OrvixFlow.
///
/// Platform (internal):
///   SuperAdmin       – full platform control
///   InternalOperator – platform support, cannot change billing/infra
///
/// Company (tenant-side):
///   CompanyOwner     – full company control
///   CompanyAdmin     – delegated company management
///   CompanyMember    – basic company membership, department-scoped access only
///
/// Department (stored on UserDepartmentMembership):
///   DepartmentManager  – manages within assigned department(s)
///   DepartmentOperator – works within assigned department(s)
///
/// Legacy aliases remain defined temporarily for compile compatibility while
/// the remaining authorization rollout is completed.
/// </summary>
public enum UserRole
{
    SuperAdmin = 1,
    InternalOperator = 2,
    CompanyOwner = 10,
    CompanyAdmin = 11,
    CompanyMember = 12,
    DepartmentManager = 20,
    DepartmentOperator = 30,
    Operator = DepartmentOperator,
    Viewer = 31,
}

public static class UserRoleExtensions
{
    public static bool IsPlatformAdmin(this UserRole role) =>
        role is UserRole.SuperAdmin or UserRole.InternalOperator;

    public static bool IsCompanyAdmin(this UserRole role) =>
        role is UserRole.CompanyOwner or UserRole.CompanyAdmin;

    public static bool IsCompanyAdminOrAbove(this UserRole role) =>
        role.IsCompanyAdmin() || role.IsPlatformAdmin();

    public static bool IsCompanyMemberOrAbove(this UserRole role) =>
        role.IsCompanyAdminOrAbove() || role == UserRole.CompanyMember;

    public static bool HasCompanyWideVisibility(this UserRole role) =>
        role.IsCompanyAdminOrAbove();

    public static string ToClaimValue(this UserRole role) => role switch
    {
        UserRole.Operator => UserRole.DepartmentOperator.ToString(),
        _ => role.ToString()
    };

    public static UserRole ParseRole(string? value) => value switch
    {
        "Owner" => UserRole.CompanyOwner,
        "Admin" => UserRole.CompanyAdmin,
        "CompanyMember" => UserRole.CompanyMember,
        "DepartmentManager" => UserRole.CompanyMember,
        "DepartmentOperator" => UserRole.CompanyMember,
        "Operator" => UserRole.CompanyMember,
        "Viewer" => UserRole.CompanyMember,
        "Member" => UserRole.CompanyMember,
        _ => Enum.TryParse<UserRole>(value, ignoreCase: true, out var r)
                 ? r
                 : UserRole.Viewer
    };

    public static UserRole ParseDeptRole(string? value) => value switch
    {
        "DepartmentManager" => UserRole.DepartmentManager,
        "Manager" => UserRole.DepartmentManager,
        "DepartmentOperator" => UserRole.DepartmentOperator,
        "Operator" => UserRole.DepartmentOperator,
        "Member" => UserRole.DepartmentOperator,
        _ => UserRole.DepartmentOperator
    };

    public static readonly IReadOnlyList<UserRole> AllRoles =
    [
        UserRole.SuperAdmin,
        UserRole.InternalOperator,
        UserRole.CompanyOwner,
        UserRole.CompanyAdmin,
        UserRole.CompanyMember,
        UserRole.DepartmentManager,
        UserRole.DepartmentOperator,
    ];

    public static readonly IReadOnlyList<UserRole> CompanyRoles =
    [
        UserRole.CompanyOwner,
        UserRole.CompanyAdmin,
        UserRole.CompanyMember,
    ];

    public static readonly IReadOnlySet<string> CompanyRoleNames = new HashSet<string>(
        CompanyRoles.Select(role => role.ToClaimValue()),
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> DepartmentRoleNames = new HashSet<string>(
        ["DepartmentManager", "DepartmentOperator"],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsHigherThan(this UserRole @this, UserRole other)
    {
        if (@this.IsPlatformAdmin())
            return false;

        return (int)@this > (int)other;
    }

    public static bool IsCompanyScopedRole(this UserRole role) => CompanyRoles.Contains(role);

    public static bool IsDepartmentScopedRole(this UserRole role) =>
        role is UserRole.DepartmentManager or UserRole.DepartmentOperator or UserRole.Operator;

    public static bool CanAssignCompanyRole(this UserRole caller, UserRole target, bool allowOwnerAssignment = false)
    {
        if (!target.IsCompanyScopedRole())
            return false;

        if (target == UserRole.CompanyOwner && !allowOwnerAssignment)
            return false;

        if (caller.IsPlatformAdmin())
            return true;

        if (!caller.IsCompanyAdmin())
            return false;

        return caller switch
        {
            UserRole.CompanyOwner => true,
            UserRole.CompanyAdmin => target != UserRole.CompanyOwner,
            _ => false,
        };
    }

    public static bool CanManageCompanyTarget(this UserRole caller, UserRole target)
    {
        if (!target.IsCompanyScopedRole())
            return false;

        if (caller.IsPlatformAdmin())
            return true;

        if (!caller.IsCompanyAdmin())
            return false;

        return (int)caller < (int)target;
    }

    public static bool CanAssignDepartmentRole(this UserRole caller, UserRole target)
    {
        if (!target.IsDepartmentScopedRole())
            return false;

        return caller.IsPlatformAdmin() || caller.IsCompanyAdmin() || caller == UserRole.CompanyMember;
    }

    public static string ToDepartmentRoleValue(this UserRole role) =>
        role == UserRole.DepartmentManager
            ? "DepartmentManager"
            : "DepartmentOperator";
}
