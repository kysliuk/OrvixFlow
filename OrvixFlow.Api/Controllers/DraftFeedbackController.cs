using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/inbox/feedback")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class DraftFeedbackController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDraftFeedbackService _feedbackService;
    private readonly ILogger<DraftFeedbackController> _logger;

    public DraftFeedbackController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        IDraftFeedbackService feedbackService,
        ILogger<DraftFeedbackController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _feedbackService = feedbackService;
        _logger = logger;
    }

    public class FeedbackRequest
    {
        [Required]
        public Guid ActionRequestId { get; set; }
        [Required]
        public string OriginalDraft { get; set; } = string.Empty;
        [Required]
        public string FinalHumanDraft { get; set; } = string.Empty;
    }

    public class FeedbackResponse
    {
        public Guid Id { get; set; }
        public Guid ActionRequestId { get; set; }
        public decimal EditDistance { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] FeedbackRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var actionRequest = await _dbContext.ActionRequests
            .FirstOrDefaultAsync(ar => ar.Id == request.ActionRequestId && ar.TenantId == tenantId);

        if (actionRequest == null)
        {
            return NotFound(new { error = "Action request not found" });
        }

        var feedback = await _feedbackService.RecordFeedbackAsync(
            tenantId,
            request.ActionRequestId,
            request.OriginalDraft,
            request.FinalHumanDraft);

        _logger.LogInformation("Recorded feedback {FeedbackId} with edit distance {EditDistance} for tenant {TenantId}",
            feedback.Id, feedback.EditDistance, tenantId);

        return Created(string.Empty, new FeedbackResponse
        {
            Id = feedback.Id,
            ActionRequestId = feedback.ActionRequestId,
            EditDistance = feedback.EditDistance,
            CreatedAtUtc = feedback.CreatedAtUtc
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetFeedbackHistory([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var feedbacks = await _dbContext.DraftFeedbacks
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Skip(offset)
            .Take(limit)
            .Select(f => new FeedbackResponse
            {
                Id = f.Id,
                ActionRequestId = f.ActionRequestId,
                EditDistance = f.EditDistance,
                CreatedAtUtc = f.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(feedbacks);
    }
}
