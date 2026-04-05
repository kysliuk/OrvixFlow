using System;
using System.Collections.Generic;
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

public class PlanModuleTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PlanService _planService;
    private readonly string _dbName;

    public PlanModuleTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var mockTenantProvider = new MockTenantProvider();
        _db = new AppDbContext(options, mockTenantProvider);
        _db.Database.EnsureDeleted();
        _db.Database.EnsureCreated();
        _planService = new PlanService(_db);

        SeedModuleDefinitions();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private void SeedModuleDefinitions()
    {
        var modules = ModuleCatalog.BuildSeed();
        foreach (var module in modules)
        {
            if (_db.ModuleDefinitions.Find(module.Id) == null)
            {
                _db.ModuleDefinitions.Add(module);
            }
        }
        _db.SaveChanges();
    }

    [Fact]
    public async Task CreatePlan_WithModuleIds_CreatesInclusions()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var moduleIds = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.DocIntelId
        };

        var result = await _planService.CreatePlanAsync(plan, moduleIds);

        result.ModuleInclusions.Should().HaveCount(2);
        result.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.InboxGuardianId);
        result.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.DocIntelId);
    }

    [Fact]
    public async Task CreatePlan_WithInvalidModuleId_ThrowsModuleNotFoundException()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var invalidModuleId = Guid.NewGuid();

        await Assert.ThrowsAsync<ModuleNotFoundException>(() => _planService.CreatePlanAsync(plan, new[] { invalidModuleId }));
    }

    [Fact]
    public async Task CreatePlan_WithoutModuleIds_CreatesPlanWithNoInclusions()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var result = await _planService.CreatePlanAsync(plan, null);

        result.ModuleInclusions.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SyncModules_AddsNewModules_RemovesOld()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var initialModules = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.DocIntelId
        };

        await _planService.CreatePlanAsync(plan, initialModules);

        var newModules = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.FinanceFlowId,
            ModuleCatalog.LeadQualifierId
        };

        await _planService.SyncModulesForPlanAsync(plan.Id, newModules);

        var updatedPlan = await _planService.GetPlanByIdAsync(plan.Id);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.ModuleInclusions.Should().HaveCount(3);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.InboxGuardianId);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.FinanceFlowId);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.LeadQualifierId);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().NotContain(ModuleCatalog.DocIntelId);
    }

    [Fact]
    public async Task SyncModules_EmptyList_RemovesAll()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var initialModules = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.DocIntelId
        };

        await _planService.CreatePlanAsync(plan, initialModules);

        await _planService.SyncModulesForPlanAsync(plan.Id, new List<Guid>());

        var updatedPlan = await _planService.GetPlanByIdAsync(plan.Id);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.ModuleInclusions.Should().BeEmpty();
    }

    [Fact]
    public async Task AddModuleToPlan_AlreadyExists_NoDuplicate()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        await _planService.CreatePlanAsync(plan, new[] { ModuleCatalog.InboxGuardianId });

        await _planService.AddModuleToPlanAsync(plan.Id, ModuleCatalog.InboxGuardianId);

        var updatedPlan = await _planService.GetPlanByIdAsync(plan.Id);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.ModuleInclusions.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveModuleFromPlan_RemovesInclusion()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var moduleIds = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.DocIntelId
        };

        await _planService.CreatePlanAsync(plan, moduleIds);

        await _planService.RemoveModuleFromPlanAsync(plan.Id, ModuleCatalog.InboxGuardianId);

        var updatedPlan = await _planService.GetPlanByIdAsync(plan.Id);
        updatedPlan.Should().NotBeNull();
        updatedPlan!.ModuleInclusions.Should().HaveCount(1);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().NotContain(ModuleCatalog.InboxGuardianId);
        updatedPlan.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.DocIntelId);
    }

    [Fact]
    public async Task GetPlanById_IncludesModuleIds()
    {
        var plan = new PlanTemplate
        {
            Name = "Test Plan",
            Slug = "test-plan",
            Description = "Test",
            MonthlyPriceCents = 1000,
            YearlyPriceCents = 10000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14,
            LegacyLocked = false
        };

        var moduleIds = new List<Guid>
        {
            ModuleCatalog.InboxGuardianId,
            ModuleCatalog.DocIntelId
        };

        await _planService.CreatePlanAsync(plan, moduleIds);

        var result = await _planService.GetPlanByIdAsync(plan.Id);

        result.Should().NotBeNull();
        result!.ModuleInclusions.Should().HaveCount(2);
        result.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.InboxGuardianId);
        result.ModuleInclusions.Select(m => m.ModuleDefinitionId).Should().Contain(ModuleCatalog.DocIntelId);
    }

    private class MockTenantProvider : ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
    }
}
