using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Embeddings;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Ai;

public class IngestionPipelineService : IIngestionPipelineService
{
    private readonly AppDbContext _dbContext;
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IChunker _chunker;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUsageService _usageService;
    private readonly IConfiguration _configuration;

    public IngestionPipelineService(
        AppDbContext dbContext,
        IEnumerable<IDocumentParser> parsers,
        IChunker chunker,
        ITextEmbeddingGenerationService embeddingService,
        IFileStorage storage,
        ITenantProvider tenantProvider,
        IUsageService usageService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _parsers = parsers;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _usageService = usageService;
        _configuration = configuration;
    }

    public async Task<IngestionResult> IngestFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid? userId = null,
        Guid? departmentId = null)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var document = new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileStream.Length,
            Status = "Processing"
        };

        _dbContext.KnowledgeBaseDocuments.Add(document);
        await _dbContext.SaveChangesAsync();

        try
        {
            // 1. Save raw file
            var storagePath = await _storage.SaveFileAsync(tenantId, document.Id, fileName, fileStream);
            document.StoragePath = storagePath;
            await _dbContext.SaveChangesAsync();

            // 2. Resolve parser
            var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType));
            if (parser == null)
            {
                throw new NotSupportedException($"No parser found for content type: {contentType}");
            }

            // 3. Parse
            fileStream.Position = 0; // Reset for parsing
            var parsedDoc = await parser.ParseAsync(fileStream, fileName);

            // 4. Chunk
            var allChunks = new List<KnowledgeBase>();
            int chunkIndex = 0;
            
            var chunkSize = _configuration.GetValue("AI:Ingestion:ChunkSize", 800);
            var overlapSize = _configuration.GetValue("AI:Ingestion:ChunkOverlap", 150);

            foreach (var textChunk in parsedDoc.TextChunks)
            {
                var chunks = _chunker.Chunk(textChunk.Content, chunkSize, overlapSize);
                foreach (var chunkText in chunks)
                {
                    // 5. Embed
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(chunkText);
                    
                    var kbChunk = new KnowledgeBase
                    {
                        TenantId = tenantId,
                        DocumentId = document.Id,
                        RawContent = chunkText,
                        ChunkIndex = chunkIndex++,
                        ChunkType = "text",
                        Title = parsedDoc.Title,
                        EmbeddingVector = new Pgvector.Vector(embedding.ToArray())
                    };
                    allChunks.Add(kbChunk);
                }
            }

            // 6. Save chunks
            _dbContext.KnowledgeBases.AddRange(allChunks);
            
            document.Status = "Indexed";
            document.IndexedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // 7. Usage tracking
            var totalStorageMb = (int)((fileStream.Length + allChunks.Count * 1536 * 4) / (1024 * 1024));
            await _usageService.RecordKnowledgeBaseAsync(tenantId, "knowledge-base", 1, userId, departmentId);
            await _usageService.RecordStorageAsync(tenantId, "knowledge-base", totalStorageMb, userId, departmentId);

            return new IngestionResult(document.Id, allChunks.Count, parsedDoc.ImageChunks.Count);
        }
        catch (Exception ex)
        {
            document.Status = "Failed";
            document.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync();
            return new IngestionResult(document.Id, 0, 0, ex.Message);
        }
    }
}
