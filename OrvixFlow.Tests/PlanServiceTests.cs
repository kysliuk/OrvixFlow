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

public class PlanServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly PlanService _planService;
    private readonly Guid _tenantId;

    public PlanServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
        _planService = new PlanService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreatePlan_WithModules_SavesAll()
    {
        var module1 = new ModuleDefinition { Key = "inbox", DisplayName = "Inbox", IsActive = true };
        var module2 = new ModuleDefinition { Key = "knowledge", DisplayName = "Knowledge", IsActive = true };
        _dbContext.ModuleDefinitions.AddRange(module1, module2);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            MonthlyPriceCents = 9900
        };

        var result = await _planService.CreatePlanAsync(plan, new[] { module1.Id, module2.Id });

        result.Should().NotBeNull();
        result.Name.Should().Be("Growth");
        result.ModuleInclusions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreatePlan_WithDuplicateSlug_ThrowsException()
    {
        var plan1 = new PlanTemplate { Name = "Starter", Slug = "starter" };
        await _planService.CreatePlanAsync(plan1);

        var plan2 = new PlanTemplate { Name = "Starter Two", Slug = "starter" };

        var act = () => _planService.CreatePlanAsync(plan2);
        await act.Should().ThrowAsync<PlanSlugAlreadyExistsException>();
    }

    [Fact]
    public async Task GetPlan_WithModules_IncludesModuleList()
    {
        var module = new ModuleDefinition { Key = "inbox", DisplayName = "Inbox", IsActive = true };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate { Name = "Growth", Slug = "growth" };
        await _planService.CreatePlanAsync(plan, new[] { module.Id });

        var result = await _planService.GetPlanBySlugAsync("growth");

        result.Should().NotBeNull();
        result!.ModuleInclusions.Should().HaveCount(1);
        result.ModuleInclusions.First().ModuleDefinition.Key.Should().Be("inbox");
    }

    [Fact]
    public async Task UpdatePlan_ModifiesCorrectly()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            MaxSeats = 5
        };
        await _planService.CreatePlanAsync(plan);

        plan.MonthlyPriceCents = 3900;
        plan.MaxSeats = 10;

        var result = await _planService.UpdatePlanAsync(plan);

        result.MonthlyPriceCents.Should().Be(3900);
        result.MaxSeats.Should().Be(10);
    }

    [Fact]
    public async Task ArchivePlan_SetsArchivedAt()
    {
        var plan = new PlanTemplate { Name = "Legacy", Slug = "legacy", IsActive = true };
        await _planService.CreatePlanAsync(plan);

        await _planService.ArchivePlanAsync(plan.Id);

        var result = await _planService.GetPlanByIdAsync(plan.Id);
        result!.IsActive.Should().BeFalse();
        result.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActivePlans_ReturnsOnlyActive()
    {
        var plan1 = new PlanTemplate { Name = "Free", Slug = "free", IsActive = true, IsFree = true };
        var plan2 = new PlanTemplate { Name = "Starter", Slug = "starter", IsActive = true };
        var plan3 = new PlanTemplate { Name = "Legacy", Slug = "legacy", IsActive = false };
        
        await _planService.CreatePlanAsync(plan1);
        await _planService.CreatePlanAsync(plan2);
        await _planService.CreatePlanAsync(plan3);

        var result = await _planService.GetActivePlansAsync();

        result.Should().HaveCount(2);
        result.Should().NotContain(p => p.Slug == "legacy");
    }

    [Fact]
    public async Task AddModuleToPlan_AddsSuccessfully()
    {
        var module = new ModuleDefinition { Key = "analytics", DisplayName = "Analytics", IsActive = true };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate { Name = "Growth", Slug = "growth" };
        await _planService.CreatePlanAsync(plan);

        await _planService.AddModuleToPlanAsync(plan.Id, module.Id);

        var result = await _planService.GetPlanByIdAsync(plan.Id);
        result!.ModuleInclusions.Should().HaveCount(1);
        result.ModuleInclusions.First().ModuleDefinition.Key.Should().Be("analytics");
    }

    [Fact]
    public async Task RemoveModuleFromPlan_RemovesSuccessfully()
    {
        var module = new ModuleDefinition { Key = "analytics", DisplayName = "Analytics", IsActive = true };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate { Name = "Growth", Slug = "growth" };
        await _planService.CreatePlanAsync(plan, new[] { module.Id });

        await _planService.RemoveModuleFromPlanAsync(plan.Id, module.Id);

        var result = await _planService.GetPlanByIdAsync(plan.Id);
        result!.ModuleInclusions.Should().BeEmpty();
    }

    [Fact]
    public async Task SetEntitlements_UpdatesCorrectly()
    {
        var plan = new PlanTemplate { Name = "Growth", Slug = "growth" };
        await _planService.CreatePlanAsync(plan);

        var entitlements = new PlanEntitlements
        {
            MaxMonthlyTokens = 500000,
            MaxApiRequestsPerDay = 5000,
            MaxStorageMb = 5000,
            MaxKnowledgeBases = 25
        };

        await _planService.SetEntitlementsAsync(plan.Id, entitlements);

        var result = await _planService.GetPlanByIdAsync(plan.Id);
        result!.Entitlements.Should().NotBeNull();
        result.Entitlements!.MaxMonthlyTokens.Should().Be(500000);
    }

    [Fact]
    public async Task CreatePlan_WithInvalidModule_ThrowsException()
    {
        var plan = new PlanTemplate { Name = "Growth", Slug = "growth" };
        var invalidModuleId = Guid.NewGuid();

        var act = () => _planService.CreatePlanAsync(plan, new[] { invalidModuleId });
        await act.Should().ThrowAsync<ModuleNotFoundException>();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
