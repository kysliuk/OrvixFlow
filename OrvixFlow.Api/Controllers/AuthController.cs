using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;
using System;
using Microsoft.AspNetCore.RateLimiting;

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
                : BuildRegisterFailure(result.Error);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Registration failed for email {Email}", req.Email);
            return StatusCode(500, new { error = "An unexpected error occurred during the registration process." });
        }
    }

    // F-03 FIX: Apply per-IP rate limiting to prevent brute-force password attacks.
    // The "login" rate limiter policy allows 5 attempts per minute per IP address.
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _authService.LoginAsync(req.Email, req.Password);
        
        if (!result.IsSuccess)
        {
            // F-28 FIX: Use LogWarning instead of LogDebug for failed login attempts.
            // LogDebug may not appear in production log levels, making security auditing impossible.
            _logger.LogWarning(
                "Login failed for email {Email} — reason: {Reason}",
                req.Email,
                result.Error);
        }
        
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken })
            : Unauthorized(new { error = result.Error });
    }

    [HttpPost("oauth-provision")]
    public async Task<IActionResult> OAuthProvision([FromBody] OAuthProvisionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Provider))
            return BadRequest(new { error = "Email and provider are required." });

        var result = await _authService.ProvisionOAuthUserAsync(req.Email, req.DisplayName, req.Provider, req.ExternalId);
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken })
            : BuildOAuthProvisionFailure(result.Error);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        var result = await _authService.RefreshSessionAsync(req.RefreshToken, req.ActiveCompanyId);
        
        return result.IsSuccess
            ? Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken })
            : Unauthorized(new { error = result.Error });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest(new { error = "Refresh token is required." });

        await _authService.LogoutAsync(req.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
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
            email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
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
            return Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken });
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

        var activeCompanyId = ParseClaimGuid("ActiveCompanyId") ?? ParseClaimGuid("TenantId");
        var result = await _authService.UpdateUserAsync(userId, req.DisplayName, activeCompanyId);

        if (result.IsSuccess)
            return Ok(new { token = result.Token, profile = result.Profile, refreshToken = result.RefreshToken });
        else
            return BadRequest(new { error = result.Error });
    }

    // F-33: Verify email with token
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return BadRequest(new { error = "Verification token is required." });

        var result = await _authService.VerifyEmailAsync(req.Token);

        return result.IsSuccess
            ? Ok(new { message = "Email verified successfully. You can now log in." })
            : BadRequest(new { error = result.Error });
    }

    private Guid? ParseClaimGuid(string claimType)
    {
        var value = HttpContext.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private IActionResult BuildRegisterFailure(string? error)
    {
        if (string.Equals(error, "An account with this email already exists.", StringComparison.Ordinal))
            return Conflict(new { error });

        return BadRequest(new { error = error ?? "Registration failed." });
    }

    private IActionResult BuildOAuthProvisionFailure(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return StatusCode(500, new { error = "OAuth provisioning failed." });

        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { error });

        return BadRequest(new { error });
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record OAuthProvisionRequest(string Email, string DisplayName, string Provider, string ExternalId);
public record SwitchCompanyRequest(Guid CompanyId);
public record UpdateProfileRequest(string? DisplayName);
public record RefreshRequest(string RefreshToken, Guid? ActiveCompanyId = null);
public record LogoutRequest(string RefreshToken);
public record VerifyRequest(string Token);
