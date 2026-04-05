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

public class CompanySubscriptionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CompanySubscriptionService _subscriptionService;
    private readonly PlanService _planService;
    private readonly MockTenantProvider _mockTenantProvider;
    private readonly string _dbName;

    public CompanySubscriptionTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _mockTenantProvider = new MockTenantProvider();
        _db = new AppDbContext(options, _mockTenantProvider);
        _db.Database.EnsureDeleted();
        _db.Database.EnsureCreated();
        _planService = new PlanService(_db);
        _subscriptionService = new CompanySubscriptionService(_db, _planService);

        SeedTestData();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private void SeedTestData()
    {
        var tenant = new Tenant
        {
            Name = "Test Company",
            Plan = "Free",
            SubscriptionStatus = "Active"
        };
        _db.Tenants.Add(tenant);
        _db.SaveChanges();

        _mockTenantProvider.SetTenantId(tenant.Id);

        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            Description = "Test",
            MonthlyPriceCents = 2900,
            YearlyPriceCents = 29000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };
        _db.PlanTemplates.Add(plan);

        var plan2 = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            Description = "Test",
            MonthlyPriceCents = 9900,
            YearlyPriceCents = 99000,
            MaxSeats = 25,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };
        _db.PlanTemplates.Add(plan2);

        _db.SaveChanges();
    }

    [Fact]
    public async Task AssignPlan_CreatesSubscription_WhenNoneExists()
    {
        var tenant = await _db.Tenants.FirstAsync();
        var plan = await _db.PlanTemplates.FirstAsync(p => p.Slug == "starter");

        var subscription = await _subscriptionService.AssignPlanAsync(tenant.Id, plan.Id, "Monthly");

        subscription.Should().NotBeNull();
        subscription.CompanyId.Should().Be(tenant.Id);
        subscription.PlanTemplateId.Should().Be(plan.Id);
        subscription.Status.Should().Be("Trialing");
        subscription.BillingInterval.Should().Be("Monthly");

        var dbSubscription = await _db.CompanySubscriptions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.CompanyId == tenant.Id);
        dbSubscription.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignPlan_UpdatesSubscription_WhenExists()
    {
        var tenant = await _db.Tenants.FirstAsync();
        var starterPlan = await _db.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        var growthPlan = await _db.PlanTemplates.FirstAsync(p => p.Slug == "growth");

        await _subscriptionService.AssignPlanAsync(tenant.Id, starterPlan.Id, "Monthly");

        var updatedSubscription = await _subscriptionService.AssignPlanAsync(tenant.Id, growthPlan.Id, "Yearly");

        updatedSubscription.PlanTemplateId.Should().Be(growthPlan.Id);
        updatedSubscription.BillingInterval.Should().Be("Yearly");

        var count = await _db.CompanySubscriptions.IgnoreQueryFilters().CountAsync(s => s.CompanyId == tenant.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ChangePlanAsync_RequiresExistingSubscription()
    {
        var tenant = await _db.Tenants.FirstAsync();
        var plan = await _db.PlanTemplates.FirstAsync(p => p.Slug == "starter");

        await Assert.ThrowsAsync<SubscriptionNotFoundException>(() =>
            _subscriptionService.ChangePlanAsync(tenant.Id, plan.Id));
    }

    private class MockTenantProvider : ITenantProvider
    {
        private Guid _tenantId = Guid.Empty;
        public void SetTenantId(Guid id) => _tenantId = id;
        public Guid GetTenantId() => _tenantId;
    }
}
