using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/inbox/connections")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class MailboxConnectionsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<MailboxConnectionsController> _logger;

    public MailboxConnectionsController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<MailboxConnectionsController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
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

        connection.IsActive = request.IsActive;
        if (request.IsActive && connection.ConnectedAtUtc == null)
        {
            connection.ConnectedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

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

        _dbContext.MailboxConnections.Remove(connection);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted mailbox connection {ConnectionId} for tenant {TenantId}",
            connectionId, tenantId);

        return NoContent();
    }
}
