using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge")]
[Authorize]
public class KnowledgeBaseController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<KnowledgeBaseController> _logger;

    public KnowledgeBaseController(
        AppDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<KnowledgeBaseController> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public class KnowledgeBaseResponse
    {
        public Guid Id { get; set; }
        public string RawContent { get; set; } = string.Empty;
        public string Metadata { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }
    }

    public class ListRequest
    {
        public string? Search { get; set; }
        public int Limit { get; set; } = 50;
        public int Offset { get; set; } = 0;
    }

    [HttpGet]
    [EnableRateLimiting("ai-search")]
    public async Task<IActionResult> ListKnowledge([FromQuery] ListRequest request)
    {
        var tenantId = _tenantProvider.GetTenantId();
        
        var query = _dbContext.KnowledgeBases
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLowerInvariant();
            query = query.Where(k => k.RawContent.ToLower().Contains(searchLower) || 
                                     k.Metadata.ToLower().Contains(searchLower));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(k => k.Id)
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToListAsync();

        var result = items.Select(k => new
        {
            id = k.Id,
            rawContent = k.RawContent.Length > 500 ? k.RawContent[..500] + "..." : k.RawContent,
            metadata = k.Metadata,
            createdAt = k.CreatedAtUtc
        });

        return Ok(new
        {
            items = result,
            total = totalCount,
            limit = request.Limit,
            offset = request.Offset
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetKnowledge(Guid id)
    {
        var tenantId = _tenantProvider.GetTenantId();
        
        var item = await _dbContext.KnowledgeBases
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);

        if (item == null)
        {
            return NotFound(new { error = "Knowledge base entry not found" });
        }

        return Ok(new KnowledgeBaseResponse
        {
            Id = item.Id,
            RawContent = item.RawContent,
            Metadata = item.Metadata,
            CreatedAt = item.CreatedAtUtc
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteKnowledge(Guid id)
    {
        var tenantId = _tenantProvider.GetTenantId();
        
        var item = await _dbContext.KnowledgeBases
            .FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId);

        if (item == null)
        {
            return NotFound(new { error = "Knowledge base entry not found" });
        }

        _dbContext.KnowledgeBases.Remove(item);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted knowledge base entry {Id} for tenant {TenantId}", id, tenantId);

        return NoContent();
    }
}
