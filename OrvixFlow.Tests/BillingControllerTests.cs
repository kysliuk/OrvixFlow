using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using OrvixFlow.Infrastructure.Services.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using PlanService = OrvixFlow.Infrastructure.Services.PlanService;

namespace OrvixFlow.Tests;

public class BillingControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly BillingController _controller;
    private readonly Guid _tenantId;
    private readonly Mock<IStripeService> _stripeServiceMock;
    private readonly Guid _newPlanId;
    private readonly Guid _currentPlanId;

    public BillingControllerTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var tenantProvider = new MockTenantProvider(_tenantId);
        _dbContext = new AppDbContext(options, tenantProvider);

        _stripeServiceMock = new Mock<IStripeService>();
        var entitlementResolverMock = new Mock<IEntitlementResolver>();
        
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Stripe:WebhookSecret"]).Returns("whsec_mock");

        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);

        var stripeWebhook = new StripeWebhookService(
            _dbContext,
            subscriptionService,
            new Mock<ILogger<StripeWebhookService>>().Object,
            mockConfig.Object);

        _controller = new BillingController(
            _dbContext,
            entitlementResolverMock.Object,
            subscriptionService,
            planService,
            _stripeServiceMock.Object,
            stripeWebhook);

        _currentPlanId = Guid.NewGuid();
        _newPlanId = Guid.NewGuid();
        SeedTestData();
        SetupControllerContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void SeedTestData()
    {
        _dbContext.Tenants.Add(new Tenant 
        { 
            Id = _tenantId, 
            Name = "Test Tenant", 
            Plan = "Free" 
        });

        var currentPlan = new PlanTemplate
        {
            Id = _currentPlanId,
            Name = "Starter",
            Slug = "starter",
            MonthlyPriceCents = 2900,
            BillingInterval = BillingInterval.Monthly,
            IsActive = true
        };
        _dbContext.PlanTemplates.Add(currentPlan);

        var newPlan = new PlanTemplate
        {
            Id = _newPlanId,
            Name = "Pro",
            Slug = "pro",
            MonthlyPriceCents = 7900,
            BillingInterval = BillingInterval.Monthly,
            IsActive = true,
            StripeMonthlyPriceId = "price_pro_monthly"
        };
        _dbContext.PlanTemplates.Add(newPlan);

        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantId,
            PlanTemplateId = _currentPlanId,
            Status = SubscriptionState.Active,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(20)
        };
        _dbContext.CompanySubscriptions.Add(subscription);

        _dbContext.SaveChanges();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new("ActiveCompanyId", _tenantId.ToString()),
            new("Role", "CompanyOwner")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task CalculateProration_WhenNoExternalSub_ReturnsEstimate()
    {
        // Act - ExternalSubscriptionId and ExternalCustomerId are null by default in seeded sub
        var result = await _controller.CalculateProration(_newPlanId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;

        // Verify it returned estimate = true
        var isEstimateProp = value.GetType().GetProperty("isEstimate");
        isEstimateProp.Should().NotBeNull();
        ((bool)isEstimateProp!.GetValue(value)!).Should().BeTrue();

        var prorationAmountProp = value.GetType().GetProperty("prorationAmount");
        prorationAmountProp.Should().NotBeNull();
        ((int)prorationAmountProp!.GetValue(value)!).Should().Be(5000); // 7900 - 2900 = 5000
    }

    [Fact]
    public async Task CalculateProration_WhenStripeNotConfigured_ReturnsEstimate()
    {
        // Arrange - set external subscription ID so it tries Stripe, but make StripeService return null
        var sub = await _dbContext.CompanySubscriptions.FirstAsync(s => s.CompanyId == _tenantId);
        sub.ExternalSubscriptionId = "sub_test";
        sub.ExternalCustomerId = "cus_test";
        await _dbContext.SaveChangesAsync();

        _stripeServiceMock.Setup(s => s.GetProrationPreviewAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((ProrationPreview?)null);

        // Act
        var result = await _controller.CalculateProration(_newPlanId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;

        var isEstimateProp = value.GetType().GetProperty("isEstimate");
        ((bool)isEstimateProp!.GetValue(value)!).Should().BeTrue();
    }

    [Fact]
    public async Task CalculateProration_WhenSubscribedAndStripeConfigured_ReturnsRealPreview()
    {
        // Arrange
        var sub = await _dbContext.CompanySubscriptions.FirstAsync(s => s.CompanyId == _tenantId);
        sub.ExternalSubscriptionId = "sub_test";
        sub.ExternalCustomerId = "cus_test";
        await _dbContext.SaveChangesAsync();

        var expectedPreview = new ProrationPreview(4500, "USD", 15);
        _stripeServiceMock.Setup(s => s.GetProrationPreviewAsync(_tenantId, "price_pro_monthly"))
            .ReturnsAsync(expectedPreview);

        // Act
        var result = await _controller.CalculateProration(_newPlanId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;

        var isEstimateProp = value.GetType().GetProperty("isEstimate");
        isEstimateProp.Should().NotBeNull();
        ((bool)isEstimateProp!.GetValue(value)!).Should().BeFalse();

        var prorationAmountProp = value.GetType().GetProperty("prorationAmount");
        prorationAmountProp.Should().NotBeNull();
        ((long)prorationAmountProp!.GetValue(value)!).Should().Be(4500);

        var currencyProp = value.GetType().GetProperty("currency");
        currencyProp.Should().NotBeNull();
        ((string)currencyProp!.GetValue(value)!).Should().Be("USD");

        var daysRemainingProp = value.GetType().GetProperty("daysRemaining");
        daysRemainingProp.Should().NotBeNull();
        ((int)daysRemainingProp!.GetValue(value)!).Should().Be(15);
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
