using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using Pgvector;
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
    private readonly IReranker? _reranker;
    private readonly IImageResolver? _imageResolver;
    
    // We can tune RRF constant k
    private const int RrfK = 60;

    public HybridVectorSearchService(
        AppDbContext dbContext,
        ITextEmbeddingGenerationService embeddingService,
        IReranker? reranker = null,
        IImageResolver? imageResolver = null)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _reranker = reranker;
        _imageResolver = imageResolver;
    }

    public async Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(string query, int maxResults = 5)
    {
        // 1. Semantic Search (Dense Vector) - Use Cosine Distance
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { query });
        var embedding = embeddings[0];
        var queryVector = new Vector(embedding.ToArray());

        // We fetch more results initially to perform RRF and reranking
        var fetchCount = maxResults * 10;

        var vectorQuery = await _dbContext.KnowledgeBases
            .Where(k => k.EmbeddingVector != null)
            .OrderBy(k => k.EmbeddingVector!.CosineDistance(queryVector))
            .Take(fetchCount)
            .Select(k => new
            {
                k.Id,
                k.RawContent,
                k.Metadata,
                k.Title,
                k.ChunkType,
                k.DocumentId,
                // Inverse distance as rough similarity
                SimilarityScore = 1.0 - k.EmbeddingVector!.CosineDistance(queryVector) 
            })
            .ToListAsync();

        // 2. Full-Text Search (Sparse)
        // EF Core 9 pgvector + Npgsql allows simple raw SQL or using EF.Functions for plain_to_tsquery
        
        var ftsQuery = await _dbContext.KnowledgeBases
            .Where(k => EF.Functions.ToTsVector("english", k.Title + " " + k.RawContent)
                        .Matches(EF.Functions.PlainToTsQuery("english", query)))
            // order by rank
            .OrderByDescending(k => EF.Functions.ToTsVector("english", k.Title + " " + k.RawContent)
                                    .Rank(EF.Functions.PlainToTsQuery("english", query)))
            .Take(fetchCount)
            .Select(k => new
            {
                k.Id,
                k.RawContent,
                k.Metadata,
                k.Title,
                k.ChunkType,
                k.DocumentId,
                // Assign a placeholder score, Rank gives something > 0
                SimilarityScore = 0.5
            })
            .ToListAsync();

        // 3. Reciprocal Rank Fusion (RRF)
        var dict = new Dictionary<Guid, KnowledgeSnippet>();
        
        // Populate base snippets
        foreach (var item in vectorQuery.Concat(ftsQuery))
        {
            if (!dict.ContainsKey(item.Id))
            {
                dict[item.Id] = new KnowledgeSnippet
                {
                    Id = item.Id,
                    Content = item.RawContent,
                    Metadata = item.Metadata,
                    Title = item.Title,
                    ChunkType = item.ChunkType,
                    DocumentId = item.DocumentId,
                    SimilarityScore = 0 // Will hold RRF score
                };
            }
        }

        // Apply RRF scores
        for (int i = 0; i < vectorQuery.Count; i++)
        {
            var rank = i + 1;
            dict[vectorQuery[i].Id].SimilarityScore += (float)(1.0 / (RrfK + rank));
        }
        
        for (int i = 0; i < ftsQuery.Count; i++)
        {
            var rank = i + 1;
            dict[ftsQuery[i].Id].SimilarityScore += (float)(1.0 / (RrfK + rank));
        }

        var topRrfResults = dict.Values
            .OrderByDescending(s => s.SimilarityScore)
            .Take(maxResults * 3) // Give reranker some candidates
            .ToList();

        var finalResults = _reranker != null && topRrfResults.Count > 0
            ? await _reranker.RerankAsync(query, topRrfResults)
            : topRrfResults;

        var topFinal = finalResults.Take(maxResults).ToList();

        // 5. Resolve related images
        if (_imageResolver != null && topFinal.Count > 0)
        {
            var docIds = topFinal.Where(x => x.DocumentId.HasValue).Select(x => x.DocumentId!.Value).Distinct();
            var images = await _imageResolver.ResolveRelevantImagesAsync(query, docIds, 5);

            foreach (var snippet in topFinal)
            {
                if (snippet.DocumentId.HasValue)
                {
                    var relevantImages = images
                        .Where(img => img.DocumentId == snippet.DocumentId.Value)
                        .Select(img => new KnowledgeImageRef(img.Id, img.AltText, img.StoragePath))
                        .ToList();
                    
                    snippet.RelatedImages = relevantImages;
                }
            }
        }

        return topFinal;
    }
}
