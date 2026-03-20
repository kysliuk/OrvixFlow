using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Api.Filters;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    [HttpPost("n8n")]
    [RequireAutomationKey]
    public IActionResult ReceiveDataFromN8n([FromBody] object payload)
    {
        // This endpoint will be hit by an n8n HTTP Request node with the X-Automation-Key header.
        // It allows n8n to safely push results back to OrvixFlow after asynchronous automation jobs finish.
        return Ok(new { message = "Data successfully received and authorized from n8n automation engine." });
    }
}
