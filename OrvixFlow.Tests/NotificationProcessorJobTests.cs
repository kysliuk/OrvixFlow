using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Tests;

public class NotificationProcessorJobTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly NotificationProcessorJob _job;

    public NotificationProcessorJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options, new BackgroundTestTenantProvider());
        _emailServiceMock = new Mock<IEmailService>();
        _job = new NotificationProcessorJob(_db, _emailServiceMock.Object, Mock.Of<ILogger<NotificationProcessorJob>>());
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesQueuedAuthEmail_WithoutRequestTenantContext()
    {
        var companyId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant A", Plan = "Free", SubscriptionStatus = "Active" });
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = "verify@example.com",
            Subject = "Verify your OrvixFlow account",
            Body = "<p>Verify</p>",
            Processed = false
        });
        await _db.SaveChangesAsync();

        await _job.ExecuteAsync();

        _emailServiceMock.Verify(x => x.SendEmailAsync("verify@example.com", "Verify your OrvixFlow account", "<p>Verify</p>"), Times.Once);

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeTrue();
        notification.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesUsageAlert_WithoutRequestTenantContext()
    {
        var companyId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant B", Plan = "Starter", SubscriptionStatus = "Active" });
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "UsageWarning80",
            Channel = "Email",
            RecipientEmail = "owner@example.com",
            MetricType = "ai-tokens",
            CurrentUsage = 80000,
            Limit = 100000,
            Percentage = 80,
            Processed = false
        });
        await _db.SaveChangesAsync();

        await _job.ExecuteAsync();

        _emailServiceMock.Verify(
            x => x.SendEmailAsync(
                "owner@example.com",
                "⚠️ Usage Alert: 80% Threshold Reached",
                It.Is<string>(body => body.Contains("AI Tokens") && body.Contains("80%"))),
            Times.Once);

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSendFails_LeavesNotificationUnprocessed()
    {
        var companyId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant C", Plan = "Free", SubscriptionStatus = "Active" });
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = "fail@example.com",
            Subject = "Verify",
            Body = "<p>Verify</p>",
            Processed = false
        });
        await _db.SaveChangesAsync();

        _emailServiceMock
            .Setup(x => x.SendEmailAsync("fail@example.com", "Verify", "<p>Verify</p>"))
            .ThrowsAsync(new InvalidOperationException("smtp failed"));

        await _job.ExecuteAsync();

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeFalse();
        notification.ProcessedAt.Should().BeNull();
        notification.AttemptCount.Should().Be(1);
        notification.LastAttemptedAt.Should().NotBeNull();
        notification.LastError.Should().Be("smtp failed");
        notification.Failed.Should().BeFalse();
        notification.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_AfterThreeFailures_MarksNotificationFailed()
    {
        var companyId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant D", Plan = "Free", SubscriptionStatus = "Active" });
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = "retry@example.com",
            Subject = "Verify",
            Body = "<p>Verify</p>",
            Processed = false
        });
        await _db.SaveChangesAsync();

        _emailServiceMock
            .Setup(x => x.SendEmailAsync("retry@example.com", "Verify", "<p>Verify</p>"))
            .ThrowsAsync(new InvalidOperationException("transient smtp failure"));

        await _job.ExecuteAsync();
        await _job.ExecuteAsync();
        await _job.ExecuteAsync();

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeFalse();
        notification.AttemptCount.Should().Be(3);
        notification.Failed.Should().BeTrue();
        notification.LastError.Should().Be("transient smtp failure");
        notification.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_RecoversStaleProcessingClaim()
    {
        var companyId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant E", Plan = "Free", SubscriptionStatus = "Active" });
        _db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = "stale@example.com",
            Subject = "Verify",
            Body = "<p>Verify</p>",
            IsProcessing = true,
            ProcessingStartedAt = DateTime.UtcNow.AddMinutes(-30),
            Processed = false
        });
        await _db.SaveChangesAsync();

        await _job.ExecuteAsync();

        _emailServiceMock.Verify(x => x.SendEmailAsync("stale@example.com", "Verify", "<p>Verify</p>"), Times.Once);

        var notification = await _db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeTrue();
        notification.IsProcessing.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFailsAfterDelivery_LeavesClaimInPlaceToAvoidImmediateResend()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ThrowOnSecondSaveDbContext(options, new BackgroundTestTenantProvider());
        var emailServiceMock = new Mock<IEmailService>();
        var job = new NotificationProcessorJob(db, emailServiceMock.Object, Mock.Of<ILogger<NotificationProcessorJob>>());

        var companyId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = companyId, Name = "Tenant F", Plan = "Free", SubscriptionStatus = "Active" });
        db.NotificationQueues.Add(new NotificationQueue
        {
            CompanyId = companyId,
            Type = "AuthEmail",
            Channel = "Email",
            RecipientEmail = "persist@example.com",
            Subject = "Verify",
            Body = "<p>Verify</p>",
            Processed = false
        });
        await db.SaveChangesAsync();
        db.ThrowOnSaveNumber = 3;

        await job.ExecuteAsync();

        emailServiceMock.Verify(x => x.SendEmailAsync("persist@example.com", "Verify", "<p>Verify</p>"), Times.Once);

        var notification = await db.NotificationQueues.IgnoreQueryFilters().SingleAsync();
        notification.Processed.Should().BeFalse();
        notification.IsProcessing.Should().BeTrue();
        notification.ProcessingStartedAt.Should().NotBeNull();
        notification.AttemptCount.Should().Be(1);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private sealed class BackgroundTestTenantProvider : ITenantProvider
    {
        public Guid GetTenantId() => Guid.Empty;
    }

    private sealed class ThrowOnSecondSaveDbContext : AppDbContext
    {
        private int _saveCount;

        public ThrowOnSecondSaveDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenantProvider)
            : base(options, tenantProvider)
        {
        }

        public int ThrowOnSaveNumber { get; set; } = -1;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount += 1;
            if (_saveCount == ThrowOnSaveNumber)
            {
                throw new DbUpdateException("Persistence failed after delivery");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
