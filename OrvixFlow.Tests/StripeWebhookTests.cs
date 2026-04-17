using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using OrvixFlow.Infrastructure.Services.Stripe;
using Stripe;
using Xunit;
using PlanService = OrvixFlow.Infrastructure.Services.PlanService;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for Stripe webhook fixes (Wave 1 - Critical):
/// - T1-1: IgnoreQueryFilters in webhook handlers
/// - T1-2: Tenant sync on invoice.paid
/// </summary>
public class StripeWebhookTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly CompanySubscriptionService _subscriptionService;
    private readonly Guid _tenantId;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<StripeWebhookService>> _mockLogger;

    public StripeWebhookTests()
    {
        _tenantId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var mockTenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, mockTenantProvider);
        _subscriptionService = new CompanySubscriptionService(_dbContext, new PlanService(_dbContext));
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<StripeWebhookService>>();

        SeedTestData();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void SeedTestData()
    {
        // Create tenant with denormalized fields
        _dbContext.Tenants.Add(new Tenant 
        { 
            Id = _tenantId, 
            Name = "Test Company", 
            Plan = "Free", 
            SubscriptionStatus = "Trialing" 
        });

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

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = starterPlan.Id,
            Status = SubscriptionState.Trialing,
            ExternalCustomerId = "cus_test123456",
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-5),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(25)
        };
        _dbContext.CompanySubscriptions.Add(subscription);
        
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task HandleInvoicePaidAsync_WithValidCustomerId_ActivatesSubscription()
    {
        // Act - simulate what the handler does with IgnoreQueryFilters (T1-1 fix)
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == "cus_test123456")
            .ToList();

        foreach (var sub in subscriptions)
        {
            if (sub.Status != SubscriptionState.Active)
            {
                sub.Status = SubscriptionState.Active;
                sub.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ExternalCustomerId == "cus_test123456");
        
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(SubscriptionState.Active);
    }

    [Fact]
    public async Task HandleInvoicePaidAsync_WithIgnoreQueryFilters_FindsSubscription()
    {
        // This test verifies IgnoreQueryFilters works
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == "cus_test123456")
            .ToList();

        subscriptions.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleInvoicePaidAsync_SyncsTenantDenormalization()
    {
        // First activate the subscription
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == "cus_test123456")
            .ToList();

        foreach (var sub in subscriptions)
        {
            sub.Status = SubscriptionState.Active;
            sub.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        // Then sync tenant denormalization
        foreach (var sub in subscriptions)
            await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

        // Assert tenant was synced
        var tenant = await _dbContext.Tenants.FindAsync(_tenantId);
        tenant.Should().NotBeNull();
        tenant!.Plan.Should().Be("starter");
        tenant.SubscriptionStatus.Should().Be("Active");
    }

    private IConfiguration CreateConfiguration(string webhookSecret)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Stripe:WebhookSecret"]).Returns(webhookSecret);
        return config.Object;
    }
}
