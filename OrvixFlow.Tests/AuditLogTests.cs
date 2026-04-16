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

public class AuditLogTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly CompanySubscriptionService _subscriptionService;
    private readonly Guid _companyId;
    private readonly Guid _userId;

    public AuditLogTests()
    {
        _companyId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_companyId));
        _auditServiceMock = new Mock<IAuditService>();
        _auditServiceMock
            .Setup(x => x.RecordAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);

        var planServiceMock = new Mock<IPlanService>();
        _planServiceMock = planServiceMock;

        _subscriptionService = new CompanySubscriptionService(_dbContext, planServiceMock.Object, _auditServiceMock.Object);
    }

    private readonly Mock<IPlanService> _planServiceMock;

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AssignPlan_LogsPlanAssignedEvent()
    {
        var plan = CreatePlan("Growth", 25);
        _planServiceMock.Setup(x => x.GetPlanByIdAsync(plan.Id)).ReturnsAsync(plan);

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.AssignPlanAsync(_companyId, plan.Id, "Monthly");

        _auditServiceMock.Verify(
            x => x.RecordAsync(
                _companyId,
                "PlanAssigned",
                It.Is<string>(s => s.Contains("Growth")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task SuspendSubscription_LogsSuspensionEvent()
    {
        var plan = CreatePlan("Business", 100);
        _planServiceMock.Setup(x => x.GetPlanByIdAsync(plan.Id)).ReturnsAsync(plan);

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.SuspendSubscriptionAsync(_companyId);

        _auditServiceMock.Verify(
            x => x.RecordAsync(
                _companyId,
                "SubscriptionSuspended",
                It.Is<string>(s => s.Contains("Business")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task ReactivateSubscription_LogsReactivationEvent()
    {
        var plan = CreatePlan("Business", 100);
        _planServiceMock.Setup(x => x.GetPlanByIdAsync(plan.Id)).ReturnsAsync(plan);

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Suspended
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.ReactivateSubscriptionAsync(_companyId);

        _auditServiceMock.Verify(
            x => x.RecordAsync(
                _companyId,
                "SubscriptionReactivated",
                It.Is<string>(s => s.Contains("Business")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task CancelSubscription_LogsCancellationEvent()
    {
        var plan = CreatePlan("Starter", 5);
        _planServiceMock.Setup(x => x.GetPlanByIdAsync(plan.Id)).ReturnsAsync(plan);

        _dbContext.PlanTemplates.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = plan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.CancelSubscriptionAsync(_companyId);

        _auditServiceMock.Verify(
            x => x.RecordAsync(
                _companyId,
                "SubscriptionCancelled",
                It.Is<string>(s => s.Contains("Starter")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task ChangePlan_LogsPlanChangedEvent()
    {
        var oldPlan = CreatePlan("Starter", 5);
        var newPlan = CreatePlan("Growth", 25);

        _planServiceMock.Setup(x => x.GetPlanByIdAsync(oldPlan.Id)).ReturnsAsync(oldPlan);
        _planServiceMock.Setup(x => x.GetPlanByIdAsync(newPlan.Id)).ReturnsAsync(newPlan);

        _dbContext.PlanTemplates.AddRange(oldPlan, newPlan);
        await _dbContext.SaveChangesAsync();

        var subscription = new CompanySubscription
        {
            CompanyId = _companyId,
            PlanTemplateId = oldPlan.Id,
            Status = SubscriptionState.Active
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();

        await _subscriptionService.ChangePlanAsync(_companyId, newPlan.Id, immediate: true);

        _auditServiceMock.Verify(
            x => x.RecordAsync(
                _companyId,
                "PlanChanged",
                It.Is<string>(s => s.Contains("Starter") && s.Contains("Growth")),
                null),
            Times.Once);
    }

    private PlanTemplate CreatePlan(string name, int maxSeats)
    {
        return new PlanTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = name.ToLowerInvariant(),
            MaxSeats = maxSeats,
            IsActive = true,
            Entitlements = new PlanEntitlements
            {
                MaxMonthlyTokens = 100000,
                MaxApiRequestsPerDay = 1000,
                MaxStorageMb = 500,
                MaxKnowledgeBases = 5
            }
        };
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
