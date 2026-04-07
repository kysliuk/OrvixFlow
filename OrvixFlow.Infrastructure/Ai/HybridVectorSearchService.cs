using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IReranker? _reranker;
    private readonly IImageResolver? _imageResolver;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<HybridVectorSearchService> _logger;
    
    // We can tune RRF constant k
    private const int RrfK = 60;

    public HybridVectorSearchService(
        AppDbContext dbContext,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IReranker? reranker = null,
        IImageResolver? imageResolver = null,
        IConfiguration? configuration = null,
        ILogger<HybridVectorSearchService>? logger = null)
    {
        _dbContext = dbContext;
        _embeddingGenerator = embeddingGenerator;
        _reranker = reranker;
        _imageResolver = imageResolver;
        _configuration = configuration;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridVectorSearchService>.Instance;
    }

    public async Task<IReadOnlyList<KnowledgeSnippet>> SearchAsync(string query, int maxResults = 5)
    {
        _logger.LogInformation("[RAG] Search query: {Query}", query);

        // 1. Semantic Search (Dense Vector) - Use Cosine Distance
        var embeddings = await _embeddingGenerator.GenerateAsync(new[] { query });
        var embedding = embeddings[0];
        var queryVector = new Vector(embedding.Vector.Span.ToArray());
        _logger.LogInformation("[RAG] Generated embedding, vector dims: {Dims}", embedding.Vector.Span.Length);

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

        _logger.LogInformation("[RAG] Vector search: {Count} results, top score: {Score}", 
            vectorQuery.Count, vectorQuery.Any() ? vectorQuery.Max(v => v.SimilarityScore) : 0);

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

        // RRF scores are typically very small (0.01-0.05 range), so threshold needs to be much lower
        // Default to 0.0 to return results unless explicitly configured higher
        var minThreshold = _configuration?.GetValue("AI:Search:MinSimilarityThreshold", 0.0) ?? 0.0;
        
        // Apply document specificity boost: prioritize chunks from uploaded documents
        foreach (var snippet in dict.Values)
        {
            if (!string.IsNullOrEmpty(snippet.Title) && snippet.DocumentId.HasValue)
            {
                snippet.SimilarityScore *= 1.5f; // 50% boost for document-sourced chunks
            }
        }

        var topRrfResults = dict.Values
            .Where(s => s.SimilarityScore > minThreshold)
            .OrderByDescending(s => s.SimilarityScore)
            .Take(maxResults * 3) // Give reranker some candidates
            .ToList();

        var finalResults = _reranker != null && topRrfResults.Count > 0
            ? await _reranker.RerankAsync(query, topRrfResults)
            : topRrfResults;

        var topFinal = finalResults.Take(maxResults).ToList();

        _logger.LogInformation("[RAG] Final results: {Count} snippets returned", topFinal.Count);
        for (int i = 0; i < topFinal.Count; i++)
        {
            var s = topFinal[i];
            var preview = s.Content.Length > 100 ? s.Content[..100] + "..." : s.Content;
            _logger.LogInformation("[RAG] Result {Index}: Title='{Title}', Score={Score}, DocumentId={DocId}, ContentPreview='{Preview}'", 
                i + 1, s.Title, s.SimilarityScore, s.DocumentId, preview);
        }

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
