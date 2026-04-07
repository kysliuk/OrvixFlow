using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;

namespace OrvixFlow.Api.Controllers;

[ApiController]
[Route("api/v1/knowledge")]
[Authorize]
public class RagDebugController : ControllerBase
{
    private readonly IHybridVectorSearchService _searchService;
    private readonly ITenantProvider _tenantProvider;

    public RagDebugController(
        IHybridVectorSearchService searchService,
        ITenantProvider tenantProvider)
    {
        _searchService = searchService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("debug-search")]
    public async Task<IActionResult> DebugSearch([FromQuery] string q, [FromQuery] int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest("Query parameter 'q' is required");
        }

        var tenantId = _tenantProvider.GetTenantId();
        
        var results = await _searchService.SearchAsync(q, maxResults);

        return Ok(new
        {
            query = q,
            tenantId = tenantId,
            count = results.Count,
            results = results.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                similarityScore = r.SimilarityScore,
                documentId = r.DocumentId,
                chunkType = r.ChunkType,
                preview = r.Content.Length > 200 ? r.Content[..200] + "..." : r.Content
            }).ToList()
        });
    }
}
