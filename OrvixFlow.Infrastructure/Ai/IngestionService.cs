using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Ai;

public class IngestionService : IIngestionService
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly ITenantProvider _tenantProvider;

    public IngestionService(
        AppDbContext dbContext,
        ITextEmbeddingGenerationService embeddingService,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _tenantProvider = tenantProvider;
    }

    public async Task IngestTextAsync(string content)
    {
        // 1. Generate the embedding vector using OpenAI
        var embedding = await _embeddingService.GenerateEmbeddingAsync(content);
        var vector = new Pgvector.Vector(embedding.ToArray());

        // 2. Get the current tenant scope
        var tenantId = _tenantProvider.GetTenantId();

        // 3. Create the knowledge base record
        var kbRecord = new KnowledgeBase
        {
            TenantId = tenantId,
            RawContent = content,
            EmbeddingVector = vector
        };

        _dbContext.KnowledgeBases.Add(kbRecord);
        await _dbContext.SaveChangesAsync();
    }
}
