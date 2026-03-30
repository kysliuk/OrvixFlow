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

public class EntitlementResolverIntegrationTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly EntitlementResolver _resolver;
    private readonly Guid _companyId;

    public EntitlementResolverIntegrationTests()
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
    public async Task FullAccessFlow_WithGrowthPlan_HasAllModules()
    {
        var inboxModule = new ModuleDefinition
        {
            Key = "inbox-guardian",
            DisplayName = "Inbox Guardian",
            IsActive = true
        };
        var docIntelModule = new ModuleDefinition
        {
            Key = "doc-intel",
            DisplayName = "Doc Intel",
            IsActive = true
        };
        var analyticsModule = new ModuleDefinition
        {
            Key = "analytics",
            DisplayName = "Analytics",
            IsActive = true
        };
        _dbContext.ModuleDefinitions.AddRange(inboxModule, docIntelModule, analyticsModule);
        await _dbContext.SaveChangesAsync();

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
            },
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = inboxModule.Id },
                new() { ModuleDefinitionId = docIntelModule.Id }
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var modules = (await _resolver.GetCompanyModulesAsync(_companyId)).ToList();
        modules.Should().HaveCount(2);
        modules.Any(m => m.Key == "inbox-guardian").Should().BeTrue();
        modules.Any(m => m.Key == "doc-intel").Should().BeTrue();
        modules.Any(m => m.Key == "analytics").Should().BeFalse();

        var canUseInbox = await _resolver.CanUseModuleAsync(_companyId, "inbox-guardian");
        canUseInbox.Should().BeTrue();

        var canUseAnalytics = await _resolver.CanUseModuleAsync(_companyId, "analytics");
        canUseAnalytics.Should().BeFalse();
    }

    [Fact]
    public async Task FullAccessFlow_EnterprisePlan_HasUnlimitedSeats()
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
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_companyId);

        entitlements.MaxSeats.Should().BeNull();
        entitlements.CanAddSeats(10000).Should().BeTrue();

        var canInvite = await _resolver.CanInviteUserAsync(_companyId, 5000);
        canInvite.Should().BeTrue();
    }

    [Fact]
    public async Task FullAccessFlow_FreePlan_LimitedAccess()
    {
        var plan = new PlanTemplate
        {
            Name = "Free",
            Slug = "free",
            MaxSeats = 2,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 50000,
                MaxApiRequestsPerDay = 500,
                MaxStorageMb = 100,
                MaxKnowledgeBases = 1
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_companyId);

        entitlements.MaxSeats.Should().Be(2);
        entitlements.MaxMonthlyTokens.Should().Be(50000);
        entitlements.MaxApiRequestsPerDay.Should().Be(500);
        entitlements.MaxStorageMb.Should().Be(100);
        entitlements.MaxKnowledgeBases.Should().Be(1);

        entitlements.CanAddSeats(1).Should().BeTrue();
        entitlements.CanAddSeats(2).Should().BeTrue();
        entitlements.CanAddSeats(3).Should().BeFalse();
    }

    [Fact]
    public async Task GetActivePlan_ReturnsCorrectPlan()
    {
        var plan = new PlanTemplate
        {
            Name = "Business",
            Slug = "business",
            MaxSeats = 100,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var activePlan = await _resolver.GetActivePlanAsync(_companyId);

        activePlan.Should().NotBeNull();
        activePlan!.Name.Should().Be("Business");
        activePlan.Slug.Should().Be("business");
    }

    [Fact]
    public async Task GetActivePlan_NoSubscription_ReturnsNull()
    {
        var activePlan = await _resolver.GetActivePlanAsync(_companyId);
        activePlan.Should().BeNull();
    }

    [Fact]
    public async Task GetSubscription_ReturnsSubscriptionWithPlanAndEntitlements()
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
            Status = "Trialing",
            TrialEndsAt = DateTime.UtcNow.AddDays(14)
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.GetSubscriptionAsync(_companyId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Trialing");
        result.PlanTemplate.Should().NotBeNull();
        result.PlanTemplate.Name.Should().Be("Growth");
        result.PlanTemplate.Entitlements.Should().NotBeNull();
        result.PlanTemplate.Entitlements!.MaxMonthlyTokens.Should().Be(500000);
    }

    [Fact]
    public async Task CanUseModule_NonExistentModule_ReturnsFalse()
    {
        var module = new ModuleDefinition
        {
            Key = "inbox-guardian",
            DisplayName = "Inbox Guardian",
            IsActive = true
        };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module.Id }
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var canUse = await _resolver.CanUseModuleAsync(_companyId, "non-existent-module");
        canUse.Should().BeFalse();
    }

    [Fact]
    public async Task GetCompanyModules_EmptyWhenNoSubscription()
    {
        var modules = await _resolver.GetCompanyModulesAsync(_companyId);
        modules.Should().BeEmpty();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
