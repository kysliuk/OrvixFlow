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
[Route("api/v1/inbox/settings")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class InboxSettingsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<InboxSettingsController> _logger;

    public InboxSettingsController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<InboxSettingsController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // WorkflowPolicy CRUD

    public class PolicyRequest
    {
        [Required]
        public string Category { get; set; } = string.Empty;
        public bool AutoExecute { get; set; }
        public decimal ConfidenceThreshold { get; set; } = 0.85m;
        public string ExcludedKeywords { get; set; } = string.Empty;
    }

    public class PolicyResponse
    {
        public Guid Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool AutoExecute { get; set; }
        public decimal ConfidenceThreshold { get; set; }
        public string ExcludedKeywords { get; set; } = string.Empty;
    }

    [HttpGet("policies")]
    public async Task<IActionResult> GetPolicies()
    {
        var tenantId = _tenantProvider.GetTenantId();
        var policies = await _dbContext.WorkflowPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new PolicyResponse
            {
                Id = p.Id,
                Category = p.Category,
                AutoExecute = p.AutoExecute,
                ConfidenceThreshold = p.ConfidenceThreshold,
                ExcludedKeywords = p.ExcludedKeywords
            })
            .ToListAsync();

        return Ok(policies);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy([FromBody] PolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { error = "Category is required" });
        }

        var tenantId = _tenantProvider.GetTenantId();

        var existing = await _dbContext.WorkflowPolicies
            .AnyAsync(p => p.TenantId == tenantId && p.Category == request.Category);

        if (existing)
        {
            return Conflict(new { error = $"Policy for category '{request.Category}' already exists" });
        }

        var policy = new WorkflowPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = request.Category,
            AutoExecute = request.AutoExecute,
            ConfidenceThreshold = request.ConfidenceThreshold,
            ExcludedKeywords = request.ExcludedKeywords ?? string.Empty
        };

        _dbContext.WorkflowPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created workflow policy {PolicyId} for tenant {TenantId}", policy.Id, tenantId);

        return CreatedAtAction(nameof(GetPolicies), new PolicyResponse
        {
            Id = policy.Id,
            Category = policy.Category,
            AutoExecute = policy.AutoExecute,
            ConfidenceThreshold = policy.ConfidenceThreshold,
            ExcludedKeywords = policy.ExcludedKeywords
        });
    }

    [HttpPut("policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy(Guid policyId, [FromBody] PolicyRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var policy = await _dbContext.WorkflowPolicies
            .FirstOrDefaultAsync(p => p.Id == policyId && p.TenantId == tenantId);

        if (policy == null)
        {
            return NotFound(new { error = "Policy not found" });
        }

        policy.Category = request.Category;
        policy.AutoExecute = request.AutoExecute;
        policy.ConfidenceThreshold = request.ConfidenceThreshold;
        policy.ExcludedKeywords = request.ExcludedKeywords ?? string.Empty;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated workflow policy {PolicyId} for tenant {TenantId}", policyId, tenantId);

        return Ok(new PolicyResponse
        {
            Id = policy.Id,
            Category = policy.Category,
            AutoExecute = policy.AutoExecute,
            ConfidenceThreshold = policy.ConfidenceThreshold,
            ExcludedKeywords = policy.ExcludedKeywords
        });
    }

    [HttpDelete("policies/{policyId:guid}")]
    public async Task<IActionResult> DeletePolicy(Guid policyId)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var policy = await _dbContext.WorkflowPolicies
            .FirstOrDefaultAsync(p => p.Id == policyId && p.TenantId == tenantId);

        if (policy == null)
        {
            return NotFound(new { error = "Policy not found" });
        }

        _dbContext.WorkflowPolicies.Remove(policy);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted workflow policy {PolicyId} for tenant {TenantId}", policyId, tenantId);

        return NoContent();
    }

    // AgentPersona

    public class PersonaRequest
    {
        public string Tone { get; set; } = "Professional";
        public string CustomInstructions { get; set; } = string.Empty;
        public string? CustomSignOff { get; set; }
    }

    public class PersonaResponse
    {
        public Guid Id { get; set; }
        public string Tone { get; set; } = string.Empty;
        public string CustomInstructions { get; set; } = string.Empty;
        public string? CustomSignOff { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    [HttpGet("persona")]
    public async Task<IActionResult> GetPersona()
    {
        var tenantId = _tenantProvider.GetTenantId();

        var persona = await _dbContext.AgentPersonas
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);

        if (persona == null)
        {
            return Ok(new PersonaResponse
            {
                Tone = "Professional",
                CustomInstructions = string.Empty,
                CustomSignOff = null,
                UpdatedAtUtc = DateTime.MinValue
            });
        }

        return Ok(new PersonaResponse
        {
            Id = persona.Id,
            Tone = persona.Tone,
            CustomInstructions = persona.CustomInstructions,
            CustomSignOff = persona.CustomSignOff,
            UpdatedAtUtc = persona.UpdatedAtUtc
        });
    }

    [HttpPut("persona")]
    public async Task<IActionResult> UpdatePersona([FromBody] PersonaRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var persona = await _dbContext.AgentPersonas
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);

        if (persona == null)
        {
            persona = new AgentPersona
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Tone = request.Tone,
                CustomInstructions = request.CustomInstructions ?? string.Empty,
                CustomSignOff = request.CustomSignOff,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.AgentPersonas.Add(persona);
        }
        else
        {
            persona.Tone = request.Tone;
            persona.CustomInstructions = request.CustomInstructions ?? string.Empty;
            persona.CustomSignOff = request.CustomSignOff;
            persona.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated agent persona for tenant {TenantId}", tenantId);

        return Ok(new PersonaResponse
        {
            Id = persona.Id,
            Tone = persona.Tone,
            CustomInstructions = persona.CustomInstructions,
            CustomSignOff = persona.CustomSignOff,
            UpdatedAtUtc = persona.UpdatedAtUtc
        });
    }
}
