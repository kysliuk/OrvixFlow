using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace OrvixFlow.Infrastructure.Ai;

public class IngestionPipelineService : IIngestionPipelineService
{
    private readonly AppDbContext _dbContext;
    private readonly IEnumerable<IDocumentParser> _parsers;
    private readonly IChunker _chunker;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IFileStorage _storage;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUsageService _usageService;
    private readonly IConfiguration _configuration;
    private readonly Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService _chatService;
    private readonly IRagMetricsCollector _metrics;
    private readonly ILogger<IngestionPipelineService> _logger;


    public IngestionPipelineService(
        AppDbContext dbContext,
        IEnumerable<IDocumentParser> parsers,
        IChunker chunker,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IFileStorage storage,
        ITenantProvider tenantProvider,
        IUsageService usageService,
        IConfiguration configuration,
        Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService chatService,
        IRagMetricsCollector metrics,
        ILogger<IngestionPipelineService> logger)
    {
        _dbContext = dbContext;
        _parsers = parsers;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _usageService = usageService;
        _configuration = configuration;
        _chatService = chatService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        Guid tenantId,
        Guid? userId = null,
        Guid? departmentId = null)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting ingestion for file: {FileName} (DocID: {DocumentId}, Tenant: {TenantId})", fileName, documentId, tenantId);

        // Fetch existing document created by controller, or create new if not found
        var document = await _dbContext.KnowledgeBaseDocuments.FindAsync(documentId);
        
        if (document == null)
        {
            // Create new document (either backward-compat mode or document not yet created)
            document = new KnowledgeBaseDocument
            {
                Id = documentId,
                TenantId = tenantId,
                FileName = fileName,
                ContentType = contentType,
                FileSizeBytes = fileStream.Length,
                Status = "Processing"
            };
            _dbContext.KnowledgeBaseDocuments.Add(document);
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            // Update existing document created by controller
            document.Status = "Processing";
            document.ContentType = contentType;
            document.FileSizeBytes = fileStream.Length;
            await _dbContext.SaveChangesAsync();
        }

        try
        {
            // 1. Resolve parser
            var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType));
            if (parser == null)
            {
                throw new NotSupportedException($"No parser found for content type: {contentType}");
            }

            // 2. Parse
            fileStream.Position = 0; // Reset for parsing
            var parsedDoc = await parser.ParseAsync(fileStream, fileName);

            // 3. Chunk
            var chunkSize = _configuration.GetValue("AI:Ingestion:ChunkSize", 800);
            var overlapSize = _configuration.GetValue("AI:Ingestion:ChunkOverlap", 150);
            int chunkIndex = 0;

            foreach (var textChunk in parsedDoc.TextChunks)
            {
                var chunks = _chunker.Chunk(textChunk.Content, chunkSize, overlapSize);
                foreach (var chunkText in chunks)
                {
                    // 4. Embed with retry
                    var embedding = await ExecuteWithRetryAsync(
                        async () =>
                        {
                            var embeddings = await _embeddingGenerator.GenerateAsync(new[] { chunkText });
                            return embeddings[0];
                        },
                        $"embedding for chunk {chunkIndex}");
                    
                    var kbChunk = new KnowledgeBase
                    {
                        TenantId = tenantId,
                        DocumentId = document.Id,
                        RawContent = chunkText,
                        ChunkIndex = chunkIndex++,
                        ChunkType = "text",
                        Title = parsedDoc.Title,
                        EmbeddingVector = new Pgvector.Vector(embedding.Vector.Span.ToArray())
                    };
                    document.Chunks.Add(kbChunk);
                }
            }

            // 5. Save chunks

            // 6. Process Images
            foreach (var imageChunk in parsedDoc.ImageChunks)
            {
                using var imageMs = new MemoryStream(imageChunk.Data);
                var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, $"img_{imageChunk.Index}_{fileName}", imageMs);

                var caption = imageChunk.Caption;
                if (string.IsNullOrWhiteSpace(caption))
                {
                    // Generate descriptive caption using Vision with retry
                    caption = await ExecuteWithRetryAsync(
                        async () => await GenerateImageCaptionAsync(imageChunk.Data, imageChunk.ContentType, fileName),
                        $"image caption for {fileName}");
                }

                var captionEmbedding = await ExecuteWithRetryAsync(
                    async () =>
                    {
                        var embeddings = await _embeddingGenerator.GenerateAsync(new[] { caption });
                        return embeddings[0];
                    },
                    $"caption embedding for {fileName}");

                var kbImage = new KnowledgeBaseImage
                {
                    TenantId = tenantId,
                    DocumentId = document.Id,
                    StoragePath = imagePath,
                    ContentType = imageChunk.ContentType,
                    AltText = caption,
                    Caption = caption,
                    CaptionEmbedding = new Pgvector.Vector(captionEmbedding.Vector.Span.ToArray())
                };

                _dbContext.KnowledgeBaseImages.Add(kbImage);
            }
            
            document.Status = "Indexed";
            document.IndexedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // 7. Usage tracking
            var totalStorageMb = (int)((fileStream.Length + document.Chunks.Count * 1536 * 4) / (1024 * 1024));
            await _usageService.RecordKnowledgeBaseAsync(tenantId, "knowledge-base", 1, userId, departmentId);
            await _usageService.RecordStorageAsync(tenantId, "knowledge-base", totalStorageMb, userId, departmentId);

            sw.Stop();
            await _metrics.RecordIngestionMetricsAsync(
                tenantId, 
                document.Id, 
                document.Chunks.Count, 
                parsedDoc.ImageChunks.Count, 
                sw.ElapsedMilliseconds);

            _logger.LogInformation("Ingestion completed for {DocumentId} in {Duration}ms", document.Id, sw.ElapsedMilliseconds);

            return new IngestionResult(document.Id, document.Chunks.Count, parsedDoc.ImageChunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion failed for {FileName}", fileName);
            document.Status = "Failed";
            document.ErrorMessage = ex.Message;
            try 
            {
                if (_dbContext.Entry(document).State == EntityState.Detached)
                {
                    _dbContext.KnowledgeBaseDocuments.Attach(document);
                }
                _dbContext.Entry(document).State = EntityState.Modified;
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                // Last resort if context is totally corrupted
            }
            return new IngestionResult(document.Id, 0, 0, ex.Message);
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3, int baseDelaySeconds = 2)
    {
        var lastException = new Exception("Operation failed");
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase))
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt - 1));
                _logger.LogWarning("Rate limited on {OperationName} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s", operationName, attempt, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
        _logger.LogError(lastException, "All retries exhausted for {OperationName}", operationName);
        throw lastException;
    }

    private async Task<string> GenerateImageCaptionAsync(byte[] imageData, string contentType, string fileName)
    {
        try
        {
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddSystemMessage("You are a vision-capable assistant. Describe the provided image concisely for a RAG knowledge base. Focus on technical details, text visible, and context.");
            
            var message = new ChatMessageContent(AuthorRole.User, "Describe this image.");
            message.Items.Add(new ImageContent(new ReadOnlyMemory<byte>(imageData), contentType));
            chatHistory.Add(message);

            var result = await _chatService.GetChatMessageContentAsync(chatHistory, kernel: null);
            return result.Content ?? $"Image from {fileName}";
        }
        catch (Exception)
        {
            // Fallback if vision is not supported or fails
            return $"Image from {fileName}";
        }
    }
}
