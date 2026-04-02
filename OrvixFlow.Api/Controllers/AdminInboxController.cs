using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/admin/inbox")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class AdminInboxController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AdminInboxController> _logger;

    public AdminInboxController(
        AppDbContext dbContext,
        ILogger<AdminInboxController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var totalEvents = await _dbContext.InboxEvents.CountAsync();
        var totalActions = await _dbContext.ActionRequests.CountAsync();
        var pendingActions = await _dbContext.ActionRequests.CountAsync(a => a.Status == "Pending");
        var failedEvents = await _dbContext.InboxEvents.CountAsync(e => e.Status == "Failed");
        var completedEvents = await _dbContext.InboxEvents.CountAsync(e => e.Status == "Completed");

        var avgConfidence = await _dbContext.ActionRequests
            .Where(a => a.Status != "Pending")
            .AverageAsync(a => (double?)a.ConfidenceScore) ?? 0;

        var feedbackCount = await _dbContext.DraftFeedbacks.CountAsync();
        var avgEditDistance = await _dbContext.DraftFeedbacks
            .AverageAsync(f => (double?)f.EditDistance) ?? 0;

        var connectionCount = await _dbContext.MailboxConnections.CountAsync();
        var activeConnections = await _dbContext.MailboxConnections.CountAsync(c => c.IsActive);

        return Ok(new
        {
            totalEvents,
            totalActions,
            pendingActions,
            failedEvents,
            completedEvents,
            avgConfidence = Math.Round(avgConfidence, 2),
            feedbackCount,
            avgEditDistance = Math.Round(avgEditDistance, 2),
            connectionCount,
            activeConnections
        });
    }

    [HttpGet("companies/{companyId:guid}/inbox")]
    public async Task<IActionResult> GetCompanyInbox(Guid companyId)
    {
        var events = await _dbContext.InboxEvents
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == companyId)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(100)
            .Select(e => new
            {
                e.Id,
                e.MessageId,
                e.SenderEmail,
                e.Subject,
                e.Status,
                e.ReceivedAtUtc
            })
            .ToListAsync();

        var stuckActions = await _dbContext.ActionRequests
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == companyId && a.Status == "Pending")
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new
            {
                a.Id,
                a.InboxEventId,
                a.EvaluatedCategory,
                a.ConfidenceScore,
                a.Status,
                a.CreatedAtUtc,
                a.ExpiresAtUtc
            })
            .ToListAsync();

        var policies = await _dbContext.WorkflowPolicies
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == companyId)
            .Select(p => new
            {
                p.Id,
                p.Category,
                p.AutoExecute,
                p.ConfidenceThreshold
            })
            .ToListAsync();

        return Ok(new
        {
            events,
            stuckActions,
            policies
        });
    }
}
