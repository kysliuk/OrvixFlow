using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Auth;

public class CompanyBootstrapService : ICompanyBootstrapService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CompanyBootstrapService> _logger;

    public CompanyBootstrapService(AppDbContext db, ILogger<CompanyBootstrapService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnsureOwnerBootstrapAsync(Guid userId, Guid companyId)
    {
        var exists = await _db.UserCompanyMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId);
        if (!exists)
        {
            _db.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = userId,
                CompanyId = companyId,
                CompanyRole = UserRole.CompanyOwner.ToClaimValue(),
                Status = "Active",
                InvitedAt = DateTime.UtcNow,
                JoinedAt = DateTime.UtcNow
            });
        }

        var defaultDepartment = await _db.Departments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.CompanyId == companyId && d.Code == "general");
        if (defaultDepartment == null)
        {
            defaultDepartment = new Department
            {
                CompanyId = companyId,
                Name = "General",
                Code = "general",
                IsActive = true
            };
            _db.Departments.Add(defaultDepartment);
            await _db.SaveChangesAsync();
        }

        var departmentMembershipExists = await _db.UserDepartmentMemberships
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == userId && m.CompanyId == companyId && m.DepartmentId == defaultDepartment.Id);
        if (!departmentMembershipExists)
        {
            _db.UserDepartmentMemberships.Add(new UserDepartmentMembership
            {
                UserId = userId,
                CompanyId = companyId,
                DepartmentId = defaultDepartment.Id,
                DepartmentRole = "Manager",
                Status = "Active"
            });
        }

        await EnsureDefaultSubscriptionAsync(companyId);

        await _db.SaveChangesAsync();
    }

    public async Task EnsureDefaultSubscriptionAsync(Guid companyId)
    {
        var exists = await _db.CompanySubscriptions
            .IgnoreQueryFilters()
            .AnyAsync(s => s.CompanyId == companyId);

        if (exists)
            return;

        var freePlanExists = await _db.PlanTemplates
            .IgnoreQueryFilters()
            .AnyAsync(p => p.Id == PlanCatalog.FreeId);

        if (!freePlanExists)
        {
            _logger.LogWarning(
                "Free plan template {PlanId} not found in DB — cannot create subscription for company {CompanyId}",
                PlanCatalog.FreeId,
                companyId);
            return;
        }

        _db.CompanySubscriptions.Add(new CompanySubscription
        {
            CompanyId = companyId,
            PlanTemplateId = PlanCatalog.FreeId,
            Status = SubscriptionState.Active,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddYears(100)
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Created Free plan subscription for company {CompanyId}", companyId);
    }
}
