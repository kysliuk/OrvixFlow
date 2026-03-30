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
    private readonly IUsageService _usageService;

    public IngestionService(
        AppDbContext dbContext,
        ITextEmbeddingGenerationService embeddingService,
        ITenantProvider tenantProvider,
        IUsageService usageService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _tenantProvider = tenantProvider;
        _usageService = usageService;
    }

    public Task IngestTextAsync(string content)
    {
        return IngestTextAsync(content, null, null);
    }

    public async Task IngestTextAsync(string content, Guid? userId = null, Guid? departmentId = null)
    {
        // 1. Generate the embedding vector using OpenAI
        var embedding = await _embeddingService.GenerateEmbeddingAsync(content);
        var vector = new Pgvector.Vector(embedding.ToArray());

        // 2. Get the current tenant scope
        var tenantId = _tenantProvider.GetTenantId();
        var companyId = tenantId;

        // 3. Estimate storage used (rough: ~4 bytes per dimension * 1536 dimensions + content)
        var estimatedStorageMb = (1536 * 4 + content.Length) / (1024 * 1024);

        // 4. Create the knowledge base record
        var kbRecord = new KnowledgeBase
        {
            TenantId = tenantId,
            RawContent = content,
            EmbeddingVector = vector
        };

        _dbContext.KnowledgeBases.Add(kbRecord);
        await _dbContext.SaveChangesAsync();

        // 5. Track usage
        await _usageService.RecordKnowledgeBaseAsync(companyId, "knowledge-base", 1, userId, departmentId);
        await _usageService.RecordStorageAsync(companyId, "knowledge-base", estimatedStorageMb, userId, departmentId);
    }
}
