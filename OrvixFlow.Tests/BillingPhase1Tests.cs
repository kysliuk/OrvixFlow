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

/// <summary>
/// Tests for Phase 1 billing improvements:
/// - T1-2/T1-3: Tenant sync in lifecycle operations
/// - T1-4: Subscription status gate
/// - T1-5: Effective entitlements in IsWithin* methods
/// - T1-6: Seat limit fix in CheckLimitAsync
/// </summary>
public class BillingPhase1Tests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly EntitlementResolver _resolver;
    private readonly CompanySubscriptionService _subscriptionService;
    private readonly PlanService _planService;
    private readonly Guid _tenantId;
    private readonly Guid _tenantId2; // For cross-tenant tests

    public BillingPhase1Tests()
    {
        _tenantId = Guid.NewGuid();
        _tenantId2 = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var mockTenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, mockTenantProvider);
        _resolver = new EntitlementResolver(_dbContext);
        _planService = new PlanService(_dbContext);
        _subscriptionService = new CompanySubscriptionService(_dbContext, _planService);
        
        SeedTestData();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void SeedTestData()
    {
        // Create tenant
        _dbContext.Tenants.Add(new Tenant 
        { 
            Id = _tenantId, 
            Name = "Test Company", 
            Plan = "Free", 
            SubscriptionStatus = "Active" 
        });
        _dbContext.Tenants.Add(new Tenant 
        { 
            Id = _tenantId2, 
            Name = "Test Company 2", 
            Plan = "Free", 
            SubscriptionStatus = "Active" 
        });

        // Create plans
        var starterPlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5
            }
        };
        _dbContext.PlanTemplates.Add(starterPlan);

        var growthPlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Growth",
            Slug = "growth",
            MonthlyPriceCents = 9900,
            MaxSeats = 25,
            IsActive = true,
            IsFree = false,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 500000,
                MaxApiRequestsPerDay = 5000,
                MaxStorageMb = 5000,
                MaxKnowledgeBases = 25
            }
        };
        _dbContext.PlanTemplates.Add(growthPlan);

        _dbContext.SaveChanges();
    }

    #region T1-2/T1-3: Tenant Sync Tests

    [Fact]
    public async Task SuspendSubscription_SyncsTenantStatus()
    {
        // Arrange
        var tenant = await _dbContext.Tenants.FirstAsync(t => t.Id == _tenantId);
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        
        tenant.Plan.Should().Be("starter");
        tenant.SubscriptionStatus.Should().Be("Trialing");

        // Act
        await _subscriptionService.SuspendSubscriptionAsync(_tenantId);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FindAsync(_tenantId);
        updatedTenant!.SubscriptionStatus.Should().Be("Suspended");
        updatedTenant.Plan.Should().Be("starter"); // Plan should remain
    }

    [Fact]
    public async Task CancelSubscription_SyncsTenantStatus()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        
        // Act
        await _subscriptionService.CancelSubscriptionAsync(_tenantId);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FindAsync(_tenantId);
        updatedTenant!.SubscriptionStatus.Should().Be("Cancelled");
        updatedTenant.Plan.Should().Be("starter"); // Plan should remain
    }

    [Fact]
    public async Task ReactivateSubscription_SyncsTenantStatus()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        await _subscriptionService.SuspendSubscriptionAsync(_tenantId);
        
        // Act
        await _subscriptionService.ReactivateSubscriptionAsync(_tenantId);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FindAsync(_tenantId);
        updatedTenant!.SubscriptionStatus.Should().Be("Active");
        updatedTenant.Plan.Should().Be("starter");
    }

    [Fact]
    public async Task ChangePlan_Immediate_SyncsTenantPlan()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        var growthPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "growth");
        
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        
        // Act
        await _subscriptionService.ChangePlanAsync(_tenantId, growthPlan.Id, immediate: true);

        // Assert
        var updatedTenant = await _dbContext.Tenants.FindAsync(_tenantId);
        updatedTenant!.Plan.Should().Be("growth");
    }

    #endregion

    #region T1-4: Subscription Status Gate Tests

    [Fact]
    public async Task CancelledSubscription_GetEntitlements_ReturnsZeroLimits()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        await _subscriptionService.CancelSubscriptionAsync(_tenantId);

        // Act
        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        // Assert
        entitlements.MaxMonthlyTokens.Should().Be(0);
        entitlements.MaxSeats.Should().BeNull();
        entitlements.MaxStorageMb.Should().Be(0);
        entitlements.MaxKnowledgeBases.Should().Be(0);
    }

    [Fact]
    public async Task SuspendedSubscription_GetEntitlements_ReturnsZeroLimits()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        await _subscriptionService.SuspendSubscriptionAsync(_tenantId);

        // Act
        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId);

        // Assert
        entitlements.MaxMonthlyTokens.Should().Be(0);
        entitlements.MaxSeats.Should().BeNull();
    }

    [Fact]
    public async Task CancelledSubscription_CanUseModuleWithOverrides_ReturnsFalse()
    {
        // Arrange
        var module = new ModuleDefinition 
        { 
            Key = "inbox", 
            DisplayName = "Inbox", 
            IsActive = true 
        };
        _dbContext.ModuleDefinitions.Add(module);
        
        var starterPlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter-with-inbox",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module.Id }
            }
        };
        _dbContext.PlanTemplates.Add(starterPlan);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);
        await _subscriptionService.CancelSubscriptionAsync(_tenantId);

        // Act
        var canUse = await _resolver.CanUseModuleWithOverridesAsync(_tenantId, "inbox");

        // Assert
        canUse.Should().BeFalse();
    }

    [Fact]
    public async Task ActiveSubscription_CanUseModuleWithOverrides_ReturnsTrue()
    {
        // Arrange
        var module = new ModuleDefinition 
        { 
            Key = "inbox", 
            DisplayName = "Inbox", 
            IsActive = true 
        };
        _dbContext.ModuleDefinitions.Add(module);
        
        var starterPlan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter-with-inbox-2",
            ModuleInclusions = new List<PlanModuleInclusion>
            {
                new() { ModuleDefinitionId = module.Id }
            }
        };
        _dbContext.PlanTemplates.Add(starterPlan);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Act
        var canUse = await _resolver.CanUseModuleWithOverridesAsync(_tenantId, "inbox");

        // Assert
        canUse.Should().BeTrue();
    }

    [Fact]
    public async Task NoSubscription_GetEntitlements_ReturnsZeroLimits()
    {
        // Act - no subscription created
        var entitlements = await _resolver.GetEntitlementsAsync(_tenantId2);

        // Assert
        entitlements.MaxMonthlyTokens.Should().Be(0);
        entitlements.MaxSeats.Should().BeNull();
    }

    #endregion

    #region T1-5: Effective Entitlements in IsWithin* Tests

    [Fact]
    public async Task IsWithinTokenLimit_RespectsAdminOverride_NotBasePlanLimit()
    {
        // Arrange - Starter plan has 100,000 tokens
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add override to give 1,000,000 tokens
        _dbContext.CompanyEntitlementOverrides.Add(new CompanyEntitlementOverride
        {
            CompanyId = _tenantId,
            MaxMonthlyTokens = 1000000,
            Note = "Custom enterprise deal"
        });
        await _dbContext.SaveChangesAsync();

        // Act - try to add 150,000 tokens (exceeds base 100k but within override)
        var canUse = await _resolver.IsWithinTokenLimitAsync(_tenantId, 150000);

        // Assert - should be allowed due to override
        canUse.Should().BeTrue();
    }

    [Fact]
    public async Task IsWithinTokenLimit_WithoutOverride_UsesBasePlanLimit()
    {
        // Arrange - Starter plan has 100,000 tokens
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Act - try to add 150,000 tokens (exceeds base 100k)
        var canUse = await _resolver.IsWithinTokenLimitAsync(_tenantId, 150000);

        // Assert - should NOT be allowed (no override)
        canUse.Should().BeFalse();
    }

    [Fact]
    public async Task CanInviteUser_RespectsAdminOverride()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add override to give 10 seats
        _dbContext.CompanyEntitlementOverrides.Add(new CompanyEntitlementOverride
        {
            CompanyId = _tenantId,
            MaxSeats = 10,
            Note = "Custom deal"
        });
        await _dbContext.SaveChangesAsync();

        // Act - try to invite 7th user (base plan has 5 seats)
        var canInvite = await _resolver.CanInviteUserAsync(_tenantId, 6);

        // Assert - should be allowed due to override
        canInvite.Should().BeTrue();
    }

    #endregion

    #region T1-6: Seat Limit Fix in CheckLimitAsync

    [Fact]
    public async Task CheckLimit_Seats_ReturnsActualMemberCount()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add 3 active members
        for (int i = 0; i < 3; i++)
        {
            var user = new User 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                Email = $"user{i}@test.com" 
            };
            _dbContext.Users.Add(user);
            
            _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = user.Id,
                CompanyId = _tenantId,
                CompanyRole = "Operator",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckLimitAsync(_tenantId, "seats");

        // Assert
        result.CurrentUsage.Should().Be(3); // Actual count, not 0
        result.Limit.Should().Be(5); // Starter plan limit
        result.ExceededLimit.Should().Be("Seats");
        result.Allowed.Should().BeTrue(); // 3 < 5

        // Add 2 more to exceed
        result = await _resolver.CheckLimitAsync(_tenantId, "seats", 3);
        result.Allowed.Should().BeFalse(); // 3+3 = 6 > 5
    }

    [Fact]
    public async Task CheckLimit_Seats_BlocksWhenAtLimit()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add 5 active members (at limit)
        for (int i = 0; i < 5; i++)
        {
            var user = new User 
            { 
                Id = Guid.NewGuid(), 
                TenantId = _tenantId, 
                Email = $"user{i}@test.com" 
            };
            _dbContext.Users.Add(user);
            
            _dbContext.UserCompanyMemberships.Add(new UserCompanyMembership
            {
                UserId = user.Id,
                CompanyId = _tenantId,
                CompanyRole = "Operator",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckLimitAsync(_tenantId, "seats", 1);

        // Assert
        result.Allowed.Should().BeFalse();
        result.CurrentUsage.Should().Be(5);
    }

    #endregion

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
