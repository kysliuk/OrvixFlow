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
/// Tests for Phase 4 billing improvements:
/// - T4-1: GetSubscription uses GetEffectiveEntitlementsAsync (not just base entitlements)
/// - T4-2: AssignPlanAsync with targetStatus parameter for post-payment scenarios
/// - T4-3: Downgrade safety checks (KB count, storage, seats)
/// - T4-4: Usage alert hooks at 80%/100% thresholds
/// - T4-5: Admin subscription view endpoint
/// </summary>
public class BillingPhase4Tests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly EntitlementResolver _resolver;
    private readonly PlanService _planService;
    private readonly CompanySubscriptionService _subscriptionService;
    private readonly Guid _tenantId;

    public BillingPhase4Tests()
    {
        _tenantId = Guid.NewGuid();
        
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
            SortOrder = 1,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5,
                MaxInboxMessagesPerMonth = 1000,
                MaxMailboxConnections = 3
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
            SortOrder = 2,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 500000,
                MaxApiRequestsPerDay = 5000,
                MaxStorageMb = 5000,
                MaxKnowledgeBases = 25,
                MaxInboxMessagesPerMonth = 10000,
                MaxMailboxConnections = 10
            }
        };
        _dbContext.PlanTemplates.Add(growthPlan);

        var freePlan = new PlanTemplate
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Free",
            Slug = "free",
            MonthlyPriceCents = 0,
            MaxSeats = 2,
            IsActive = true,
            IsFree = true,
            SortOrder = 0,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 50000,
                MaxApiRequestsPerDay = 500,
                MaxStorageMb = 100,
                MaxKnowledgeBases = 1,
                MaxInboxMessagesPerMonth = 100,
                MaxMailboxConnections = 1
            }
        };
        _dbContext.PlanTemplates.Add(freePlan);

        _dbContext.SaveChanges();
    }

    #region T4-1: GetEffectiveEntitlements in GetSubscription

    [Fact]
    public async Task GetEffectiveEntitlements_ReturnsOverrides_WhenOverrideExists()
    {
        // Arrange - add entitlement override
        var overrideEntity = new CompanyEntitlementOverride
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            MaxMonthlyTokens = 500000, // Override to higher value
            Note = "Custom enterprise deal"
        };
        _dbContext.CompanyEntitlementOverrides.Add(overrideEntity);
        await _dbContext.SaveChangesAsync();

        // Assign Starter plan (base limit = 100,000)
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Act
        var effectiveEntitlements = await _resolver.GetEffectiveEntitlementsAsync(_tenantId);

        // Assert - should return override value (500,000), not base plan value (100,000)
        effectiveEntitlements.MaxMonthlyTokens.Should().Be(500000);
        effectiveEntitlements.HasEntitlementOverride.Should().BeTrue();
    }

    [Fact]
    public async Task GetEffectiveEntitlements_ReturnsBasePlan_WhenNoOverride()
    {
        // Assign Starter plan (base limit = 100,000)
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Act
        var effectiveEntitlements = await _resolver.GetEffectiveEntitlementsAsync(_tenantId);

        // Assert - should return base plan value
        effectiveEntitlements.MaxMonthlyTokens.Should().Be(100000);
        effectiveEntitlements.HasEntitlementOverride.Should().BeFalse();
    }

    #endregion

    #region T4-2: AssignPlanAsync with targetStatus parameter

    [Fact]
    public async Task AssignPlanAsync_WithTargetStatus_SetsStatusCorrectly()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");

        // Act - assign with targetStatus = Active (for post-payment scenarios)
        var subscription = await _subscriptionService.AssignPlanAsync(
            _tenantId, 
            starterPlan.Id, 
            null,  // billingInterval
            "Active");  // targetStatus - should be added

        // Assert
        subscription.Status.Should().Be(SubscriptionState.Active); // Not Trialing
    }

    [Fact]
    public async Task AssignPlanAsync_WithoutTargetStatus_SetsDefaultStatus()
    {
        // Arrange
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");

        // Act - assign without targetStatus (existing behavior)
        var subscription = await _subscriptionService.AssignPlanAsync(
            _tenantId, 
            starterPlan.Id, 
            null);  // no targetStatus

        // Assert - should default to Trialing for paid plans
        subscription.Status.Should().Be(SubscriptionState.Trialing);
    }

    #endregion

    #region T4-3: Downgrade Safety Checks

    [Fact]
    public async Task ChangePlanAsync_ThrowsDowngradeNotAllowed_WhenKbsExceedNewLimit()
    {
        // Arrange - current subscription with 5 KBs (Starter plan)
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add 5 KnowledgeBaseDocuments (actual KB "units")
        for (int i = 0; i < 5; i++)
        {
            _dbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                FileName = $"KB {i}.pdf",
                Status = "Indexed"
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - try to downgrade to Free plan (max 1 KB)
        var freePlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "free");
        var act = async () => await _subscriptionService.ChangePlanAsync(_tenantId, freePlan.Id, true);

        // Assert
        await act.Should().ThrowAsync<DowngradeNotAllowedException>()
            .WithMessage("*KB*exceed*limit*");
    }

    [Fact]
    public async Task ChangePlanAsync_ThrowsDowngradeNotAllowed_WhenStorageExceedsNewLimit()
    {
        // Arrange - current subscription with Starter plan (500MB storage)
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add storage usage event (300MB used)
        _dbContext.UsageEvents.Add(new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "test",
            MetricType = UsageMetric.StorageMb,
            Quantity = 300,
            OccurredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - try to downgrade to Free plan (100MB limit)
        var freePlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "free");
        var act = async () => await _subscriptionService.ChangePlanAsync(_tenantId, freePlan.Id, true);

        // Assert
        await act.Should().ThrowAsync<DowngradeNotAllowedException>()
            .WithMessage("*storage*exceed*limit*");
    }

    [Fact]
    public async Task ChangePlanAsync_ThrowsSeatLimitExceeded_WhenSeatsExceedNewLimit()
    {
        // Arrange - current subscription with Starter plan (5 seats)
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

        // Act - try to downgrade to Free plan (2 seats)
        var freePlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "free");
        var act = async () => await _subscriptionService.ChangePlanAsync(_tenantId, freePlan.Id, true);

        // Assert - SeatLimitExceededException is thrown (seats are checked first before KBs/storage)
        await act.Should().ThrowAsync<SeatLimitExceededException>()
            .WithMessage("*seats*exceed*limit*");
    }

    [Fact]
    public async Task ChangePlanAsync_Succeeds_WhenWithinNewPlanLimits()
    {
        // Arrange - Starter plan (5 seats, 5 KBs)
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Act - upgrade to Growth plan (25 seats, 25 KBs) - should succeed
        var growthPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "growth");
        var act = async () => await _subscriptionService.ChangePlanAsync(_tenantId, growthPlan.Id, true);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region T4-4: Usage Alert Hooks

    [Fact]
    public async Task CheckLimit_ReturnsCorrectPercentageAt80Percent()
    {
        // Arrange - Starter plan with 100,000 tokens
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add 80,000 tokens used (80% of limit)
        _dbContext.UsageEvents.Add(new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "test",
            MetricType = UsageMetric.AiTokens,
            Quantity = 80000,
            OccurredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckLimitAsync(_tenantId, "tokens");

        // Assert
        result.Limit.Should().Be(100000);
        result.CurrentUsage.Should().Be(80000);
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimit_BlocksWhenAt100Percent()
    {
        // Arrange - Starter plan with 100,000 tokens
        var starterPlan = await _dbContext.PlanTemplates.FirstAsync(p => p.Slug == "starter");
        await _subscriptionService.AssignPlanAsync(_tenantId, starterPlan.Id);

        // Add 100,000 tokens used (100% of limit)
        _dbContext.UsageEvents.Add(new UsageEvent
        {
            CompanyId = _tenantId,
            ModuleKey = "test",
            MetricType = UsageMetric.AiTokens,
            Quantity = 100000,
            OccurredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _resolver.CheckLimitAsync(_tenantId, "tokens", 1);

        // Assert - should block any additional usage
        result.Allowed.Should().BeFalse();
        result.CurrentUsage.Should().Be(100000);
    }

    #endregion

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}

/// <summary>
/// Tests for DowngradeNotAllowedException
/// </summary>
public class DowngradeSafetyTests
{
    [Fact]
    public void DowngradeNotAllowedException_ContainsCorrectMessage()
    {
        // Arrange & Act
        var exception = new DowngradeNotAllowedException("knowledgeBases", 5, 1);

        // Assert
        exception.Message.Should().Contain("5");
        exception.Message.Should().Contain("1");
        exception.ExceededLimit.Should().Be("knowledgeBases");
        exception.CurrentValue.Should().Be(5);
        exception.MaxAllowed.Should().Be(1);
    }
}
