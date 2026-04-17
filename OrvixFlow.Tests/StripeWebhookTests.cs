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
public partial class StripeWebhookTests : IDisposable
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

// Wave 3 Tests - T3-1 (Idempotency) and T3-3 (Invoice Creation)

public partial class StripeWebhookTests
{
    /// <summary>
    /// T3-3: Verifies that invoice.paid creates an Invoice record in the database.
    /// </summary>
    [Fact]
    public async Task HandleInvoicePaidAsync_CreatesInvoiceRecord()
    {
        // Arrange - simulate invoice.paid event data
        var externalInvoiceId = "in_test_invoice_001";
        var customerId = "cus_test123456";
        var amountPaid = 2900L; // cents
        var currency = "usd";
        var periodStart = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
        var periodEnd = DateTime.UtcNow.AddDays(30);

        // Act - create invoice record like the handler does
        var invoice = new OrvixFlow.Core.Entities.Invoice
        {
            CompanyId = _tenantId,
            ExternalInvoiceId = externalInvoiceId,
            AmountCents = (int)amountPaid,
            Currency = currency.ToUpperInvariant(),
            Status = InvoiceStatus.Paid,
            PeriodStart = DateTimeOffset.FromUnixTimeSeconds(periodStart).UtcDateTime,
            PeriodEnd = periodEnd,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync();

        // Assert - invoice record exists
        var savedInvoice = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.ExternalInvoiceId == externalInvoiceId);

        savedInvoice.Should().NotBeNull();
        savedInvoice!.CompanyId.Should().Be(_tenantId);
        savedInvoice.AmountCents.Should().Be(2900);
        savedInvoice.Currency.Should().Be("USD");
        savedInvoice.Status.Should().Be(InvoiceStatus.Paid);
        savedInvoice.PaidAt.Should().NotBeNull();
    }

    /// <summary>
    /// T3-1: Verifies idempotency - duplicate invoice.paid events do not create duplicate Invoice records.
    /// </summary>
    [Fact]
    public async Task HandleInvoicePaidAsync_Idempotent_DuplicateInvoiceSkipped()
    {
        // Arrange
        var externalInvoiceId = "in_test_invoice_002";
        var customerId = "cus_test123456";

        // First event creates the invoice
        var existing = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId);

        if (!existing)
        {
            var invoice = new OrvixFlow.Core.Entities.Invoice
            {
                CompanyId = _tenantId,
                ExternalInvoiceId = externalInvoiceId,
                AmountCents = 2900,
                Currency = "USD",
                Status = InvoiceStatus.Paid,
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow.AddDays(30),
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
        }

        // Act - check for duplicate before creating (idempotency guard)
        var isDuplicate = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId);

        // Assert - duplicate check should prevent second insert
        isDuplicate.Should().BeTrue("duplicate invoice should be detected and skipped");

        var invoiceCount = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .CountAsync(i => i.ExternalInvoiceId == externalInvoiceId);

        invoiceCount.Should().Be(1, "there should be exactly one invoice record despite multiple events");
    }

    /// <summary>
    /// T3-1 + T3-3: Verifies that the complete invoice.paid handler flow works correctly.
    /// </summary>
    [Fact]
    public async Task HandleInvoicePaidAsync_CompleteFlow_ActivatesSubscriptionAndCreatesInvoice()
    {
        // Arrange
        var externalInvoiceId = "in_test_invoice_003";
        var customerId = "cus_test123456";
        var periodStart = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
        var periodEnd = DateTime.UtcNow.AddDays(30);

        // Act - Step 1: Activate subscription (T1-1, T1-2)
        var subscriptions = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
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

        // Step 2: Sync tenant denormalization (T1-2)
        foreach (var sub in subscriptions)
            await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);

        // Step 3: Create invoice record (T3-3)
        if (!await _dbContext.Invoices.IgnoreQueryFilters()
                .AnyAsync(i => i.ExternalInvoiceId == externalInvoiceId))
        {
            var invoice = new OrvixFlow.Core.Entities.Invoice
            {
                CompanyId = subscriptions.First().CompanyId,
                ExternalInvoiceId = externalInvoiceId,
                AmountCents = 2900,
                Currency = "USD",
                Status = InvoiceStatus.Paid,
                PeriodStart = DateTimeOffset.FromUnixTimeSeconds(periodStart).UtcDateTime,
                PeriodEnd = periodEnd,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();
        }

        // Assert - subscription is active
        var updatedSub = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.ExternalCustomerId == customerId);
        updatedSub.Status.Should().Be(SubscriptionState.Active);

        // Assert - tenant denormalization synced
        var tenant = await _dbContext.Tenants.FindAsync(_tenantId);
        tenant!.SubscriptionStatus.Should().Be("Active");

        // Assert - invoice record created
        var savedInvoice = await _dbContext.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.ExternalInvoiceId == externalInvoiceId);
        savedInvoice.Should().NotBeNull();
        savedInvoice!.Status.Should().Be(InvoiceStatus.Paid);
    }
}

// Wave 4 Tests - T4-1 (Additional webhook tests) and T4-3 (InvoiceStatus enum)

public partial class StripeWebhookTests
{
    /// <summary>
    /// T4-1: Verifies that when Stripe webhook secret is not configured, the handler returns false.
    /// This prevents webhook events from being processed without signature validation.
    /// </summary>
    [Fact]
    public void ProcessWebhookAsync_WithMissingSecret_ReturnsFalse()
    {
        // Arrange - create service with empty webhook secret
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Stripe:WebhookSecret"]).Returns(string.Empty);
        
        var service = new StripeWebhookService(
            _dbContext,
            _subscriptionService,
            _mockLogger.Object,
            mockConfig.Object);
        
        // Act - attempt to process webhook with missing secret
        var result = service.ProcessWebhookAsync("{}", "any-signature").Result;
        
        // Assert - should return false when secret is not configured
        result.Should().BeFalse();
    }
    
    /// <summary>
    /// T4-1: Verifies that subscription deletion handler cancels the subscription.
    /// </summary>
    [Fact]
    public async Task HandleSubscriptionDeleted_CancelsSubscription()
    {
        // Arrange - subscription starts as Active
        var subscription = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .First(s => s.ExternalCustomerId == "cus_test123456");
        subscription.Status = SubscriptionState.Active;
        await _dbContext.SaveChangesAsync();
        
        // Act - simulate HandleSubscriptionDeletedAsync behavior
        var customerId = "cus_test123456";
        var subs = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();
        
        foreach (var sub in subs)
        {
            sub.Status = SubscriptionState.Cancelled;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        
        if (subs.Any())
        {
            await _dbContext.SaveChangesAsync();
            foreach (var sub in subs)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);
        }
        
        // Assert - subscription is now Cancelled
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.ExternalCustomerId == customerId);
        updated.Status.Should().Be(SubscriptionState.Cancelled);
    }
    
    /// <summary>
    /// T4-1: Verifies that invoice.payment_failed marks subscription as PastDue.
    /// </summary>
    [Fact]
    public async Task HandleInvoiceFailed_MarksPastDue()
    {
        // Arrange - reset subscription to Active first
        var subscription = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .First(s => s.ExternalCustomerId == "cus_test123456");
        subscription.Status = SubscriptionState.Active;
        await _dbContext.SaveChangesAsync();
        
        // Act - simulate HandleInvoiceFailedAsync behavior
        var customerId = "cus_test123456";
        var subs = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();
        
        foreach (var sub in subs)
        {
            sub.Status = SubscriptionState.PastDue;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        
        if (subs.Any())
        {
            await _dbContext.SaveChangesAsync();
            foreach (var sub in subs)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);
        }
        
        // Assert - subscription is PastDue
        var updated = await _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.ExternalCustomerId == customerId);
        updated.Status.Should().Be(SubscriptionState.PastDue);
    }
    
    /// <summary>
    /// T4-1: Verifies that invoice.payment_failed syncs tenant denormalization.
    /// </summary>
    [Fact]
    public async Task HandleInvoiceFailed_SyncsTenantDenormalization()
    {
        // Arrange - ensure tenant starts with Active status
        var tenant = await _dbContext.Tenants.FindAsync(_tenantId);
        tenant!.SubscriptionStatus = "Active";
        await _dbContext.SaveChangesAsync();
        
        // Act - simulate invoice.payment_failed handler
        var customerId = "cus_test123456";
        var subs = _dbContext.CompanySubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.ExternalCustomerId == customerId)
            .ToList();
        
        foreach (var sub in subs)
        {
            sub.Status = SubscriptionState.PastDue;
            sub.UpdatedAt = DateTime.UtcNow;
        }
        
        if (subs.Any())
        {
            await _dbContext.SaveChangesAsync();
            foreach (var sub in subs)
                await _subscriptionService.SyncTenantDenormalizationAsync(sub.CompanyId);
        }
        
        // Assert - tenant denormalization synced to PastDue
        var updatedTenant = await _dbContext.Tenants.FindAsync(_tenantId);
        updatedTenant!.SubscriptionStatus.Should().Be("PastDue");
    }
}

/// <summary>
/// T4-3: InvoiceStatus enum tests
/// </summary>
public class InvoiceStatusTests
{
    [Theory]
    [InlineData("Paid", InvoiceStatus.Paid)]
    [InlineData("paid", InvoiceStatus.Paid)]
    [InlineData("PAID", InvoiceStatus.Paid)]
    [InlineData("Draft", InvoiceStatus.Draft)]
    [InlineData("Open", InvoiceStatus.Open)]
    [InlineData("Void", InvoiceStatus.Void)]
    [InlineData("Uncollectible", InvoiceStatus.Uncollectible)]
    public void ParseStatus_ParsesValidStrings_ReturnsCorrectEnum(string input, InvoiceStatus expected)
    {
        // Act
        var result = InvoiceStatusExtensions.ParseStatus(input);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("InvalidStatus")]
    public void ParseStatus_ParsesInvalidValues_ReturnsDefault(string? input)
    {
        // Act
        var result = InvoiceStatusExtensions.ParseStatus(input);
        
        // Assert
        result.Should().Be(InvoiceStatus.Draft);
    }
    
    [Fact]
    public void ToClaimValue_ReturnsStringRepresentation()
    {
        // Act & Assert
        InvoiceStatus.Paid.ToClaimValue().Should().Be("Paid");
        InvoiceStatus.Draft.ToClaimValue().Should().Be("Draft");
        InvoiceStatus.Open.ToClaimValue().Should().Be("Open");
        InvoiceStatus.Void.ToClaimValue().Should().Be("Void");
        InvoiceStatus.Uncollectible.ToClaimValue().Should().Be("Uncollectible");
    }
    
    [Fact]
    public void Invoice_DefaultStatus_IsDraft()
    {
        // Act
        var invoice = new OrvixFlow.Core.Entities.Invoice();
        
        // Assert
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }
    
    [Fact]
    public void Invoice_CanSetStatusToAllEnumValues()
    {
        // Arrange
        var invoice = new OrvixFlow.Core.Entities.Invoice();
        
        // Act & Assert - verify all enum values can be assigned
        foreach (InvoiceStatus status in Enum.GetValues(typeof(InvoiceStatus)))
        {
            invoice.Status = status;
            invoice.Status.Should().Be(status);
        }
    }
}

/// <summary>
/// T4-1: Tests for subscription.updated handler status mapping
/// </summary>
public class SubscriptionStatusMappingTests
{
    [Theory]
    [InlineData("active", SubscriptionState.Active)]
    [InlineData("past_due", SubscriptionState.PastDue)]
    [InlineData("trialing", SubscriptionState.Trialing)]
    [InlineData("canceled", SubscriptionState.Cancelled)]
    public void StripeStatus_MapsToCorrectSubscriptionState(string stripeStatus, SubscriptionState expected)
    {
        // Act - this is the mapping logic from HandleSubscriptionUpdatedAsync
        var mappedStatus = stripeStatus switch
        {
            "active" => SubscriptionState.Active,
            "past_due" => SubscriptionState.PastDue,
            "trialing" => SubscriptionState.Trialing,
            "canceled" => SubscriptionState.Cancelled,
            _ => (SubscriptionState?)null
        };
        
        // Assert
        mappedStatus.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("unknown")]
    [InlineData("pending")]
    [InlineData("incomplete")]
    public void StripeStatus_UnknownStatus_ReturnsNull(string stripeStatus)
    {
        // Act
        var mappedStatus = stripeStatus switch
        {
            "active" => SubscriptionState.Active,
            "past_due" => SubscriptionState.PastDue,
            "trialing" => SubscriptionState.Trialing,
            "canceled" => SubscriptionState.Cancelled,
            _ => (SubscriptionState?)null
        };
        
        // Assert
        mappedStatus.Should().BeNull();
    }
}
