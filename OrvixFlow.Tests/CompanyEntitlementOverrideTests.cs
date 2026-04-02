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

public class CompanyEntitlementOverrideTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;
    private readonly EntitlementResolver _resolver;

    public CompanyEntitlementOverrideTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
        _resolver = new EntitlementResolver(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetEntitlementOverrideAsync_NoOverride_ReturnsNull()
    {
        var result = await _resolver.GetEntitlementOverrideAsync(_tenantId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEntitlementOverrideAsync_OverrideExists_ReturnsOverride()
    {
        var overrideEntity = new CompanyEntitlementOverride
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            MaxMonthlyTokens = 200000,
            MaxSeats = 10,
            Note = "VIP customer"
        };
        _dbContext.CompanyEntitlementOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.GetEntitlementOverrideAsync(_tenantId);
        result.Should().NotBeNull();
        result!.MaxMonthlyTokens.Should().Be(200000);
        result.MaxSeats.Should().Be(10);
        result.Note.Should().Be("VIP customer");
    }

    [Fact]
    public async Task GetEffectiveEntitlementsAsync_WithOverride_AppliesOverrides()
    {
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = Guid.NewGuid(),
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var plan = new PlanTemplate
        {
            Id = subscription.PlanTemplateId,
            Name = "Starter",
            Slug = "starter",
            MaxSeats = 5,
            IsActive = true
        };
        plan.Entitlements = new PlanEntitlements
        {
            PlanTemplateId = plan.Id,
            MaxMonthlyTokens = 100000,
            MaxStorageMb = 500,
            MaxKnowledgeBases = 5,
            MaxApiRequestsPerDay = 1000
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var overrideEntity = new CompanyEntitlementOverride
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            MaxMonthlyTokens = 500000,
            MaxStorageMb = 2000,
            Note = "Custom deal"
        };
        _dbContext.CompanyEntitlementOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync();

        var effective = await _resolver.GetEffectiveEntitlementsAsync(_tenantId);

        effective.MaxMonthlyTokens.Should().Be(500000);
        effective.MaxStorageMb.Should().Be(2000);
        effective.MaxSeats.Should().Be(5); // Not overridden, uses plan default
        effective.HasEntitlementOverride.Should().BeTrue();
        effective.OverrideNote.Should().Be("Custom deal");
    }

    [Fact]
    public async Task GetEffectiveEntitlementsAsync_NoOverride_UsesPlanDefaults()
    {
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = Guid.NewGuid(),
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var plan = new PlanTemplate
        {
            Id = subscription.PlanTemplateId,
            Name = "Starter",
            Slug = "starter",
            MaxSeats = 5,
            IsActive = true
        };
        plan.Entitlements = new PlanEntitlements
        {
            PlanTemplateId = plan.Id,
            MaxMonthlyTokens = 100000,
            MaxStorageMb = 500,
            MaxKnowledgeBases = 5,
            MaxApiRequestsPerDay = 1000
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var effective = await _resolver.GetEffectiveEntitlementsAsync(_tenantId);

        effective.MaxMonthlyTokens.Should().Be(100000);
        effective.HasEntitlementOverride.Should().BeFalse();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
