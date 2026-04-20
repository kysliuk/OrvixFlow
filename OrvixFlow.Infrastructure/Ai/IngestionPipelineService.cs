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
using System.Security.Cryptography;

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

        // Fetch existing document created by controller (bypass tenant filter since we have the ID)
        var document = await _dbContext.KnowledgeBaseDocuments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == documentId);
        
        _logger.LogInformation("Document lookup result: {Found}, TenantId: {DocTenantId}", document != null, document?.TenantId);
        
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
            _logger.LogInformation("Created new document record for {DocumentId}", documentId);
        }
        else
        {
            // Update existing document created by controller
            document.Status = "Processing";
            document.ContentType = contentType;
            document.FileSizeBytes = fileStream.Length;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Updated existing document status to Processing");
        }

        // Clear any existing chunks for this document (handles retries/duplicates)
        var existingChunks = await _dbContext.KnowledgeBases
            .Where(k => k.DocumentId == document.Id)
            .ToListAsync();
        if (existingChunks.Any())
        {
            _logger.LogInformation("Clearing " + existingChunks.Count + " existing chunks before re-processing");
            _dbContext.KnowledgeBases.RemoveRange(existingChunks);
            document.Chunks.Clear();
        }

        try
        {
            // 1. Resolve parser
            var parser = _parsers.FirstOrDefault(p => p.CanParse(contentType));
            if (parser == null)
            {
                throw new NotSupportedException($"No parser found for content type: {contentType}");
            }
            _logger.LogInformation("Found parser: {Parser}", parser.GetType().Name);

            // 2. Parse
            fileStream.Position = 0;
            _logger.LogInformation("Parsing file...");
            var parsedDoc = await parser.ParseAsync(fileStream, fileName);
            _logger.LogInformation("Parsing complete. TextChunks: {TextChunks}, ImageChunks: {ImageChunks}", 
                parsedDoc.TextChunks.Count, parsedDoc.ImageChunks.Count);

            // 3. Chunk
            var chunkSize = _configuration.GetValue("AI:Ingestion:ChunkSize", 800);
            var overlapSize = _configuration.GetValue("AI:Ingestion:ChunkOverlap", 150);
            _logger.LogInformation("Chunking text with size {ChunkSize}, overlap {OverlapSize}", chunkSize, overlapSize);
            
            int chunkIndex = 0;
            int totalChunks = 0;

            foreach (var textChunk in parsedDoc.TextChunks)
            {
                var chunks = _chunker.Chunk(textChunk.Content, chunkSize, overlapSize).ToList();
                _logger.LogInformation("Text chunk split: index=" + textChunk.Index + ", count=" + chunks.Count);
                
                foreach (var chunkText in chunks)
                {
                    totalChunks++;
                    try
                    {
                        // 4. Embed with retry
                        _logger.LogInformation("Generating embedding for chunk number (num=" + chunkIndex + ", total=" + totalChunks + ")");
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
                        _dbContext.KnowledgeBases.Add(kbChunk);
                        _logger.LogInformation("Added chunk " + (chunkIndex - 1) + " with embedding");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process chunk {Index}: {Error}", chunkIndex, ex.Message);
                        throw;
                    }
                }
            }

            _logger.LogInformation("Total chunks created: {TotalChunks}", totalChunks);

            // 5. Process Images
            _logger.LogInformation("Processing {ImageCount} images...", parsedDoc.ImageChunks.Count);
            foreach (var imageChunk in parsedDoc.ImageChunks)
            {
                using var imageMs = new MemoryStream(imageChunk.Data);
                var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, $"img_{imageChunk.Index}_{fileName}", imageMs);

                var storedObjectForImage = new StoredObject
                {
                    TenantId = tenantId,
                    DepartmentId = departmentId,
                    Module = "knowledge-base",
                    EntityType = "image",
                    EntityId = document.Id,
                    StorageProvider = _configuration["Storage:Provider"] ?? "Local",
                    ContainerOrBucket = _configuration["Storage:MinIO:Bucket"] ?? "local",
                    StorageKey = imagePath,
                    OriginalFileName = $"img_{imageChunk.Index}_{fileName}",
                    ContentType = imageChunk.ContentType,
                    SizeBytes = imageChunk.Data.Length,
                    Sha256 = ComputeSha256(imageChunk.Data),
                    VirusScanStatus = "Clean",
                    LifecycleStatus = "Active",
                    CreatedByUserId = userId ?? Guid.Empty
                };

                _dbContext.StoredObjects.Add(storedObjectForImage);

                var caption = imageChunk.Caption;
                if (string.IsNullOrWhiteSpace(caption))
                {
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
            
            // 6. Save document and chunks
            _logger.LogInformation("Saving document and chunks to database...");
            document.Status = "Indexed";
            document.IndexedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {ChunkCount} chunks and {ImageCount} images", 
                document.Chunks.Count, parsedDoc.ImageChunks.Count);

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
            _logger.LogError(ex, "Ingestion failed for {FileName}: {Error}", fileName, ex.Message);
            
            // Try to update status to Failed using fresh query
            try
            {
                var docToUpdate = await _dbContext.KnowledgeBaseDocuments
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.Id == documentId);
                
                if (docToUpdate != null)
                {
                    docToUpdate.Status = "Failed";
                    docToUpdate.ErrorMessage = ex.Message;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Updated document status to Failed");
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update document status to Failed: {Error}", updateEx.Message);
            }
            
            return new IngestionResult(documentId, 0, 0, ex.Message);
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
            catch (Exception ex) when (
                ex.Message.Contains("429") || 
                ex.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("rate_limit_exceeded") ||
                ex.Message.Contains("TPM"))
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt - 1));
                _logger.LogWarning("Rate limited on {OperationName} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s. Error: {Error}", 
                    operationName, attempt, maxRetries, delay.TotalSeconds, ex.Message);
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
            return $"Image from {fileName}";
        }
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
