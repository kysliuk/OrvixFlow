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

public class CompanyModuleOverrideTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;
    private readonly EntitlementResolver _resolver;

    public CompanyModuleOverrideTests()
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
    public async Task GetModuleOverridesAsync_NoOverrides_ReturnsEmpty()
    {
        var result = await _resolver.GetModuleOverridesAsync(_tenantId);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CanUseModuleWithOverridesAsync_GrantedModule_ReturnsTrue()
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
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            Key = "premium-module",
            DisplayName = "Premium Module",
            IsActive = true
        };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var overrideEntity = new CompanyModuleOverride
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            ModuleDefinitionId = module.Id,
            IsEnabled = true,
            Note = "Trial access"
        };
        _dbContext.CompanyModuleOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.CanUseModuleWithOverridesAsync(_tenantId, "premium-module");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUseModuleWithOverridesAsync_SuppressedModule_ReturnsFalse()
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
            IsActive = true
        };

        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            Key = "inbox-guardian",
            DisplayName = "Inbox Guardian",
            IsActive = true
        };
        var inclusion = new PlanModuleInclusion
        {
            Id = Guid.NewGuid(),
            PlanTemplateId = plan.Id,
            ModuleDefinitionId = module.Id
        };
        plan.ModuleInclusions.Add(inclusion);

        _dbContext.PlanTemplates.Add(plan);
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var overrideEntity = new CompanyModuleOverride
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            ModuleDefinitionId = module.Id,
            IsEnabled = false,
            Note = "Suspended"
        };
        _dbContext.CompanyModuleOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.CanUseModuleWithOverridesAsync(_tenantId, "inbox-guardian");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUseModuleWithOverridesAsync_NoOverride_UsesPlanDefault()
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
            IsActive = true
        };

        var module = new ModuleDefinition
        {
            Id = Guid.NewGuid(),
            Key = "inbox-guardian",
            DisplayName = "Inbox Guardian",
            IsActive = true
        };
        var inclusion = new PlanModuleInclusion
        {
            Id = Guid.NewGuid(),
            PlanTemplateId = plan.Id,
            ModuleDefinitionId = module.Id
        };
        plan.ModuleInclusions.Add(inclusion);

        _dbContext.PlanTemplates.Add(plan);
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.CanUseModuleWithOverridesAsync(_tenantId, "inbox-guardian");
        result.Should().BeTrue();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
