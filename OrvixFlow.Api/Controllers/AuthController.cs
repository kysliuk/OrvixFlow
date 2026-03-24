using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;
using System;

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
            activeCompanyId = user.FindFirst("ActiveCompanyId")?.Value ?? user.FindFirst("TenantId")?.Value,
            plan = user.FindFirst("Plan")?.Value,
            role = user.FindFirst("Role")?.Value,
            displayName = user.FindFirst("DisplayName")?.Value
        });
    }

    [HttpPost("switch-company")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequest req)
    {
        var user = HttpContext.User;
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized(new { error = "Invalid user context." });
        }

        var result = await _authService.SwitchCompanyAsync(userId, req.CompanyId);
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile })
            : Unauthorized(new { error = result.Error });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record OAuthProvisionRequest(string Email, string DisplayName, string Provider, string ExternalId);
public record SwitchCompanyRequest(Guid CompanyId);
