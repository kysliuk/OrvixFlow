using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using Xunit;

namespace OrvixFlow.Tests;

public class ActionsControllerTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ActionsController _controller;
    private readonly Mock<IWebhookCallbackService> _callbackServiceMock;
    private readonly Guid _tenantId;

    public ActionsControllerTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options, new MockTenantProvider(_tenantId));
        
        _callbackServiceMock = new Mock<IWebhookCallbackService>();
        var loggerMock = new Mock<ILogger<ActionsController>>();
        var tenantProvider = new MockTenantProvider(_tenantId);

        _controller = new ActionsController(
            _dbContext,
            tenantProvider,
            _callbackServiceMock.Object,
            loggerMock.Object);

        SetupControllerContext();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetAction_ValidId_Returns200()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id);

        var result = await _controller.GetAction(action.Id);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<ActionsController.ActionRequestResponse>().Subject;
        response.Id.Should().Be(action.Id);
        response.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task GetAction_InvalidId_Returns404()
    {
        var result = await _controller.GetAction(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetPendingActions_ReturnsOnlyPendingNonExpired()
    {
        var inboxEvent = await CreateInboxEvent();
        await CreateActionRequest(inboxEvent.Id, status: "Pending", expiresAt: DateTime.UtcNow.AddDays(7));
        await CreateActionRequest(inboxEvent.Id, status: "Approved", expiresAt: DateTime.UtcNow.AddDays(7));
        await CreateActionRequest(inboxEvent.Id, status: "Pending", expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.GetPendingActions();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var actions = okResult.Value.Should().BeAssignableTo<List<ActionsController.ActionRequestResponse>>().Subject;
        actions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ResolveAction_Approve_SetsApprovedStatus()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id);

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<dynamic>().Subject;
        
        var updatedAction = await _dbContext.ActionRequests.FindAsync(action.Id);
        updatedAction!.Status.Should().Be("Approved");

        var updatedEvent = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        updatedEvent!.Status.Should().Be("Human_Approved");
    }

    [Fact]
    public async Task ResolveAction_Reject_SetsRejectedStatus()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id);

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = false,
            RowVersion = 0
        });

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;

        var updatedAction = await _dbContext.ActionRequests.FindAsync(action.Id);
        updatedAction!.Status.Should().Be("Rejected");

        var updatedEvent = await _dbContext.InboxEvents.FindAsync(inboxEvent.Id);
        updatedEvent!.Status.Should().Be("Human_Rejected");
    }

    [Fact]
    public async Task ResolveAction_AlreadyResolved_Returns409()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id, status: "Approved");

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ResolveAction_Expired_Returns409()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id, expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        result.Should().BeOfType<ConflictObjectResult>();

        var updatedAction = await _dbContext.ActionRequests.FindAsync(action.Id);
        updatedAction!.Status.Should().Be("Expired");
    }

    [Fact]
    public async Task ResolveAction_WrongRowVersion_Returns409()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id, rowVersion: 5);

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ResolveAction_CorrectRowVersion_Succeeds()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id, rowVersion: 3);

        var result = await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 3
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResolveAction_CreatesAuditTrail()
    {
        var inboxEvent = await CreateInboxEvent();
        var action = await CreateActionRequest(inboxEvent.Id);

        var auditTrailCountBefore = await _dbContext.AuditTrails.CountAsync();

        await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        var auditTrailCountAfter = await _dbContext.AuditTrails.CountAsync();
        auditTrailCountAfter.Should().Be(auditTrailCountBefore + 1);

        var auditTrail = await _dbContext.AuditTrails
            .OrderByDescending(a => a.Timestamp)
            .FirstAsync();
        auditTrail.Action.Should().Be("ActionRequestResolved");
        auditTrail.NewState.Should().Be("Approved");
        auditTrail.EntityId.Should().Be(action.Id.ToString());
    }

    [Fact]
    public async Task ResolveAction_WithModifiedResponse_UsesModifiedResponse()
    {
        var inboxEvent = await CreateInboxEvent(webhookCallbackPath: "test-callback");
        var action = await CreateActionRequest(inboxEvent.Id, draftResponse: "Original draft");

        await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            ModifiedResponse = "Modified response text",
            RowVersion = 0
        });

        _callbackServiceMock.Verify(
            x => x.SendCallbackAsync(
                "test-callback",
                It.IsAny<Core.Models.PolicyDecision>(),
                inboxEvent.Id,
                "Modified response text"),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAction_WithWebhookCallback_SendsCallback()
    {
        var inboxEvent = await CreateInboxEvent(webhookCallbackPath: "approval-callback");
        var action = await CreateActionRequest(inboxEvent.Id);

        await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        _callbackServiceMock.Verify(
            x => x.SendCallbackAsync(
                "approval-callback",
                It.Is<Core.Models.PolicyDecision>(d => d.Decision == Core.Models.PolicyDecisionType.AutoExecute),
                inboxEvent.Id,
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAction_WithoutWebhookCallback_NoCallbackSent()
    {
        var inboxEvent = await CreateInboxEvent(webhookCallbackPath: null);
        var action = await CreateActionRequest(inboxEvent.Id);

        await _controller.ResolveAction(action.Id, new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        _callbackServiceMock.Verify(
            x => x.SendCallbackAsync(
                It.IsAny<string>(),
                It.IsAny<Core.Models.PolicyDecision>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAction_InvalidActionId_Returns404()
    {
        var result = await _controller.ResolveAction(Guid.NewGuid(), new ActionsController.ResolveRequest
        {
            Approved = true,
            RowVersion = 0
        });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private async Task<InboxEvent> CreateInboxEvent(string? webhookCallbackPath = "test-callback")
    {
        var inboxEvent = new InboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            MessageId = Guid.NewGuid().ToString(),
            SenderEmail = "test@example.com",
            Subject = "Test Subject",
            BodyText = "Test body",
            WebhookCallbackPath = webhookCallbackPath,
            Status = "Action_Required",
            ReceivedAtUtc = DateTime.UtcNow
        };
        _dbContext.InboxEvents.Add(inboxEvent);
        await _dbContext.SaveChangesAsync();
        return inboxEvent;
    }

    private async Task<ActionRequest> CreateActionRequest(
        Guid inboxEventId,
        string status = "Pending",
        DateTime? expiresAt = null,
        string draftResponse = "Test draft response",
        uint rowVersion = 0)
    {
        var action = new ActionRequest
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            InboxEventId = inboxEventId,
            EvaluatedCategory = "Support",
            ConfidenceScore = 0.85m,
            DraftResponse = draftResponse,
            PolicyReason = "Test reason",
            Status = status,
            ExpiresAtUtc = expiresAt ?? DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = rowVersion
        };
        _dbContext.ActionRequests.Add(action);
        await _dbContext.SaveChangesAsync();
        return action;
    }

    private class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public MockTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetTenantId() => _tenantId;
    }
}
