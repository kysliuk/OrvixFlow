using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using OrvixFlow.Infrastructure.Services.Stripe;
using Xunit;

namespace OrvixFlow.Tests;

public class StripeServiceTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;

    public StripeServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ReactivateSubscriptionAsync_WhenNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Stripe:SecretKey"]).Returns((string?)null);
        var mockLogger = new Mock<ILogger<StripeService>>();
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);

        var service = new StripeService(
            mockConfig.Object,
            mockLogger.Object,
            _dbContext,
            subscriptionService,
            planService);

        // Act
        var act = async () => await service.ReactivateSubscriptionAsync("sub_123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Stripe is not configured.");
    }

    [Fact]
    public async Task GetSubscriptionDetailsAsync_WhenNotConfigured_ReturnsNull()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Stripe:SecretKey"]).Returns((string?)null);
        var mockLogger = new Mock<ILogger<StripeService>>();
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);

        var service = new StripeService(
            mockConfig.Object,
            mockLogger.Object,
            _dbContext,
            subscriptionService,
            planService);

        // Act
        var result = await service.GetSubscriptionDetailsAsync("sub_123");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProrationPreviewAsync_WhenNotConfigured_ReturnsNull()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Stripe:SecretKey"]).Returns((string?)null);
        var mockLogger = new Mock<ILogger<StripeService>>();
        var planService = new PlanService(_dbContext);
        var subscriptionService = new CompanySubscriptionService(_dbContext, planService);

        var service = new StripeService(
            mockConfig.Object,
            mockLogger.Object,
            _dbContext,
            subscriptionService,
            planService);

        // Act
        var result = await service.GetProrationPreviewAsync(_tenantId, "price_123");

        // Assert
        result.Should().BeNull();
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
