using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class SeatLimitTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly EntitlementResolver _resolver;
    private readonly Guid _companyId;

    public SeatLimitTests()
    {
        _companyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_companyId));
        _resolver = new EntitlementResolver(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CanInviteUser_AtExactLimit_ReturnsFalse()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            MaxSeats = 5,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        for (int i = 0; i < 5; i++)
        {
            var user = new User { Id = Guid.NewGuid(), TenantId = _companyId, Email = $"user{i}@test.com" };
            _dbContext.Users.Add(user);
            _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = user.Id,
                CompanyId = _companyId,
                CompanyRole = "Member",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        var canInvite = await _resolver.CanInviteUserAsync(_companyId, 5);
        canInvite.Should().BeFalse();
    }

    [Fact]
    public async Task CanInviteUser_BelowLimit_ReturnsTrue()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            MaxSeats = 5,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var user = new User { Id = Guid.NewGuid(), TenantId = _companyId, Email = "user@test.com" };
        _dbContext.Users.Add(user);
        _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = user.Id,
            CompanyId = _companyId,
            CompanyRole = "Member",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var canInvite = await _resolver.CanInviteUserAsync(_companyId, 1);
        canInvite.Should().BeTrue();
    }

    [Fact]
    public async Task CanInviteUser_UnlimitedSeats_ReturnsTrue()
    {
        var plan = new PlanTemplate
        {
            Name = "Enterprise",
            Slug = "enterprise",
            MaxSeats = null,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 10000000,
                MaxApiRequestsPerDay = 100000,
                MaxStorageMb = 500000,
                MaxKnowledgeBases = 1000
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var canInvite = await _resolver.CanInviteUserAsync(_companyId, 1000);
        canInvite.Should().BeTrue();
    }

    [Fact]
    public async Task CanInviteUser_NoSubscription_ReturnsTrue()
    {
        var canInvite = await _resolver.CanInviteUserAsync(_companyId, 0);
        canInvite.Should().BeTrue();
    }

    [Fact]
    public async Task GetEntitlements_ReturnsCorrectSeatLimit()
    {
        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            MaxSeats = 25,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 500000,
                MaxApiRequestsPerDay = 5000,
                MaxStorageMb = 5000,
                MaxKnowledgeBases = 25
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_companyId);

        entitlements.MaxSeats.Should().Be(25);
        entitlements.CanAddSeats(25).Should().BeTrue();
        entitlements.CanAddSeats(26).Should().BeFalse();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
