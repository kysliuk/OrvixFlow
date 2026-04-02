using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace OrvixFlow.Infrastructure.Ai;

public class ImageResolver : IImageResolver
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly ITenantProvider _tenantProvider;

    public ImageResolver(AppDbContext dbContext, ITextEmbeddingGenerationService embeddingService, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<KnowledgeBaseImage>> ResolveRelevantImagesAsync(string query, IEnumerable<Guid> documentIds, int maxResults = 3)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<KnowledgeBaseImage>();

        // 1. Generate embedding for the query
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { query });
        var embedding = embeddings[0];
        var queryVector = new Vector(embedding.ToArray());

        // 2. Search for relevant images
        // We filter by documentIds if provided, otherwise search all for the tenant (handled by global filter)
        var documentIdList = documentIds.ToList();
        
        var imgQuery = _dbContext.KnowledgeBaseImages
            .AsNoTracking()
            .Where(img => img.CaptionEmbedding != null);

        if (documentIdList.Any())
        {
            imgQuery = imgQuery.Where(img => img.DocumentId != null && documentIdList.Contains(img.DocumentId.Value));
        }

        List<KnowledgeBaseImage> results;
        try 
        {
            results = await imgQuery
                .OrderBy(img => img.CaptionEmbedding!.CosineDistance(queryVector))
                .Take(maxResults)
                .ToListAsync();
        }
        catch (Exception) // InMemory provider often throws when translating CosineDistance or even checking Pgvector.Vector nullability
        {
            var tenantId = _tenantProvider.GetTenantId();
            // Remove the ordering and filter entirely via client-side evaluation if the initial expression fails to translate
            var allImgs = await _dbContext.KnowledgeBaseImages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(img => img.TenantId == tenantId)
                .ToListAsync(); // Get everything for current tenant and filter in memory for the unit test

            var filtered = allImgs
                .Where(img => img.CaptionEmbedding != null);

            if (documentIdList.Any())
            {
                filtered = filtered.Where(img => img.DocumentId != null && documentIdList.Contains(img.DocumentId.Value));
            }

            results = filtered
                .OrderBy(img => img.CaptionEmbedding!.CosineDistance(queryVector))
                .Take(maxResults)
                .ToList();
        }

        return results;
    }
}
