using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var superAdminEmail = "superadmin@orvixflow.local";
        var superAdminPassword = "SuperAdmin123!";

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == superAdminEmail))
        {
            logger.LogInformation("SuperAdmin user already exists, skipping seed.");
            return;
        }

        logger.LogInformation("Seeding SuperAdmin user...");

        var tenant = new Tenant
        {
            Name = "Platform Admin Org",
            Plan = "Enterprise",
            SubscriptionStatus = "Active"
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var user = new User
        {
            TenantId = tenant.Id,
            Email = superAdminEmail,
            DisplayName = "Platform Admin",
            OAuthProvider = "local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(superAdminPassword),
            Role = UserRole.SuperAdmin.ToClaimValue()
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var membership = new UserCompanyMembership
        {
            UserId = user.Id,
            CompanyId = tenant.Id,
            CompanyRole = UserRole.CompanyOwner.ToClaimValue(),
            Status = "Active",
            InvitedAt = DateTime.UtcNow,
            JoinedAt = DateTime.UtcNow
        };
        db.UserCompanyMemberships.Add(membership);

        var department = new Department
        {
            CompanyId = tenant.Id,
            Name = "General",
            Code = "general",
            IsActive = true
        };
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        var deptMembership = new UserDepartmentMembership
        {
            UserId = user.Id,
            CompanyId = tenant.Id,
            DepartmentId = department.Id,
            DepartmentRole = "Manager",
            Status = "Active"
        };
        db.UserDepartmentMemberships.Add(deptMembership);
        await db.SaveChangesAsync();

        logger.LogInformation("SuperAdmin user seeded: {Email}", superAdminEmail);
    }
}
