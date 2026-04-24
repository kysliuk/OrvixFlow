using FluentAssertions;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Tests;

public class DepartmentRbacPhase1Tests
{
    [Fact]
    public void ParseRole_CompanyMember_ReturnsCompanyMember()
    {
        var role = UserRoleExtensions.ParseRole("CompanyMember");

        role.Should().Be(UserRole.CompanyMember);
    }

    [Fact]
    public void ParseRole_LegacyOperator_ReturnsCompanyMember()
    {
        var role = UserRoleExtensions.ParseRole("Operator");

        role.Should().Be(UserRole.CompanyMember);
    }

    [Fact]
    public void ParseDeptRole_DepartmentManager_ReturnsDepartmentManager()
    {
        var role = UserRoleExtensions.ParseDeptRole("DepartmentManager");

        role.Should().Be(UserRole.DepartmentManager);
    }

    [Fact]
    public void ParseDeptRole_LegacyMember_ReturnsDepartmentOperator()
    {
        var role = UserRoleExtensions.ParseDeptRole("Member");

        role.Should().Be(UserRole.DepartmentOperator);
    }

    [Fact]
    public void IsCompanyMemberOrAbove_CompanyMember_ReturnsTrue()
    {
        UserRole.CompanyMember.IsCompanyMemberOrAbove().Should().BeTrue();
    }

    [Fact]
    public void Invitation_CanStoreInvitedDepartmentRole()
    {
        var invitation = new Invitation
        {
            AssignedRole = "CompanyMember",
            InvitedDepartmentRole = "DepartmentOperator"
        };

        invitation.InvitedDepartmentRole.Should().Be("DepartmentOperator");
    }
}
