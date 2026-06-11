using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class UsagePeriodRolloverJobTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ILogger<UsagePeriodRolloverJob>> _loggerMock;
    private readonly Guid _tenantId;

    public UsagePeriodRolloverJobTests()
    {
        _tenantId = Guid.NewGuid();
        _auditServiceMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<UsagePeriodRolloverJob>>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Using a mock tenant provider for the db context setup
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPeriodExpired_AdvancesToNextPeriod()
    {
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "Test Company",
            Plan = "starter",
            SubscriptionStatus = "Active"
        };
        _dbContext.Tenants.Add(tenant);

        var yesterday = DateTime.UtcNow.AddDays(-1);
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = yesterday.AddDays(-30),
            CurrentPeriodEnd = yesterday
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var job = new UsagePeriodRolloverJob(_dbContext, _auditServiceMock.Object, _loggerMock.Object);

        // Act
        await job.ExecuteAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        updated.Should().NotBeNull();
        updated!.CurrentPeriodStart.Should().Be(yesterday);
        updated.CurrentPeriodEnd.Should().Be(yesterday.AddDays(30));
        
        _auditServiceMock.Verify(x => x.RecordAsync(
            _tenantId,
            "PeriodRolledOver",
            It.Is<string>(s => s.Contains("Billing period advanced")),
            It.IsAny<Guid?>()
        ), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPeriodNotExpired_DoesNotAdvance()
    {
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "Test Company",
            Plan = "starter",
            SubscriptionStatus = "Active"
        };
        _dbContext.Tenants.Add(tenant);

        var tomorrow = DateTime.UtcNow.AddDays(1);
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = tomorrow.AddDays(-30),
            CurrentPeriodEnd = tomorrow
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var job = new UsagePeriodRolloverJob(_dbContext, _auditServiceMock.Object, _loggerMock.Object);

        // Act
        await job.ExecuteAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        updated.Should().NotBeNull();
        updated!.CurrentPeriodStart.Should().Be(tomorrow.AddDays(-30));
        updated.CurrentPeriodEnd.Should().Be(tomorrow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTrialingSubscription_DoesNotAdvance()
    {
        // Trialing subscriptions should not be processed by this job (handled by TrialExpirationJob)
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        var tenant = new Tenant
        {
            Id = _tenantId,
            Name = "Test Company",
            Plan = "starter",
            SubscriptionStatus = "Trialing"
        };
        _dbContext.Tenants.Add(tenant);

        var yesterday = DateTime.UtcNow.AddDays(-1);
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Trialing,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = yesterday.AddDays(-30),
            CurrentPeriodEnd = yesterday
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        var job = new UsagePeriodRolloverJob(_dbContext, _auditServiceMock.Object, _loggerMock.Object);

        // Act
        await job.ExecuteAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscription.Id);

        updated.Should().NotBeNull();
        updated!.CurrentPeriodStart.Should().Be(yesterday.AddDays(-30));
        updated.CurrentPeriodEnd.Should().Be(yesterday);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAffectOtherCompanies()
    {
        var plan = new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            Slug = "starter",
            IsFree = false,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(plan);

        // Company A - Expired
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var subA = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = yesterday.AddDays(-30),
            CurrentPeriodEnd = yesterday
        };
        _dbContext.CompanySubscriptions.Add(subA);

        // Company B - Unexpired
        var companyBId = Guid.NewGuid();
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var subB = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyBId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active,
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = tomorrow.AddDays(-30),
            CurrentPeriodEnd = tomorrow
        };
        _dbContext.CompanySubscriptions.Add(subB);
        await _dbContext.SaveChangesAsync();

        var job = new UsagePeriodRolloverJob(_dbContext, _auditServiceMock.Object, _loggerMock.Object);

        // Act
        await job.ExecuteAsync();

        // Assert
        var updatedA = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subA.Id);
        updatedA!.CurrentPeriodStart.Should().Be(yesterday);

        var updatedB = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subB.Id);
        updatedB!.CurrentPeriodStart.Should().Be(tomorrow.AddDays(-30));
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
