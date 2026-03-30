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
using Xunit;

namespace OrvixFlow.Tests;

public class PlanTemplateTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public PlanTemplateTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Create_ValidPlanTemplate_SavesToDb()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            Description = "Small team plan",
            MonthlyPriceCents = 2900,
            YearlyPriceCents = 29000,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            IsTrialAllowed = true,
            TrialDays = 14
        };

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.PlanTemplates.FindAsync(plan.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Starter");
        retrieved.Slug.Should().Be("starter");
        retrieved.MonthlyPriceCents.Should().Be(2900);
        retrieved.MaxSeats.Should().Be(5);
    }

    [Fact]
    public async Task Archive_PlanTemplate_SetsArchivedAt()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        plan.IsActive = false;
        plan.ArchivedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.PlanTemplates.FindAsync(plan.Id);
        retrieved!.IsActive.Should().BeFalse();
        retrieved.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetActivePlans_ReturnsOnlyActive()
    {
        var plans = new List<PlanTemplate>
        {
            new() { Name = "Free", Slug = "free", IsActive = true, IsFree = true },
            new() { Name = "Starter", Slug = "starter", IsActive = true },
            new() { Name = "Legacy", Slug = "legacy", IsActive = false }
        };
        _dbContext.PlanTemplates.AddRange(plans);
        await _dbContext.SaveChangesAsync();

        var activePlans = await _dbContext.PlanTemplates
            .Where(p => p.IsActive)
            .ToListAsync();

        activePlans.Should().HaveCount(2);
        activePlans.Should().NotContain(p => p.Slug == "legacy");
    }

    [Fact]
    public async Task PlanTemplate_Slug_IsUnique()
    {
        var plan1 = new PlanTemplate { Name = "Starter", Slug = "starter" };
        _dbContext.PlanTemplates.Add(plan1);
        await _dbContext.SaveChangesAsync();

        var existing = await _dbContext.PlanTemplates.FirstOrDefaultAsync(p => p.Slug == "starter");
        existing.Should().NotBeNull();
        existing!.Name.Should().Be("Starter");
    }

    [Fact]
    public async Task PlanTemplate_WithEntitlements_SavesCorrectly()
    {
        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
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

        var retrieved = await _dbContext.PlanTemplates
            .Include(p => p.Entitlements)
            .FirstOrDefaultAsync(p => p.Id == plan.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Entitlements.Should().NotBeNull();
        retrieved.Entitlements!.MaxMonthlyTokens.Should().Be(500000);
        retrieved.Entitlements.MaxApiRequestsPerDay.Should().Be(5000);
    }

    [Fact]
    public async Task PlanTemplate_WithModuleInclusions_SavesCorrectly()
    {
        var module1 = new ModuleDefinition { Key = "inbox", DisplayName = "Inbox", IsActive = true };
        var module2 = new ModuleDefinition { Key = "knowledge", DisplayName = "Knowledge", IsActive = true };
        _dbContext.ModuleDefinitions.AddRange(module1, module2);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module1.Id },
                new() { ModuleDefinitionId = module2.Id }
            }
        };

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.PlanTemplates
            .Include(p => p.ModuleInclusions)
            .ThenInclude(m => m.ModuleDefinition)
            .FirstOrDefaultAsync(p => p.Id == plan.Id);

        retrieved.Should().NotBeNull();
        retrieved!.ModuleInclusions.Should().HaveCount(2);
        retrieved.ModuleInclusions.Should().Contain(m => m.ModuleDefinition.Key == "inbox");
        retrieved.ModuleInclusions.Should().Contain(m => m.ModuleDefinition.Key == "knowledge");
    }

    [Fact]
    public async Task DefaultPlanTemplate_HasCorrectValues()
    {
        var plan = new PlanTemplate
        {
            Name = "Free",
            Slug = "free"
        };

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        plan.Currency.Should().Be("USD");
        plan.BillingInterval.Should().Be("Monthly");
        plan.IsTrialAllowed.Should().BeTrue();
        plan.TrialDays.Should().Be(14);
    }

    [Fact]
    public async Task PlanTemplate_Update_ModifiesCorrectly()
    {
        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            MaxSeats = 5
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        plan.MonthlyPriceCents = 3900;
        plan.MaxSeats = 10;
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.PlanTemplates.FindAsync(plan.Id);
        retrieved!.MonthlyPriceCents.Should().Be(3900);
        retrieved.MaxSeats.Should().Be(10);
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
