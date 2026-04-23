using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

public class ArchivedCompanyPurgeJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<ArchivedCompanyPurgeJob> _logger;

    public ArchivedCompanyPurgeJob(AppDbContext db, ILogger<ArchivedCompanyPurgeJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var archivedCompanies = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.LifecycleStatus == "Archived" && t.DeletionScheduledFor.HasValue && t.DeletionScheduledFor.Value <= now)
            .ToListAsync();

        if (archivedCompanies.Count == 0)
        {
            _logger.LogInformation("No archived companies eligible for purge at {Now}", now);
            return;
        }

        foreach (var company in archivedCompanies)
        {
            _logger.LogInformation("Purging archived company {CompanyId} ({CompanyName})", company.Id, company.Name);

            var users = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == company.Id)
                .ToListAsync();

            foreach (var user in users)
            {
                var fallbackCompanyId = await _db.UserCompanyMemberships
                    .IgnoreQueryFilters()
                    .Where(m => m.UserId == user.Id && m.Status == "Active" && m.CompanyId != company.Id)
                    .Join(
                        _db.Tenants.IgnoreQueryFilters().Where(t => t.LifecycleStatus != "Archived"),
                        m => m.CompanyId,
                        t => t.Id,
                        (m, t) => m.CompanyId)
                    .FirstOrDefaultAsync();

                var refreshTokens = await _db.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(r => r.UserId == user.Id && r.RevokedAt == null)
                    .ToListAsync();

                foreach (var token in refreshTokens)
                    token.RevokedAt = now;

                if (fallbackCompanyId != Guid.Empty)
                {
                    user.TenantId = fallbackCompanyId;
                    continue;
                }

                _db.Users.Remove(user);
            }

            _db.Tenants.Remove(company);
        }

        await _db.SaveChangesAsync();
    }
}
