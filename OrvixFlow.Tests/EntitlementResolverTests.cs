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

public class EntitlementResolverTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly EntitlementResolver _resolver;
    private readonly Guid _tenantId;

    public EntitlementResolverTests()
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
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetCompanyModules_IncludesPlanModules()
    {
        var module = new ModuleDefinition 
        { 
            Key = "inbox", 
            DisplayName = "Inbox", 
            IsActive = true 
        };
        _dbContext.ModuleDefinitions.Add(module);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module.Id }
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var modules = await _resolver.GetCompanyModulesAsync(_tenantId);

        modules.Should().HaveCount(1);
        modules.First().Key.Should().Be("inbox");
    }

    [Fact]
    public async Task GetEntitlements_ReturnsPlanEntitlements()
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
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        entitlements.MaxSeats.Should().Be(25);
        entitlements.MaxMonthlyTokens.Should().Be(500000);
        entitlements.MaxApiRequestsPerDay.Should().Be(5000);
    }

    [Fact]
    public async Task CanInviteUser_RespectsSeatLimit()
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
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var user = new User { Id = Guid.NewGuid(), TenantId = _tenantId, Email = "test@test.com" };
        _dbContext.Users.Add(user);
        
        _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
        {
            UserId = user.Id,
            CompanyId = _tenantId,
            CompanyRole = "CompanyOwner",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var canInvite = await _resolver.CanInviteUserAsync(_tenantId, 4);
        canInvite.Should().BeTrue();

        var cannotInvite = await _resolver.CanInviteUserAsync(_tenantId, 5);
        cannotInvite.Should().BeFalse();
    }

    [Fact]
    public async Task CanUseModule_ReturnsFalse_WhenModuleNotInPlan()
    {
        var module1 = new ModuleDefinition { Key = "inbox", DisplayName = "Inbox", IsActive = true };
        var module2 = new ModuleDefinition { Key = "analytics", DisplayName = "Analytics", IsActive = true };
        _dbContext.ModuleDefinitions.AddRange(module1, module2);
        await _dbContext.SaveChangesAsync();

        var plan = new PlanTemplate
        {
            Name = "Starter",
            Slug = "starter",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module1.Id }
            }
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var canUseInbox = await _resolver.CanUseModuleAsync(_tenantId, "inbox");
        canUseInbox.Should().BeTrue();

        var canUseAnalytics = await _resolver.CanUseModuleAsync(_tenantId, "analytics");
        canUseAnalytics.Should().BeFalse();
    }

    [Fact]
    public async Task GetActivePlan_ReturnsPlanTemplate()
    {
        var plan = new PlanTemplate
        {
            Name = "Growth",
            Slug = "growth",
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var activePlan = await _resolver.GetActivePlanAsync(_tenantId);

        activePlan.Should().NotBeNull();
        activePlan!.Name.Should().Be("Growth");
    }

    [Fact]
    public async Task GetSubscription_ReturnsSubscriptionWithPlan()
    {
        var plan = new PlanTemplate
        {
            Name = "Business",
            Slug = "business",
            MaxSeats = 100
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Trialing"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var result = await _resolver.GetSubscriptionAsync(_tenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("Trialing");
        result.PlanTemplate.Should().NotBeNull();
        result.PlanTemplate.MaxSeats.Should().Be(100);
    }

    [Fact]
    public async Task GetModules_ReturnsEmpty_WhenNoSubscription()
    {
        var modules = await _resolver.GetCompanyModulesAsync(_tenantId);

        modules.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitlements_ReturnsDefaults_WhenNoEntitlements()
    {
        var plan = new PlanTemplate
        {
            Name = "Free",
            Slug = "free"
        };
        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        entitlements.MaxSeats.Should().BeNull();
        entitlements.MaxMonthlyTokens.Should().Be(100000);
        entitlements.MaxApiRequestsPerDay.Should().Be(1000);
    }

    [Fact]
    public async Task GetEntitlements_ReturnsUsageData_WithIgnoreQueryFilters()
    {
        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = Guid.NewGuid(),
            Status = "Active",
            CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var usageEvent = new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "inbox-guardian",
            MetricType = "ai-tokens",
            Quantity = 5000,
            OccurredAt = DateTime.UtcNow.AddDays(-5)
        };
        _dbContext.UsageEvents.Add(usageEvent);

        var apiUsage = new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "inbox-guardian",
            MetricType = "api-requests",
            Quantity = 100,
            OccurredAt = DateTime.UtcNow
        };
        _dbContext.UsageEvents.Add(apiUsage);

        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        entitlements.TokensUsedThisPeriod.Should().Be(5000);
        entitlements.ApiRequestsUsedToday.Should().Be(100);
    }

    [Fact]
    public async Task GetEntitlements_FiltersUsageByBillingPeriod()
    {
        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = Guid.NewGuid(),
            Status = "Active",
            CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var currentEvent = new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "inbox-guardian",
            MetricType = "ai-tokens",
            Quantity = 5000,
            OccurredAt = DateTime.UtcNow.AddDays(-5)
        };
        _dbContext.UsageEvents.Add(currentEvent);

        var oldEvent = new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "doc-intel",
            MetricType = "ai-tokens",
            Quantity = 999999,
            OccurredAt = DateTime.UtcNow.AddYears(-1)
        };
        _dbContext.UsageEvents.Add(oldEvent);

        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        entitlements.TokensUsedThisPeriod.Should().Be(5000);
    }

    [Fact]
    public async Task GetEffectiveEntitlements_AppliesOverrides()
    {
        var subscription = new CompanySubscription
        {
            CompanyId = _tenantId,
            PlanTemplateId = Guid.NewGuid(),
            Status = "Active"
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var overrideEntity = new CompanyEntitlementOverride
        {
            CompanyId = _tenantId,
            MaxMonthlyTokens = 999999,
            Note = "Custom deal"
        };
        _dbContext.CompanyEntitlementOverrides.Add(overrideEntity);

        await _dbContext.SaveChangesAsync();

        var entitlements = await _resolver.GetEffectiveEntitlementsAsync(_tenantId);

        entitlements.MaxMonthlyTokens.Should().Be(999999);
        entitlements.HasEntitlementOverride.Should().BeTrue();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
