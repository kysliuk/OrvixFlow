using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Api.Filters;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIngestionService _ingestionService;

    public AgentController(IAgentService agentService, ITenantProvider tenantProvider, IIngestionService ingestionService)
    {
        _agentService = agentService;
        _tenantProvider = tenantProvider;
        _ingestionService = ingestionService;
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] AgentRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var response = await _agentService.ProcessInternalAsync(request.Prompt, tenantId);
        
        if (response.IsSuccess)
        {
            return Ok(new { message = response.Message, metadata = response.Metadata });
        }
        
        return StatusCode(500, new { error = response.ErrorMessage });
    }

    [HttpPost("ingest")]
    [RequireModule("knowledge-base")]
    public async Task<IActionResult> Ingest([FromBody] AgentRequest request)
    {
        await _ingestionService.IngestTextAsync(request.Prompt);
        return Ok(new { status = "Ingested and embedded successfully." });
    }
}

public class AgentRequest
{
    public string Prompt { get; set; } = string.Empty;
}
