using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class InboxProcessingIntegrationTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly Guid _tenantId;
    private readonly string _dbName;

    public InboxProcessingIntegrationTests()
    {
        _tenantId = Guid.NewGuid();
        _dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task InboxEvent_CanBeCreatedAndRetrieved()
    {
        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            MessageId = "test-msg-001",
            SenderEmail = "customer@example.com",
            SenderName = "Test Customer",
            Subject = "Test Subject",
            BodyText = "Test body content",
            WebhookCallbackPath = "test-webhook",
            ReceivedAtUtc = DateTime.UtcNow,
            Status = "Ingested"
        };

        _dbContext.InboxEvents.Add(inboxEvent);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        retrieved.Should().NotBeNull();
        retrieved!.MessageId.Should().Be("test-msg-001");
        retrieved.Status.Should().Be("Ingested");
    }

    [Fact]
    public async Task ActionRequest_CanBeCreatedWithInboxEvent()
    {
        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            MessageId = "test-msg-002",
            SenderEmail = "customer@example.com",
            Subject = "Test",
            BodyText = "Body",
            ReceivedAtUtc = DateTime.UtcNow,
            Status = "Action_Required"
        };
        _dbContext.InboxEvents.Add(inboxEvent);

        var actionRequest = new ActionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            InboxEventId = inboxEvent.Id,
            EvaluatedCategory = "Support",
            ConfidenceScore = 0.75m,
            DraftResponse = "Draft response",
            PolicyReason = "Below confidence threshold",
            Status = "Pending",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        _dbContext.ActionRequests.Add(actionRequest);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.ActionRequests
            .Include(a => a.InboxEvent)
            .FirstAsync(a => a.Id == actionRequest.Id);

        retrieved.Should().NotBeNull();
        retrieved.InboxEvent.Should().NotBeNull();
        retrieved.InboxEvent!.MessageId.Should().Be("test-msg-002");
    }

    [Fact]
    public async Task PolicyGateService_EvaluatesAgainstTenantPolicies()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var policyGateService = new PolicyGateService(_dbContext, cache);

        var policy = new WorkflowPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Category = "Support",
            AutoExecute = true,
            ConfidenceThreshold = 0.80m
        };
        _dbContext.WorkflowPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();

        var context = new PolicyEvaluationContext
        {
            SenderEmail = "customer@example.com",
            Subject = "Test",
            BodyText = "Body",
            Category = "Support",
            ConfidenceScore = 0.90m
        };

        var result = await policyGateService.EvaluateAsync(context, _tenantId);

        result.Decision.Should().Be(PolicyDecisionType.AutoExecute);
    }

    [Fact]
    public async Task DraftFeedbackService_CalculatesEditDistance()
    {
        var feedbackService = new DraftFeedbackService(_dbContext);

        var original = "Hello, thank you for contacting us.";
        var modified = "Hello, thank you for contacting us. We value your feedback.";

        var distance = await feedbackService.CalculateEditDistanceAsync(original, modified);

        distance.Should().BeGreaterThan(0);
        distance.Should().BeLessThan(1);
    }

    [Fact]
    public async Task FullPipeline_DbStateTransitions()
    {
        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            MessageId = "pipeline-test-001",
            SenderEmail = "test@example.com",
            Subject = "Pipeline Test",
            BodyText = "Test body",
            ReceivedAtUtc = DateTime.UtcNow,
            Status = "Ingested"
        };
        _dbContext.InboxEvents.Add(inboxEvent);
        await _dbContext.SaveChangesAsync();

        inboxEvent.Status = "Processing";
        await _dbContext.SaveChangesAsync();
        var processing = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        processing!.Status.Should().Be("Processing");

        inboxEvent.Status = "Action_Required";
        await _dbContext.SaveChangesAsync();
        var actionRequired = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        actionRequired!.Status.Should().Be("Action_Required");

        var actionRequest = new ActionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            InboxEventId = inboxEvent.Id,
            EvaluatedCategory = "Support",
            ConfidenceScore = 0.70m,
            DraftResponse = "Draft",
            PolicyReason = "Low confidence",
            Status = "Pending",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        _dbContext.ActionRequests.Add(actionRequest);
        await _dbContext.SaveChangesAsync();

        actionRequest.Status = "Approved";
        inboxEvent.Status = "Human_Approved";
        await _dbContext.SaveChangesAsync();

        var finalEvent = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        finalEvent!.Status.Should().Be("Human_Approved");

        var finalAction = await _dbContext.ActionRequests.FindAsync(actionRequest.Id);
        finalAction!.Status.Should().Be("Approved");
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
