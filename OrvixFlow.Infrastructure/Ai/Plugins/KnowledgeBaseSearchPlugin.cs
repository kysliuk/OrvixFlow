using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Ai.Plugins;

public class KnowledgeBaseSearchPlugin
{
    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public KnowledgeBaseSearchPlugin(
        AppDbContext dbContext,
        ITextEmbeddingGenerationService embeddingService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
    }

    [KernelFunction("search_knowledge_base")]
    [Description("Searches the internal knowledge base for information matching the user's query. Useful for answering questions based on stored facts.")]
    public async Task<string> SearchAsync(
        [Description("The search query string to find related facts for")] string query)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(query);
        var queryVector = new Pgvector.Vector(embedding.ToArray());

        // EF Core global query filters automatically restrict results to the current scope's TenantId.
        var results = await _dbContext.KnowledgeBases
            .Where(k => k.EmbeddingVector != null)
            .OrderBy(k => k.EmbeddingVector!.L2Distance(queryVector))
            .Take(3)
            .Select(k => k.RawContent)
            .ToListAsync();

        if (results.Count == 0)
            return "No relevant information found in the knowledge base.";

        return string.Join("\n\n---\n\n", results);
    }
}
