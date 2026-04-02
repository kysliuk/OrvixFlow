using System;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Api.Jobs;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/actions")]
public class ActionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IWebhookCallbackService _callbackService;
    private readonly ILogger<ActionsController> _logger;

    public ActionsController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        IWebhookCallbackService callbackService,
        ILogger<ActionsController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _callbackService = callbackService;
        _logger = logger;
    }

    public class ActionRequestResponse
    {
        public Guid Id { get; set; }
        public Guid InboxEventId { get; set; }
        public string EvaluatedCategory { get; set; } = string.Empty;
        public decimal ConfidenceScore { get; set; }
        public string DraftResponse { get; set; } = string.Empty;
        public string PolicyReason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public class ResolveRequest
    {
        public bool Approved { get; set; }
        public string? ModifiedResponse { get; set; }
        public uint RowVersion { get; set; }
    }

    [HttpGet("{actionId:guid}")]
    public async Task<IActionResult> GetAction(Guid actionId)
    {
        var action = await _dbContext.ActionRequests
            .Include(a => a.InboxEvent)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action == null)
        {
            return NotFound(new { error = "Action request not found" });
        }

        return Ok(new ActionRequestResponse
        {
            Id = action.Id,
            InboxEventId = action.InboxEventId,
            EvaluatedCategory = action.EvaluatedCategory,
            ConfidenceScore = action.ConfidenceScore,
            DraftResponse = action.DraftResponse,
            PolicyReason = action.PolicyReason,
            Status = action.Status,
            ExpiresAtUtc = action.ExpiresAtUtc
        });
    }

    [HttpPost("{actionId:guid}/resolve")]
    public async Task<IActionResult> ResolveAction(Guid actionId, [FromBody] ResolveRequest request)
    {
        var action = await _dbContext.ActionRequests
            .Include(a => a.InboxEvent)
            .FirstOrDefaultAsync(a => a.Id == actionId);

        if (action == null)
        {
            return NotFound(new { error = "Action request not found" });
        }

        if (action.Status != "Pending")
        {
            return Conflict(new { error = $"Action already {action.Status.ToLower()}" });
        }

        if (action.ExpiresAtUtc < DateTime.UtcNow)
        {
            action.Status = "Expired";
            await _dbContext.SaveChangesAsync();
            return Conflict(new { error = "Action request has expired" });
        }

        if (action.RowVersion != request.RowVersion)
        {
            return Conflict(new { error = "Concurrent modification detected. Please refresh and try again." });
        }

        var previousState = action.Status;
        var newStatus = request.Approved ? "Approved" : "Rejected";
        var actor = User.Identity?.Name ?? "system";

        action.Status = newStatus;

        var inboxEvent = action.InboxEvent;
        if (inboxEvent != null)
        {
            inboxEvent.Status = request.Approved ? "Human_Approved" : "Human_Rejected";
        }

        var auditTrail = new AuditTrail
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetTenantId(),
            Action = "ActionRequestResolved",
            Actor = actor,
            EntityId = actionId.ToString(),
            PreviousState = previousState,
            NewState = newStatus,
            DecisionDetails = $"{(request.Approved ? "Approved" : "Rejected")} by {actor}. " +
                           $"Original draft: {action.DraftResponse}. " +
                           $"Modified response: {request.ModifiedResponse ?? "None"}",
            Timestamp = DateTime.UtcNow
        };
        _dbContext.AuditTrails.Add(auditTrail);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Concurrent modification detected. Please refresh and try again." });
        }

        if (inboxEvent?.WebhookCallbackPath != null)
        {
            var decision = new Core.Models.PolicyDecision
            {
                Decision = request.Approved ? Core.Models.PolicyDecisionType.AutoExecute : Core.Models.PolicyDecisionType.HoldForApproval,
                Reason = $"Manually {newStatus.ToLower()} by {actor}",
                Category = action.EvaluatedCategory,
                ConfidenceScore = action.ConfidenceScore,
                ShouldSendCallback = true
            };

            await _callbackService.SendCallbackAsync(
                inboxEvent.WebhookCallbackPath,
                decision,
                inboxEvent.Id,
                request.ModifiedResponse ?? action.DraftResponse);
        }

        if (request.Approved && !string.IsNullOrEmpty(request.ModifiedResponse) && request.ModifiedResponse != action.DraftResponse)
        {
            var feedback = new DraftFeedback
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantProvider.GetTenantId(),
                ActionRequestId = actionId,
                OriginalDraft = action.DraftResponse,
                FinalHumanDraft = request.ModifiedResponse,
                EditDistance = 0,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.DraftFeedbacks.Add(feedback);
            await _dbContext.SaveChangesAsync();

            try
            {
                BackgroundJob.Enqueue<FeedbackEnrichmentJob>(job => job.ProcessAsync(feedback.Id));
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Hangfire not initialized, skipping feedback enrichment job");
            }
        }

        _logger.LogInformation(
            "Action {ActionId} resolved: {Status} by {Actor} for inbox event {EventId}",
            actionId,
            newStatus,
            actor,
            action.InboxEventId);

        return Ok(new
        {
            actionId = action.Id,
            status = action.Status,
            message = request.Approved ? "Email approved and will be sent" : "Email rejected"
        });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingActions()
    {
        var actions = await _dbContext.ActionRequests
            .Include(a => a.InboxEvent)
            .AsNoTracking()
            .Where(a => a.Status == "Pending" && a.ExpiresAtUtc > DateTime.UtcNow)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new ActionRequestResponse
            {
                Id = a.Id,
                InboxEventId = a.InboxEventId,
                EvaluatedCategory = a.EvaluatedCategory,
                ConfidenceScore = a.ConfidenceScore,
                DraftResponse = a.DraftResponse,
                PolicyReason = a.PolicyReason,
                Status = a.Status,
                ExpiresAtUtc = a.ExpiresAtUtc
            })
            .ToListAsync();

        return Ok(actions);
    }
}
