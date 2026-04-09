using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public MailboxConnectionsController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        IN8nProvisioningService n8nProvisioning,
        IServiceProvider serviceProvider,
        ILogger<MailboxConnectionsController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _n8nProvisioning = n8nProvisioning;
        _serviceProvider = serviceProvider;
        _logger = logger;
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
                ConnectedAtUtc = c.ConnectedAtUtc
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
            ConnectedAtUtc = connection.ConnectedAtUtc
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
                    ConnectedAtUtc = connection.ConnectedAtUtc
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
            ConnectedAtUtc = connection.ConnectedAtUtc
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

        _dbContext.MailboxConnections.Remove(connection);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted mailbox connection {ConnectionId} for tenant {TenantId}",
            connectionId, tenantId);

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

            try
            {
                var credentialId = await n8nProvisioning.CreateCredentialAsync(connection.Provider, connection.EmailAddress, new { });
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
