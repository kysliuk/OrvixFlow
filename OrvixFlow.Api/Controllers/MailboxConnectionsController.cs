using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OrvixFlow.Core.Authorization;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/inbox/connections")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class MailboxConnectionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IN8nProvisioningService _n8nProvisioning;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MailboxConnectionsController> _logger;
    private readonly IMailboxCredentialService _mailboxCredentialService;
    private readonly IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public MailboxConnectionsController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        IN8nProvisioningService n8nProvisioning,
        IServiceProvider serviceProvider,
        ILogger<MailboxConnectionsController> logger,
        IMailboxCredentialService mailboxCredentialService,
        IMemoryCache memoryCache,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _n8nProvisioning = n8nProvisioning;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _mailboxCredentialService = mailboxCredentialService;
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public class ConnectionRequest
    {
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; } = string.Empty;
        [Required]
        public string Provider { get; set; } = string.Empty;
    }

    public class ConnectionResponse
    {
        public Guid Id { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? N8nWorkflowId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ConnectedAtUtc { get; set; }
        public bool HasCredential { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetConnections()
    {
        var tenantId = _tenantProvider.GetTenantId();
        var connections = await _dbContext.MailboxConnections
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ConnectionResponse
            {
                Id = c.Id,
                EmailAddress = c.EmailAddress,
                Provider = c.Provider,
                IsActive = c.IsActive,
                N8nWorkflowId = c.N8nWorkflowId,
                CreatedAtUtc = c.CreatedAtUtc,
                ConnectedAtUtc = c.ConnectedAtUtc,
                HasCredential = c.CredentialId != null
            })
            .ToListAsync();

        return Ok(connections);
    }

    [HttpPost]
    public async Task<IActionResult> CreateConnection([FromBody] ConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailAddress) || string.IsNullOrWhiteSpace(request.Provider))
        {
            return BadRequest(new { error = "EmailAddress and Provider are required" });
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.NewGuid().ToString());

        var existing = await _dbContext.MailboxConnections
            .AnyAsync(c => c.TenantId == tenantId && c.EmailAddress == request.EmailAddress);

        if (existing)
        {
            return Conflict(new { error = "This email address is already connected" });
        }

        var connection = new MailboxConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            EmailAddress = request.EmailAddress,
            Provider = request.Provider,
            IsActive = false
        };

        _dbContext.MailboxConnections.Add(connection);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created mailbox connection {ConnectionId} for {Email} tenant {TenantId}",
            connection.Id, request.EmailAddress, tenantId);

        return Created(string.Empty, new ConnectionResponse
        {
            Id = connection.Id,
            EmailAddress = connection.EmailAddress,
            Provider = connection.Provider,
            IsActive = connection.IsActive,
            N8nWorkflowId = connection.N8nWorkflowId,
            CreatedAtUtc = connection.CreatedAtUtc,
            ConnectedAtUtc = connection.ConnectedAtUtc,
            HasCredential = false
        });
    }

    [HttpPost("{connectionId:guid}/activate")]
    public async Task<IActionResult> ToggleConnection(Guid connectionId, [FromBody] ToggleRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (request.IsActive && !connection.IsActive)
        {
            var providerLower = connection.Provider.ToLowerInvariant();
            if ((providerLower == "gmail" || providerLower == "microsoft" || providerLower == "outlook") && connection.CredentialId == null)
            {
                return BadRequest(new { error = "MISSING_CREDENTIALS", message = "Please link your OAuth credentials before activating the mailbox connection." });
            }

            if (string.IsNullOrEmpty(connection.N8nWorkflowId))
            {
                BackgroundJob.Enqueue(() => ProvisionN8nWorkflowJob(connectionId, tenantId));
                _logger.LogInformation("Queued n8n provisioning for connection {ConnectionId}", connectionId);
                return Ok(new ConnectionResponse
                {
                    Id = connection.Id,
                    EmailAddress = connection.EmailAddress,
                    Provider = connection.Provider,
                    IsActive = false,
                    N8nWorkflowId = connection.N8nWorkflowId,
                    CreatedAtUtc = connection.CreatedAtUtc,
                    ConnectedAtUtc = connection.ConnectedAtUtc,
                    HasCredential = connection.CredentialId != null
                });
            }

            connection.IsActive = true;
            connection.ConnectedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        else if (!request.IsActive)
        {
            connection.IsActive = false;
            await _dbContext.SaveChangesAsync();
        }

        _logger.LogInformation("Toggled connection {ConnectionId} to {IsActive} for tenant {TenantId}",
            connectionId, request.IsActive, tenantId);

        return Ok(new ConnectionResponse
        {
            Id = connection.Id,
            EmailAddress = connection.EmailAddress,
            Provider = connection.Provider,
            IsActive = connection.IsActive,
            N8nWorkflowId = connection.N8nWorkflowId,
            CreatedAtUtc = connection.CreatedAtUtc,
            ConnectedAtUtc = connection.ConnectedAtUtc,
            HasCredential = connection.CredentialId != null
        });
    }

    public class ToggleRequest
    {
        public bool IsActive { get; set; }
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestN8nConnection()
    {
        var result = await _n8nProvisioning.TestConnectionAsync();
        return Ok(new { success = result, message = result ? "n8n connection successful" : "n8n connection failed" });
    }

    [HttpDelete("{connectionId:guid}")]
    public async Task<IActionResult> DeleteConnection(Guid connectionId)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (!string.IsNullOrEmpty(connection.N8nWorkflowId))
        {
            BackgroundJob.Enqueue(() => CleanupN8nWorkflowJob(connection.N8nWorkflowId, connection.N8nCredentialId));
        }

        if (connection.CredentialId != null)
        {
            await _mailboxCredentialService.DeleteCredentialAsync(connection.CredentialId.Value);
        }

        _dbContext.MailboxConnections.Remove(connection);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted mailbox connection {ConnectionId} for tenant {TenantId}",
            connectionId, tenantId);

        return NoContent();
    }

    [HttpPost("{connectionId:guid}/credential/authorize")]
    public async Task<IActionResult> Authorize(Guid connectionId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var actingUserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (!callerRole.IsCompanyAdminOrAbove() && connection.UserId != actingUserId)
        {
            return StatusCode(403, new { error = "You do not have permission to authorize this mailbox." });
        }

        var state = Guid.NewGuid().ToString("N");
        _memoryCache.Set($"oauth_state_{connectionId}", state, TimeSpan.FromMinutes(10));

        var provider = connection.Provider.ToLowerInvariant();
        
        var clientId = provider switch
        {
            "gmail" => _configuration["Mailbox:Google:ClientId"] ?? _configuration["MAILBOX_GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_ID"),
            "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:ClientId"] ?? _configuration["MAILBOX_MICROSOFT_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_ID"),
            _ => null
        };

        var redirectUri = provider switch
        {
            "gmail" => _configuration["Mailbox:Google:RedirectUri"] ?? _configuration["MAILBOX_GOOGLE_REDIRECT_URI"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_REDIRECT_URI"),
            "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:RedirectUri"] ?? _configuration["MAILBOX_MICROSOFT_REDIRECT_URI"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_REDIRECT_URI"),
            _ => null
        };

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            var mockCallbackUrl = $"{_configuration["Frontend:BaseUrl"] ?? "http://localhost:3000"}/mailbox-callback?code=mock_code&state={state}&connectionId={connectionId}";
            return Ok(new { authorizationUrl = mockCallbackUrl, isMock = true });
        }

        string authorizationUrl;
        if (provider == "gmail")
        {
            var googleScope = "https://mail.google.com/ openid email profile";
            authorizationUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(googleScope)}&state={state}&access_type=offline&prompt=consent";
        }
        else
        {
            var microsoftScope = "https://outlook.office.com/IMAP.AccessAsUser.All offline_access openid email profile";
            authorizationUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(microsoftScope)}&state={state}&response_mode=query&prompt=consent";
        }

        return Ok(new { authorizationUrl, isMock = false });
    }

    public class CallbackRequest
    {
        [Required]
        public string Code { get; set; } = string.Empty;
        [Required]
        public string State { get; set; } = string.Empty;
    }

    [HttpPost("{connectionId:guid}/credential/callback")]
    public async Task<IActionResult> Callback(Guid connectionId, [FromBody] CallbackRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var actingUserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (!callerRole.IsCompanyAdminOrAbove() && connection.UserId != actingUserId)
        {
            return StatusCode(403, new { error = "You do not have permission to configure this mailbox." });
        }

        if (!_memoryCache.TryGetValue($"oauth_state_{connectionId}", out string? cachedState) || cachedState != request.State)
        {
            return BadRequest(new { error = "INVALID_STATE", message = "OAuth state validation failed. Possible CSRF attempt." });
        }
        
        _memoryCache.Remove($"oauth_state_{connectionId}");

        string accessToken;
        string refreshToken;
        string providerAccountId = "mock_subject_id";
        var expiresAtUtc = DateTime.UtcNow.AddHours(1);
        var scopes = new List<string>();

        var provider = connection.Provider.ToLowerInvariant();

        if (request.Code == "mock_code")
        {
            accessToken = "mock_access_token_" + Guid.NewGuid().ToString("N");
            refreshToken = "mock_refresh_token_" + Guid.NewGuid().ToString("N");
            scopes.Add(provider == "gmail" ? "https://mail.google.com/" : "https://outlook.office.com/IMAP.AccessAsUser.All");
        }
        else
        {
            var clientId = provider switch
            {
                "gmail" => _configuration["Mailbox:Google:ClientId"] ?? _configuration["MAILBOX_GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_ID"),
                "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:ClientId"] ?? _configuration["MAILBOX_MICROSOFT_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_ID"),
                _ => null
            };

            var clientSecret = provider switch
            {
                "gmail" => _configuration["Mailbox:Google:ClientSecret"] ?? _configuration["MAILBOX_GOOGLE_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_SECRET"),
                "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:ClientSecret"] ?? _configuration["MAILBOX_MICROSOFT_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_SECRET"),
                _ => null
            };

            var redirectUri = provider switch
            {
                "gmail" => _configuration["Mailbox:Google:RedirectUri"] ?? _configuration["MAILBOX_GOOGLE_REDIRECT_URI"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_REDIRECT_URI"),
                "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:RedirectUri"] ?? _configuration["MAILBOX_MICROSOFT_REDIRECT_URI"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_REDIRECT_URI"),
                _ => null
            };

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
            {
                return BadRequest(new { error = "OAUTH_UNCONFIGURED", message = "Provider client credentials are not configured on the backend." });
            }

            var tokenEndpoint = provider == "gmail"
                ? "https://oauth2.googleapis.com/token"
                : "https://login.microsoftonline.com/common/oauth2/v2.0/token";

            var httpClient = _httpClientFactory.CreateClient();
            var values = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "code", request.Code },
                { "grant_type", "authorization_code" },
                { "redirect_uri", redirectUri }
            };

            var content = new FormUrlEncodedContent(values);
            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsync(tokenEndpoint, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to contact OAuth token endpoint for provider {Provider}", provider);
                return StatusCode(502, new { error = "OAUTH_COMMUNICATION_ERROR", message = "Failed to contact authorization provider." });
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("OAuth token exchange failed for provider {Provider}. Status: {Status}, Body: {Body}", provider, response.StatusCode, errorPayload);
                return BadRequest(new { error = "TOKEN_EXCHANGE_FAILED", message = "Provider rejected authorization code." });
            }

            var tokenJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(tokenJson);
            var root = doc.RootElement;

            accessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
            refreshToken = root.GetProperty("refresh_token").GetString() ?? string.Empty;
            
            if (root.TryGetProperty("expires_in", out var expProp))
            {
                expiresAtUtc = DateTime.UtcNow.AddSeconds(expProp.GetInt32());
            }

            if (root.TryGetProperty("scope", out var scopeProp))
            {
                scopes.AddRange(scopeProp.GetString()?.Split(' ') ?? Array.Empty<string>());
            }

            if (root.TryGetProperty("id_token", out var idTokenProp))
            {
                var idToken = idTokenProp.GetString();
                if (!string.IsNullOrEmpty(idToken))
                {
                    providerAccountId = ExtractSubjectFromIdToken(idToken);
                }
            }
        }

        var credential = await _mailboxCredentialService.StoreCredentialAsync(
            tenantId,
            connectionId,
            connection.Provider,
            providerAccountId,
            accessToken,
            refreshToken,
            scopes,
            expiresAtUtc);

        _logger.LogInformation("Successfully completed OAuth callback link for mailbox connection {ConnectionId}", connectionId);

        return Ok(new 
        { 
            success = true, 
            message = "Mailbox credential successfully authorized and linked." 
        });
    }

    private static string ExtractSubjectFromIdToken(string idToken)
    {
        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2) return "unknown_sub";
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            var jsonBytes = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("sub", out var subProp))
            {
                return subProp.GetString() ?? "unknown_sub";
            }
            return "unknown_sub";
        }
        catch
        {
            return "unknown_sub";
        }
    }

    [HttpPost("{connectionId:guid}/credential/refresh")]
    public async Task<IActionResult> Refresh(Guid connectionId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var actingUserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (!callerRole.IsCompanyAdminOrAbove() && connection.UserId != actingUserId)
        {
            return StatusCode(403, new { error = "You do not have permission to manage this mailbox." });
        }

        if (connection.CredentialId == null)
        {
            return BadRequest(new { error = "NO_CREDENTIAL", message = "No credential linked to this mailbox connection." });
        }

        var decrypted = await _mailboxCredentialService.GetDecryptedTokensAsync(connection.CredentialId.Value);
        if (decrypted == null)
        {
            return BadRequest(new { error = "DECRYPTION_FAILED", message = "Failed to retrieve or decrypt credential tokens." });
        }

        var provider = connection.Provider.ToLowerInvariant();

        if (decrypted.Value.refreshToken.StartsWith("mock_refresh_token_"))
        {
            var mockAccessToken = "mock_access_token_" + Guid.NewGuid().ToString("N");
            var mockRefreshToken = decrypted.Value.refreshToken;
            await _mailboxCredentialService.UpdateTokensAsync(connection.CredentialId.Value, mockAccessToken, mockRefreshToken, DateTime.UtcNow.AddHours(1));
            return Ok(new { success = true, message = "Mock credential refreshed." });
        }

        var clientId = provider switch
        {
            "gmail" => _configuration["Mailbox:Google:ClientId"] ?? _configuration["MAILBOX_GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_ID"),
            "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:ClientId"] ?? _configuration["MAILBOX_MICROSOFT_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_ID"),
            _ => null
        };

        var clientSecret = provider switch
        {
            "gmail" => _configuration["Mailbox:Google:ClientSecret"] ?? _configuration["MAILBOX_GOOGLE_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_SECRET"),
            "outlook" or "microsoft" => _configuration["Mailbox:Microsoft:ClientSecret"] ?? _configuration["MAILBOX_MICROSOFT_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_SECRET"),
            _ => null
        };

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return BadRequest(new { error = "OAUTH_UNCONFIGURED", message = "Provider client credentials are not configured on the backend." });
        }

        var tokenEndpoint = provider == "gmail"
            ? "https://oauth2.googleapis.com/token"
            : "https://login.microsoftonline.com/common/oauth2/v2.0/token";

        var httpClient = _httpClientFactory.CreateClient();
        var values = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "refresh_token", decrypted.Value.refreshToken },
            { "grant_type", "refresh_token" }
        };

        var content = new FormUrlEncodedContent(values);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(tokenEndpoint, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to contact OAuth token endpoint for refresh for provider {Provider}", provider);
            return StatusCode(502, new { error = "OAUTH_COMMUNICATION_ERROR", message = "Failed to contact authorization provider." });
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("OAuth refresh token exchange failed for provider {Provider}. Status: {Status}, Body: {Body}", provider, response.StatusCode, errorPayload);
            return BadRequest(new { error = "REFRESH_FAILED", message = "Provider rejected refresh token." });
        }

        var tokenJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(tokenJson);
        var root = doc.RootElement;

        var newAccessToken = root.GetProperty("access_token").GetString() ?? string.Empty;
        var newRefreshToken = root.TryGetProperty("refresh_token", out var refProp) 
            ? refProp.GetString() ?? decrypted.Value.refreshToken 
            : decrypted.Value.refreshToken;

        var expiresAtUtc = DateTime.UtcNow.AddHours(1);
        if (root.TryGetProperty("expires_in", out var expProp))
        {
            expiresAtUtc = DateTime.UtcNow.AddSeconds(expProp.GetInt32());
        }

        await _mailboxCredentialService.UpdateTokensAsync(connection.CredentialId.Value, newAccessToken, newRefreshToken, expiresAtUtc);

        return Ok(new { success = true, message = "Credential refreshed successfully." });
    }

    [HttpDelete("{connectionId:guid}/credential")]
    public async Task<IActionResult> Disconnect(Guid connectionId)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var callerRole = UserRoleExtensions.ParseRole(User.FindFirst("Role")?.Value);
        var actingUserId = Guid.Parse(User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

        var connection = await _dbContext.MailboxConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId);

        if (connection == null)
        {
            return NotFound(new { error = "Connection not found" });
        }

        if (!callerRole.IsCompanyAdminOrAbove() && connection.UserId != actingUserId)
        {
            return StatusCode(403, new { error = "You do not have permission to manage this mailbox." });
        }

        if (connection.CredentialId == null)
        {
            return BadRequest(new { error = "NO_CREDENTIAL", message = "No credential linked to this mailbox connection." });
        }

        var decrypted = await _mailboxCredentialService.GetDecryptedTokensAsync(connection.CredentialId.Value);
        if (decrypted != null && !decrypted.Value.accessToken.StartsWith("mock_access_token_"))
        {
            var provider = connection.Provider.ToLowerInvariant();
            var revokeUrl = provider == "gmail"
                ? $"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(decrypted.Value.refreshToken)}"
                : null;

            if (!string.IsNullOrEmpty(revokeUrl))
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    await httpClient.PostAsync(revokeUrl, null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to revoke tokens on provider side for {Provider} (best-effort)", provider);
                }
            }
        }

        if (!string.IsNullOrEmpty(connection.N8nWorkflowId))
        {
            BackgroundJob.Enqueue(() => CleanupN8nWorkflowJob(connection.N8nWorkflowId, connection.N8nCredentialId));
            connection.N8nCredentialId = null;
            connection.N8nWorkflowId = null;
        }

        var credId = connection.CredentialId.Value;
        await _mailboxCredentialService.DeleteCredentialAsync(credId);

        return NoContent();
    }

    [NonAction]
    [Hangfire.JobDisplayName("Provision n8n Workflow for Mailbox")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task ProvisionN8nWorkflowJob(Guid connectionId, Guid tenantId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var n8nProvisioning = scope.ServiceProvider.GetRequiredService<IN8nProvisioningService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MailboxConnectionsController>>();

        var connection = await dbContext.MailboxConnections.FindAsync(connectionId);
        if (connection == null)
        {
            logger.LogWarning("Connection {ConnectionId} not found for provisioning", connectionId);
            return;
        }

        try
        {
            var templateWorkflowId = Environment.GetEnvironmentVariable("N8N_TEMPLATE_WORKFLOW_ID") ?? "default-email-sync";
            object credentialPayload = new { };

            if (connection.CredentialId != null)
            {
                var credService = scope.ServiceProvider.GetRequiredService<IMailboxCredentialService>();
                var tokens = await credService.GetDecryptedTokensAsync(connection.CredentialId.Value);
                if (tokens != null)
                {
                    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var providerLower = connection.Provider.ToLowerInvariant();
                    if (providerLower == "gmail")
                    {
                        var clientId = config["Mailbox:Google:ClientId"] ?? config["MAILBOX_GOOGLE_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_ID");
                        var clientSecret = config["Mailbox:Google:ClientSecret"] ?? config["MAILBOX_GOOGLE_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_GOOGLE_CLIENT_SECRET");
                        
                        if (tokens.Value.accessToken.StartsWith("mock_access_token_"))
                        {
                            clientId = "mock_client_id";
                            clientSecret = "mock_client_secret";
                        }

                        credentialPayload = new
                        {
                            clientId,
                            clientSecret,
                            authUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                            accessTokenUrl = "https://oauth2.googleapis.com/token",
                            oauthTokenData = new
                            {
                                access_token = tokens.Value.accessToken,
                                refresh_token = tokens.Value.refreshToken,
                                expires_in = 3600,
                                token_type = "Bearer"
                            }
                        };
                    }
                    else if (providerLower == "outlook" || providerLower == "microsoft")
                    {
                        var clientId = config["Mailbox:Microsoft:ClientId"] ?? config["MAILBOX_MICROSOFT_CLIENT_ID"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_ID");
                        var clientSecret = config["Mailbox:Microsoft:ClientSecret"] ?? config["MAILBOX_MICROSOFT_CLIENT_SECRET"] ?? Environment.GetEnvironmentVariable("MAILBOX_MICROSOFT_CLIENT_SECRET");
                        
                        if (tokens.Value.accessToken.StartsWith("mock_access_token_"))
                        {
                            clientId = "mock_client_id";
                            clientSecret = "mock_client_secret";
                        }

                        credentialPayload = new
                        {
                            clientId,
                            clientSecret,
                            authUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                            accessTokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                            oauthTokenData = new
                            {
                                access_token = tokens.Value.accessToken,
                                refresh_token = tokens.Value.refreshToken,
                                expires_in = 3600,
                                token_type = "Bearer"
                            }
                        };
                    }
                }
            }

            try
            {
                var credentialId = await n8nProvisioning.CreateCredentialAsync(connection.Provider, connection.EmailAddress, credentialPayload);
                connection.N8nCredentialId = credentialId;
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Created n8n credential {CredentialId} for connection {ConnectionId}", credentialId, connectionId);
            }
            catch (Exception credEx)
            {
                logger.LogWarning(credEx, "Failed to create n8n credential for connection {ConnectionId}, continuing without credential", connectionId);
            }

            var workflowId = await n8nProvisioning.ProvisionWorkflowAsync(templateWorkflowId, connection.EmailAddress, tenantId);
            connection.N8nWorkflowId = workflowId;
            connection.IsActive = true;
            connection.ConnectedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Provisioned n8n credential {CredentialId} and workflow {WorkflowId} for connection {ConnectionId}",
                connection.N8nCredentialId, workflowId, connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to provision n8n workflow for connection {ConnectionId}", connectionId);
            connection.IsActive = false;
            await dbContext.SaveChangesAsync();
            throw;
        }
    }

    [NonAction]
    [Hangfire.JobDisplayName("Cleanup n8n Workflow")]
    public async Task CleanupN8nWorkflowJob(string? workflowId, string? credentialId)
    {
        using var scope = _serviceProvider.CreateScope();
        var n8nProvisioning = scope.ServiceProvider.GetRequiredService<IN8nProvisioningService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MailboxConnectionsController>>();

        if (!string.IsNullOrEmpty(workflowId))
        {
            await n8nProvisioning.DeleteWorkflowAsync(workflowId);
            logger.LogInformation("Deleted n8n workflow {WorkflowId}", workflowId);
        }

        if (!string.IsNullOrEmpty(credentialId))
        {
            await n8nProvisioning.DeleteCredentialAsync(credentialId);
            logger.LogInformation("Deleted n8n credential {CredentialId}", credentialId);
        }
    }
}

