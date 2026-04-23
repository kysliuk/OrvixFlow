using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Tests;

public class ArchivedCompanyPurgeJobTests : IDisposable
{
    private readonly AppDbContext _db;

    public ArchivedCompanyPurgeJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(x => x.GetTenantId()).Returns(Guid.Empty);
        _db = new AppDbContext(options, tenantProvider.Object);
    }

    [Fact]
    public async Task ExecuteAsync_RehomesUsersWithOtherActiveCompanies_AndDeletesArchivedTenant()
    {
        var archivedCompanyId = Guid.NewGuid();
        var fallbackCompanyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _db.Tenants.AddRange(
            new Tenant
            {
                Id = archivedCompanyId,
                Name = "Archived Co",
                Plan = "Free",
                SubscriptionStatus = "Active",
                LifecycleStatus = "Archived",
                ArchivedAt = DateTime.UtcNow.AddDays(-70),
                DeletionScheduledFor = DateTime.UtcNow.AddDays(-1)
            },
            new Tenant
            {
                Id = fallbackCompanyId,
                Name = "Fallback Co",
                Plan = "Free",
                SubscriptionStatus = "Active",
                LifecycleStatus = "Active"
            });

        _db.Users.Add(new User
        {
            Id = userId,
            Email = "rehome@example.com",
            DisplayName = "Rehome User",
            TenantId = archivedCompanyId
        });
        _db.UserCompanyMemberships.AddRange(
            new UserCompanyMembership { UserId = userId, CompanyId = archivedCompanyId, CompanyRole = "CompanyOwner", Status = "Active" },
            new UserCompanyMembership { UserId = userId, CompanyId = fallbackCompanyId, CompanyRole = "CompanyAdmin", Status = "Active" });
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            LookupKey = "rehome-key",
            Token = "rehome-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        var job = new ArchivedCompanyPurgeJob(_db, Mock.Of<ILogger<ArchivedCompanyPurgeJob>>());

        await job.ExecuteAsync();

        var user = await _db.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == userId);
        user.TenantId.Should().Be(fallbackCompanyId);
        (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == archivedCompanyId)).Should().BeFalse();
        (await _db.RefreshTokens.IgnoreQueryFilters().FirstAsync(r => r.UserId == userId)).RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DeletesUsersWithoutFallbackCompany()
    {
        var archivedCompanyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _db.Tenants.Add(new Tenant
        {
            Id = archivedCompanyId,
            Name = "Archived Co",
            Plan = "Free",
            SubscriptionStatus = "Active",
            LifecycleStatus = "Archived",
            ArchivedAt = DateTime.UtcNow.AddDays(-70),
            DeletionScheduledFor = DateTime.UtcNow.AddDays(-1)
        });
        _db.Users.Add(new User
        {
            Id = userId,
            Email = "delete@example.com",
            DisplayName = "Delete User",
            TenantId = archivedCompanyId
        });
        _db.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = userId,
            CompanyId = archivedCompanyId,
            CompanyRole = "CompanyOwner",
            Status = "Active"
        });
        await _db.SaveChangesAsync();

        var job = new ArchivedCompanyPurgeJob(_db, Mock.Of<ILogger<ArchivedCompanyPurgeJob>>());

        await job.ExecuteAsync();

        (await _db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId)).Should().BeFalse();
        (await _db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == archivedCompanyId)).Should().BeFalse();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
