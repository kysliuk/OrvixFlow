using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Web;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using InboxProcessingJob = OrvixFlow.Api.Jobs.InboxProcessingJob;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/inbox")]
public class InboxEventsController : ControllerBase
{
    private readonly IInboxEventRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<InboxEventsController> _logger;

    public InboxEventsController(
        IInboxEventRepository repository,
        ITenantProvider tenantProvider,
        ILogger<InboxEventsController> logger)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public class InboxEventRequest
    {
        [Required]
        public string MessageId { get; set; } = string.Empty;
        
        public string? ThreadId { get; set; }
        
        [Required]
        public string SenderEmail { get; set; } = string.Empty;
        
        public string SenderName { get; set; } = string.Empty;
        
        [Required]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Body { get; set; } = string.Empty;
        
        public string? WebhookCallbackPath { get; set; }
    }

    public class InboxEventResponse
    {
        public Guid EventId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsDuplicate { get; set; }
        public string? Message { get; set; }
    }

    [HttpPost("events")]
    public async Task<IActionResult> IngestEvent([FromBody] InboxEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return BadRequest(new { error = "MessageId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.SenderEmail) || string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { error = "SenderEmail and Body are required" });
        }

        var tenantId = _tenantProvider.GetTenantId();

        var (inboxEvent, isDuplicate) = await _repository.CreateWithIdempotencyCheckAsync(
            request.MessageId,
            tenantId,
            request.SenderEmail,
            HttpUtility.HtmlEncode(request.SenderName),
            HttpUtility.HtmlEncode(request.Subject),
            SanitizeHtml(request.Body),
            request.WebhookCallbackPath);

        if (isDuplicate)
        {
            _logger.LogInformation(
                "Duplicate email detected: {MessageId} for tenant {TenantId}",
                request.MessageId,
                tenantId);

            return Ok(new InboxEventResponse
            {
                EventId = inboxEvent!.Id,
                Status = inboxEvent.Status,
                IsDuplicate = true,
                Message = "Email already processed"
            });
        }

        BackgroundJob.Enqueue<InboxProcessingJob>(job => job.ProcessAsync(inboxEvent!.Id, tenantId));

        _logger.LogInformation(
            "Email ingested: {MessageId}, EventId: {EventId}, Tenant: {TenantId}",
            request.MessageId,
            inboxEvent.Id,
            tenantId);

        return Accepted(new InboxEventResponse
        {
            EventId = inboxEvent.Id,
            Status = inboxEvent.Status,
            IsDuplicate = false,
            Message = "Email queued for processing"
        });
    }

    [HttpGet("events/{eventId:guid}")]
    public async Task<IActionResult> GetEvent(Guid eventId)
    {
        var inboxEvent = await _repository.GetByIdAsync(eventId);
        if (inboxEvent == null)
        {
            return NotFound(new { error = "Event not found" });
        }

        return Ok(new
        {
            inboxEvent.Id,
            inboxEvent.MessageId,
            inboxEvent.SenderEmail,
            inboxEvent.SenderName,
            inboxEvent.Subject,
            inboxEvent.Status,
            inboxEvent.ReceivedAtUtc
        });
    }

    [HttpGet("events")]
    public async Task<IActionResult> ListEvents(
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var tenantId = _tenantProvider.GetTenantId();
        
        var (items, totalCount) = await _repository.ListAsync(tenantId, status, limit, offset);

        var events = items.Select(e => new
        {
            eventId = e.Id,
            messageId = e.MessageId,
            subject = e.Subject,
            senderEmail = e.SenderEmail,
            senderName = e.SenderName,
            status = e.Status,
            receivedAtUtc = e.ReceivedAtUtc
        });

        return Ok(new
        {
            items = events,
            total = totalCount,
            limit,
            offset
        });
    }

    private static string SanitizeHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var text = HttpUtility.HtmlEncode(html);
        if (text.Length > 10000)
            text = text[..10000] + "... [truncated]";

        return text;
    }
}
