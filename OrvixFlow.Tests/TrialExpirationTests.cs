using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class TrialExpirationTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public TrialExpirationTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExpiredTrial_ShouldBeDowngradedToFree()
    {
        var freePlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Free",
            Slug = "free",
            IsFree = true,
            IsActive = true,
            MaxSeats = 2
        };
        _dbContext.PlanTemplates.Add(freePlan);

        var paidPlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true,
            MaxSeats = 5
        };
        _dbContext.PlanTemplates.Add(paidPlan);

        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "Test Company",
            Plan = "starter",
            SubscriptionStatus = "Trialing"
        };
        _dbContext.Tenants.Add(tenant);

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = paidPlan.Id,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddHours(-1),
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-14),
            CurrentPeriodEnd = DateTime.UtcNow
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var expiredTrials = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Trialing && s.TrialEndsAt.HasValue && s.TrialEndsAt.Value <= now)
            .ToListAsync();

        expiredTrials.Should().HaveCount(1);

        foreach (var s in expiredTrials)
        {
            s.PlanTemplateId = freePlan.Id;
            s.Status = SubscriptionStatus.Active;
            s.TrialEndsAt = null;
            s.UpdatedAt = now;
        }

        var tenantAfter = await _dbContext.Tenants.FindAsync(_tenantId);
        tenantAfter!.Plan = freePlan.Slug;
        tenantAfter.SubscriptionStatus = SubscriptionStatus.Active;

        await _dbContext.SaveChangesAsync();

        var updated = await _dbContext.CompanySubscriptions.FindAsync(subscription.Id);
        updated!.Status.Should().Be(SubscriptionStatus.Active);
        updated.PlanTemplateId.Should().Be(freePlan.Id);
        updated.TrialEndsAt.Should().BeNull();

        var tenantFinal = await _dbContext.Tenants.FindAsync(_tenantId);
        tenantFinal!.Plan.Should().Be("free");
    }

    [Fact]
    public async Task ActiveTrial_ShouldNotBeAffected()
    {
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(7)
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var expiredTrials = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Trialing && s.TrialEndsAt.HasValue && s.TrialEndsAt.Value <= now)
            .ToListAsync();

        expiredTrials.Should().BeEmpty();
    }

    [Fact]
    public async Task ActiveSubscription_ShouldNotBeAffected()
    {
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionStatus.Active,
            TrialEndsAt = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var expiredTrials = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Trialing && s.TrialEndsAt.HasValue && s.TrialEndsAt.Value <= now)
            .ToListAsync();

        expiredTrials.Should().BeEmpty();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
