using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using Pgvector.EntityFrameworkCore;

namespace OrvixFlow.Infrastructure.Ai;

public interface IHybridVectorSearchService
{
    Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(string query, int maxResults = 5);
}

public class HybridVectorSearchService : IHybridVectorSearchService
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private const float SimilarityThreshold = 0.5f;

    public HybridVectorSearchService(
        AppDbContext dbContext,
        ITextEmbeddingGenerationService embeddingService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(string query, int maxResults = 5)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(query);

        var results = await _dbContext.KnowledgeBases
            .Where(k => k.EmbeddingVector != null)
            .OrderBy(k => k.EmbeddingVector.L2Distance(new Pgvector.Vector(embedding.ToArray())))
            .Take(maxResults * 2)
            .Select(k => new
            {
                k.Id,
                k.RawContent,
                k.Metadata,
                Distance = k.EmbeddingVector.L2Distance(new Pgvector.Vector(embedding.ToArray()))
            })
            .ToListAsync();

        var snippets = results
            .Select(r => new KnowledgeSnippet
            {
                Id = r.Id,
                Content = r.RawContent,
                Metadata = r.Metadata,
                SimilarityScore = ConvertDistanceToSimilarity(r.Distance)
            })
            .Where(s => s.SimilarityScore >= SimilarityThreshold)
            .OrderByDescending(s => s.SimilarityScore)
            .Take(maxResults)
            .ToList();

        return snippets;
    }

    private static float ConvertDistanceToSimilarity(double l2Distance)
    {
        var similarity = 1.0f - (float)Math.Min(1.0, Math.Max(0.0, l2Distance / 2.0));
        return similarity;
    }
}
