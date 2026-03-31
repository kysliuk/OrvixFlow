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
    private readonly Microsoft.Extensions.Logging.ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, Microsoft.Extensions.Logging.ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        try
        {
            var result = await _authService.RegisterAsync(req.Email, req.Password, req.DisplayName);
            return result.IsSuccess
                ? Ok(new { token = result.Token, profile = result.Profile })
                : Conflict(new { error = result.Error });
        }
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"REGISTRATION ERROR: {ex}");
            return StatusCode(500, new { error = "An unexpected error occurred during the registration process.", details = ex.ToString() });
        }
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
        _logger.LogInformation("[DEBUG][CompanySwitch] Request received for target CompanyId: {CompanyId}", req.CompanyId);

        var user = HttpContext.User;
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        _logger.LogInformation("[DEBUG][CompanySwitch] Extracted UserId value from token: {UserIdValue}", userIdValue ?? "NULL");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            _logger.LogWarning("[DEBUG][CompanySwitch] Rejected: Invalid or missing user context mapped from JWT claims.");
            return Unauthorized(new { error = "Invalid user context." });
        }

        var result = await _authService.SwitchCompanyAsync(userId, req.CompanyId);
        
        if (result.IsSuccess)
        {
            _logger.LogInformation("[DEBUG][CompanySwitch] Success. Issuing new JWT for CompanyId: {CompanyId}", req.CompanyId);
            return Ok(new { token = result.Token, profile = result.Profile });
        }
        else
        {
            _logger.LogWarning("[DEBUG][CompanySwitch] Rejected by AuthService. Reason: {Error}", result.Error);
            return Unauthorized(new { error = result.Error });
        }
    }

    [HttpPut("profile")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = HttpContext.User;
        var userIdValue = user.FindFirst("sub")?.Value
            ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized(new { error = "Invalid user context." });

        var result = await _authService.UpdateUserAsync(userId, req.DisplayName);

        if (result.IsSuccess)
            return Ok(new { token = result.Token, profile = result.Profile });
        else
            return BadRequest(new { error = result.Error });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record OAuthProvisionRequest(string Email, string DisplayName, string Provider, string ExternalId);
public record SwitchCompanyRequest(Guid CompanyId);
public record UpdateProfileRequest(string? DisplayName);
