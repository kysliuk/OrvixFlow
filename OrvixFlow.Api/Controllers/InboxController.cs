using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrvixFlow.Api.Filters;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InboxController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ITenantProvider _tenantProvider;

    public InboxController(IAgentService agentService, ITenantProvider tenantProvider)
    {
        _agentService = agentService;
        _tenantProvider = tenantProvider;
    }

    [HttpPost("process")]
    [EnableRateLimiting("ai-process")]
    [RequireModule("inbox-guardian")]
    public async Task<IActionResult> Process([FromBody] InboxRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _agentService.ProcessInternalAsync(request.Prompt, tenantId);
        
        if (response.IsSuccess)
        {
            return Ok(new { message = response.Message, metadata = response.Metadata });
        }
        
        return StatusCode(500, new { error = response.ErrorMessage });
    }
}

public class InboxRequest
{
    public string Prompt { get; set; } = string.Empty;
}
