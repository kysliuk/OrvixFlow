using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/inbox")]
public class InboxGuardianController : ControllerBase
{
    private readonly IInboxGuardianService _inboxGuardianService;
    private readonly ITenantProvider _tenantProvider;

    public InboxGuardianController(IInboxGuardianService inboxGuardianService, ITenantProvider tenantProvider)
    {
        _inboxGuardianService = inboxGuardianService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessEmail([FromBody] InboxMessage message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Body))
        {
            return BadRequest("Invalid message payload.");
        }

        var tenantId = _tenantProvider.GetTenantId();
        var response = await _inboxGuardianService.ProcessIncomingMessageAsync(message, tenantId);

        if (response.IsSuccess)
        {
            return Ok(response);
        }

        return StatusCode(500, response);
    }
}
