using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Api.Filters;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/inbox/rag")]
[RequireAutomationKey]
public class RagEmailController : ControllerBase
{
    private readonly IRagEmailService _ragEmailService;

    public RagEmailController(IRagEmailService ragEmailService)
    {
        _ragEmailService = ragEmailService;
    }

    [HttpPost]
    public async Task<ActionResult<N8nEmailPayload>> ProcessRagEmail([FromBody] RagEmailRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is null.");
        }

        try
        {
            var payload = await _ragEmailService.ProcessRagEmailAsync(
                request.SenderEmail,
                request.Subject,
                request.BodyText,
                request.TenantId,
                request.MessageId);

            return Ok(payload);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record RagEmailRequest(
    Guid TenantId,
    string MessageId,
    string SenderEmail,
    string Subject,
    string BodyText
);
