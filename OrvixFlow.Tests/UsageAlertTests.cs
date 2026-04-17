using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for UsageAlertService (T3-4).
/// </summary>
public class UsageAlertTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<ILogger<UsageAlertService>> _mockLogger;
    private readonly UsageAlertService _service;
    private readonly Guid _tenantId;

    public UsageAlertTests()
    {
        _tenantId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var mockTenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, mockTenantProvider);
        _mockEmailService = new Mock<IEmailService>();
        _mockLogger = new Mock<ILogger<UsageAlertService>>();
        
        _service = new UsageAlertService(_dbContext, _mockEmailService.Object, _mockLogger.Object);
        
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
            Plan = "Free" 
        });

        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@test.com",
            PasswordHash = "hash",
            TenantId = _tenantId
        };
        _dbContext.Users.Add(owner);

        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            IsActive = true,
            IsFree = false,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000
            }
        };
        _dbContext.PlanTemplates.Add(plan);

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        var membership = new UserCompanyMembership
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            CompanyId = _tenantId,
            CompanyRole = "CompanyOwner",
            Status = "Active"
        };
        _dbContext.UserCompanyMemberships.Add(membership);
        
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CheckAndAlertAsync_Below80Percent_NoNotificationQueued()
    {
        // Act - 50% usage should not trigger alert
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 50000, 100000);
        
        // Assert - no notification queued
        var count = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task CheckAndAlertAsync_At80Percent_QueuesWarningNotification()
    {
        // Act - exactly 80% usage should trigger warning
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 80000, 100000);
        
        // Assert - one notification queued
        var notifications = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .ToListAsync();
        
        notifications.Should().HaveCount(1);
        notifications[0].Type.Should().Be("UsageWarning80");
        notifications[0].Percentage.Should().Be(80);
        notifications[0].RecipientEmail.Should().Be("owner@test.com");
    }

    [Fact]
    public async Task CheckAndAlertAsync_At100Percent_QueuesCriticalNotification()
    {
        // Act - 100% usage should trigger critical
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 100000, 100000);
        
        // Assert - one notification queued
        var notifications = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .ToListAsync();
        
        notifications.Should().HaveCount(1);
        notifications[0].Type.Should().Be("UsageCritical100");
        notifications[0].Percentage.Should().Be(100);
    }

    [Fact]
    public async Task CheckAndAlertAsync_DuplicateWarning_SkipsSecondAlert()
    {
        // Act - send warning twice
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 80000, 100000);
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 85000, 100000);
        
        // Assert - only one notification in queue
        var count = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .CountAsync(n => n.Type == "UsageWarning80");
        
        count.Should().Be(1, "idempotency should prevent duplicate warnings in same period");
    }

    [Fact]
    public async Task CheckAndAlertAsync_CanSendDifferentAlertTypes()
    {
        // Act - send warning, then critical
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 80000, 100000);
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 100000, 100000);
        
        // Assert - both types queued
        var warnings = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .CountAsync(n => n.Type == "UsageWarning80");
        var criticals = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .CountAsync(n => n.Type == "UsageCritical100");
        
        warnings.Should().Be(1);
        criticals.Should().Be(1);
    }

    [Fact]
    public async Task CheckAndAlertAsync_ZeroLimit_Skips()
    {
        // Act
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 50000, 0);
        
        // Assert - no notification
        var count = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .CountAsync();
        
        count.Should().Be(0);
    }

    [Fact]
    public async Task HasAlertBeenSentThisPeriodAsync_NoAlertSent_ReturnsFalse()
    {
        // Act
        var result = await _service.HasAlertBeenSentThisPeriodAsync(_tenantId, "UsageWarning80");
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAlertBeenSentThisPeriodAsync_AlertSent_ReturnsTrue()
    {
        // Arrange - queue an alert first
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 80000, 100000);
        
        // Act
        var result = await _service.HasAlertBeenSentThisPeriodAsync(_tenantId, "UsageWarning80");
        
        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAndAlertAsync_MultipleOwners_SendsToAll()
    {
        // Arrange - add another owner
        var owner2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner2@test.com",
            PasswordHash = "hash",
            TenantId = _tenantId
        };
        _dbContext.Users.Add(owner2);

        var membership2 = new UserCompanyMembership
        {
            Id = Guid.NewGuid(),
            UserId = owner2.Id,
            CompanyId = _tenantId,
            CompanyRole = "CompanyOwner",
            Status = "Active"
        };
        _dbContext.UserCompanyMemberships.Add(membership2);
        await _dbContext.SaveChangesAsync();
        
        // Act
        await _service.CheckAndAlertAsync(_tenantId, "ai-tokens", 80000, 100000);
        
        // Assert - both owners should receive notification
        var notifications = await _dbContext.NotificationQueues
            .IgnoreQueryFilters()
            .ToListAsync();
        
        notifications.Should().HaveCount(2);
        notifications.Should().Contain(n => n.RecipientEmail == "owner@test.com");
        notifications.Should().Contain(n => n.RecipientEmail == "owner2@test.com");
    }
}
