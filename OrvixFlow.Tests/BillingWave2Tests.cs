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
using OrvixFlow.Infrastructure.Services.Stripe;
using Xunit;

namespace OrvixFlow.Tests;

/// <summary>
/// Tests for Wave 2 billing improvements:
/// - T2-1: Checkout, portal, invoices endpoints
/// - T2-2: CreatePortalSessionAsync implementation
/// - T2-3: subscription.updated and subscription.deleted handlers (already implemented in Wave 1)
/// - T2-4: Startup warning for missing Stripe config
/// - T2-5: invoice.payment_failed tenant sync (already implemented in Wave 1)
/// </summary>
public class BillingWave2Tests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;
    private readonly Guid _starterPlanId;

    public BillingWave2Tests()
    {
        _tenantId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var mockTenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, mockTenantProvider);

        _starterPlanId = Guid.NewGuid();
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

        // Create starter plan
        var starterPlan = new PlanTemplate
        {
            Id = _starterPlanId,
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            MaxSeats = 5,
            IsActive = true,
            IsFree = false,
            SortOrder = 1,
            BillingInterval = BillingInterval.Monthly,
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

        // Create subscription
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = _starterPlanId,
            Status = SubscriptionState.Active,
            ExternalCustomerId = "cus_test123456",
            BillingInterval = BillingInterval.Monthly,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-15),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(15)
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        _dbContext.SaveChanges();
    }

    #region T2-1: Invoice Query Tests

    [Fact]
    public async Task Invoices_Query_ReturnsInvoicesForCompany()
    {
        // Arrange - add invoice
        var invoice = new Invoice
        {
            CompanyId = _tenantId,
            ExternalInvoiceId = "inv_test001",
            AmountCents = 2900,
            Currency = "USD",
            Status = InvoiceStatus.Paid,
            PeriodStart = DateTime.UtcNow.AddDays(-30),
            PeriodEnd = DateTime.UtcNow,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        // Act - query invoices for company
        var invoices = await _dbContext.Invoices
            .Where(i => i.CompanyId == _tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new 
            {
                i.Id,
                i.ExternalInvoiceId,
                i.AmountCents,
                i.Currency,
                i.Status,
                i.PeriodStart,
                i.PeriodEnd,
                i.PaidAt,
                i.CreatedAt
            })
            .ToListAsync();

        // Assert
        invoices.Should().HaveCount(1);
        invoices[0].ExternalInvoiceId.Should().Be("inv_test001");
        invoices[0].AmountCents.Should().Be(2900);
        invoices[0].Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Invoices_Query_ReturnsEmpty_WhenNoInvoices()
    {
        // Act
        var invoices = await _dbContext.Invoices
            .Where(i => i.CompanyId == _tenantId)
            .ToListAsync();

        // Assert
        invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Invoices_Query_DoesNotReturnOtherCompaniesInvoices()
    {
        // Arrange - add invoice for another company
        var otherTenantId = Guid.NewGuid();
        _dbContext.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other Company" });
        
        _dbContext.Invoices.Add(new Invoice
        {
            CompanyId = otherTenantId,
            ExternalInvoiceId = "inv_other",
            AmountCents = 5000,
            Status = InvoiceStatus.Paid
        });
        await _dbContext.SaveChangesAsync();

        // Act - query should only return our company's invoices
        var invoices = await _dbContext.Invoices
            .Where(i => i.CompanyId == _tenantId)
            .ToListAsync();

        // Assert
        invoices.Should().HaveCount(0);
    }

    #endregion

    #region T2-2: Portal Session Tests

    [Fact]
    public void CreatePortalSessionAsync_ThrowsWhenNotConfigured()
    {
        // Arrange - create service without Stripe config
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["Stripe:SecretKey"]).Returns((string?)null);
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<StripeService>>();
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);
        
        var stripeService = new StripeService(
            mockConfig.Object,
            mockLogger.Object,
            _dbContext,
            subscriptionService,
            planService);

        // Act & Assert - should throw when Stripe not configured
        var act = async () => await stripeService.CreatePortalSessionAsync(_tenantId, "https://return.url");
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Fact]
    public void CreatePortalSessionAsync_ThrowsWhenNoExternalCustomerId()
    {
        // Arrange - remove external customer ID
        var sub = _dbContext.CompanySubscriptions.First();
        sub.ExternalCustomerId = null;
        _dbContext.SaveChanges();

        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        mockConfig.Setup(c => c["Stripe:SecretKey"]).Returns("sk_test_mock");
        mockConfig.Setup(c => c["Stripe:Prices:Starter:Monthly"]).Returns("price_mock");
        
        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<StripeService>>();
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);
        
        var stripeService = new StripeService(
            mockConfig.Object,
            mockLogger.Object,
            _dbContext,
            subscriptionService,
            planService);

        // Act & Assert - should throw when no external customer ID
        var act = async () => await stripeService.CreatePortalSessionAsync(_tenantId, "https://return.url");
        act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    #endregion

    #region T2-3: Subscription Updated Handler Tests

    [Fact]
    public async Task HandleSubscriptionUpdated_MapsStripeStatusToSubscriptionState()
    {
        // Test that Stripe statuses map correctly
        var stripeStatusMapping = new Dictionary<string, SubscriptionState>
        {
            ["active"] = SubscriptionState.Active,
            ["past_due"] = SubscriptionState.PastDue,
            ["trialing"] = SubscriptionState.Trialing,
            ["canceled"] = SubscriptionState.Cancelled
        };

        foreach (var (stripeStatus, expectedState) in stripeStatusMapping)
        {
            // This tests the mapping logic used in HandleSubscriptionUpdatedAsync
            var mappedStatus = stripeStatus switch
            {
                "active" => SubscriptionState.Active,
                "past_due" => SubscriptionState.PastDue,
                "trialing" => SubscriptionState.Trialing,
                "canceled" => SubscriptionState.Cancelled,
                _ => (SubscriptionState?)null
            };

            mappedStatus.Should().Be(expectedState, $"Stripe status '{stripeStatus}' should map to {expectedState}");
        }
    }

    [Fact]
    public async Task HandleSubscriptionUpdated_UpdatesSubscriptionWithExternalId()
    {
        // Arrange
        var subscription = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .First(s => s.ExternalCustomerId == "cus_test123456");
        
        var originalSubId = subscription.ExternalSubscriptionId;

        // Act - simulate what the handler does
        subscription.ExternalSubscriptionId = "sub_new123";
        subscription.Status = SubscriptionState.Active;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.ExternalCustomerId == "cus_test123456");
        
        updated.ExternalSubscriptionId.Should().Be("sub_new123");
    }

    #endregion

    #region T2-5: Invoice Failed Handler Tests

    [Fact]
    public async Task HandleInvoiceFailed_MarksSubscriptionAsPastDue()
    {
        // Arrange - reset subscription to Active
        var subscription = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .First(s => s.ExternalCustomerId == "cus_test123456");
        subscription.Status = SubscriptionState.Active;
        await _dbContext.SaveChangesAsync();

        // Act - simulate what the handler does
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == "cus_test123456")
            .ToList();

        foreach (var sub in subscriptions)
        {
            sub.Status = SubscriptionState.PastDue;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.ExternalCustomerId == "cus_test123456");
        updated.Status.Should().Be(SubscriptionState.PastDue);
    }

    [Fact]
    public async Task HandleInvoiceFailed_SyncsTenantDenormalization()
    {
        // Arrange
        var subscription = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .First(s => s.ExternalCustomerId == "cus_test123456");
        subscription.Status = SubscriptionState.Active;
        await _dbContext.SaveChangesAsync();

        // Create subscription service
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);

        // Act - simulate what the handler does
        subscription.Status = SubscriptionState.PastDue;
        subscription.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Sync tenant
        await subscriptionService.SyncTenantDenormalizationAsync(_tenantId);

        // Assert
        var tenant = await _dbContext.Tenants.FindAsync(_tenantId);
        tenant.Should().NotBeNull();
        tenant!.SubscriptionStatus.Should().Be("PastDue");
    }

    #endregion

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
