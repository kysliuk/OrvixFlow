using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class AccessResolverTests
{
    [Fact]
    public async Task Should_Union_Department_Module_Permissions()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentA = Guid.NewGuid();
        var departmentB = Guid.NewGuid();

        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(m => m.GetTenantId()).Returns(companyId);

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(mockTenantProvider.Object);
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("AccessResolverDb_" + Guid.NewGuid()));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant { Id = companyId, Name = "C1" });
            db.Users.Add(new User { Id = userId, TenantId = companyId, Email = "u@x.com", Role = "Operator" });
            db.Departments.AddRange(
                new Department { Id = departmentA, CompanyId = companyId, Name = "A", Code = "a" },
                new Department { Id = departmentB, CompanyId = companyId, Name = "B", Code = "b" }
            );
            db.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = userId,
                CompanyId = companyId,
                CompanyRole = "Operator",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
            db.UserDepartmentMemberships.AddRange(
                new UserDepartmentMembership { UserId = userId, CompanyId = companyId, DepartmentId = departmentA, Status = "Active", DepartmentRole = "Member" },
                new UserDepartmentMembership { UserId = userId, CompanyId = companyId, DepartmentId = departmentB, Status = "Active", DepartmentRole = "Member" }
            );
            var module = new ModuleDefinition { Key = "inbox-guardian", DisplayName = "Inbox", Tier = "Utility", Visibility = "UserFacing", IsActive = true };
            db.ModuleDefinitions.Add(module);
            await db.SaveChangesAsync();

            var assignmentA = new ModuleAssignment
            {
                CompanyId = companyId,
                ModuleDefinitionId = module.Id,
                DepartmentId = departmentA,
                Scope = "Department",
                IsEnabled = true
            };
            var assignmentB = new ModuleAssignment
            {
                CompanyId = companyId,
                ModuleDefinitionId = module.Id,
                DepartmentId = departmentB,
                Scope = "Department",
                IsEnabled = true
            };
            db.ModuleAssignments.AddRange(assignmentA, assignmentB);
            await db.SaveChangesAsync();

            db.ModulePermissionGrants.AddRange(
                new ModulePermissionGrant { ModuleAssignmentId = assignmentA.Id, CanView = true, CanUse = false, CanViewLogs = true },
                new ModulePermissionGrant { ModuleAssignmentId = assignmentB.Id, CanView = true, CanUse = true, CanViewLogs = false }
            );
            await db.SaveChangesAsync();

            var resolver = new AccessResolver(db);
            var permissions = await resolver.GetEffectivePermissionsAsync(userId, companyId, "inbox-guardian");
            permissions.CanView.Should().BeTrue();
            permissions.CanUse.Should().BeTrue();
            permissions.CanViewLogs.Should().BeTrue();
        }
    }
}
