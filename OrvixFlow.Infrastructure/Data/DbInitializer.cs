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
        // F-33 FIX: Backfill existing users who don't have EmailVerified set, 
        // but ONLY if they don't have a pending verification token (which indicates they are new/active verification cycle).
        var unverifiedCount = await db.Users.IgnoreQueryFilters()
            .Where(u => u.EmailVerified == false && u.OAuthProvider == "local" && u.VerificationToken == null)
            .CountAsync();
        
        if (unverifiedCount > 0)
        {
            logger.LogInformation("Backfilling {Count} existing legacy users with email verification...", unverifiedCount);
            await db.Users.IgnoreQueryFilters()
                .Where(u => u.EmailVerified == false && u.OAuthProvider == "local" && u.VerificationToken == null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailVerified, true));
            logger.LogInformation("Backfill complete: {Count} legacy users verified", unverifiedCount);
        }

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
            Role = UserRole.SuperAdmin.ToClaimValue(),
            EmailVerified = true // Platform admin is pre-verified
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
