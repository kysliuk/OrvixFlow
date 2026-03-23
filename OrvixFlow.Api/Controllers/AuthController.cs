using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        var result = await _authService.RegisterAsync(req.Email, req.Password, req.DisplayName);
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile })
            : Conflict(new { error = result.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _authService.LoginAsync(req.Email, req.Password);
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile })
            : Unauthorized(new { error = result.Error });
    }

    [HttpPost("oauth-provision")]
    public async Task<IActionResult> OAuthProvision([FromBody] OAuthProvisionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Provider))
            return BadRequest(new { error = "Email and provider are required." });

        var result = await _authService.ProvisionOAuthUserAsync(req.Email, req.DisplayName, req.Provider, req.ExternalId);
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile })
            : StatusCode(500, new { error = result.Error });
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Me()
    {
        var user = HttpContext.User;
        return Ok(new
        {
            userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value,
            email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            tenantId = user.FindFirst("TenantId")?.Value,
            plan = user.FindFirst("Plan")?.Value,
            role = user.FindFirst("Role")?.Value,
            displayName = user.FindFirst("DisplayName")?.Value
        });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record OAuthProvisionRequest(string Email, string DisplayName, string Provider, string ExternalId);
