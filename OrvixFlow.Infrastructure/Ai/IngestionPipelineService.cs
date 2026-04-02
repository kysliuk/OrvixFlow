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
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

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
    private readonly Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService _chatService;


    public IngestionPipelineService(
        AppDbContext dbContext,
        IEnumerable<IDocumentParser> parsers,
        IChunker chunker,
        ITextEmbeddingGenerationService embeddingService,
        IFileStorage storage,
        ITenantProvider tenantProvider,
        IUsageService usageService,
        IConfiguration configuration,
        Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService chatService)
    {
        _dbContext = dbContext;
        _parsers = parsers;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _storage = storage;
        _tenantProvider = tenantProvider;
        _usageService = usageService;
        _configuration = configuration;
        _chatService = chatService;
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

        try
        {
            // 1. Save raw file
            var storagePath = await _storage.SaveFileAsync(tenantId, document.Id, fileName, fileStream);
            document.StoragePath = storagePath;

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
            var chunkSize = _configuration.GetValue("AI:Ingestion:ChunkSize", 800);
            var overlapSize = _configuration.GetValue("AI:Ingestion:ChunkOverlap", 150);
            int chunkIndex = 0;

            foreach (var textChunk in parsedDoc.TextChunks)
            {
                var chunks = _chunker.Chunk(textChunk.Content, chunkSize, overlapSize);
                foreach (var chunkText in chunks)
                {
                    // 5. Embed
                    var embeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { chunkText });
                    var embedding = embeddings[0];
                    
                    var kbChunk = new KnowledgeBase
                    {
                        TenantId = tenantId,
                        DocumentId = document.Id, // Use explicit ID
                        RawContent = chunkText,
                        ChunkIndex = chunkIndex++,
                        ChunkType = "text",
                        Title = parsedDoc.Title,
                        EmbeddingVector = new Pgvector.Vector(embedding.ToArray()),
                        StoragePath = document.StoragePath ?? string.Empty
                    };
                    document.Chunks.Add(kbChunk);
                }
            }

            // 6. Save chunks - they are in document.Chunks and document is already tracked.

            // 7. Process Images
            foreach (var imageChunk in parsedDoc.ImageChunks)
            {
                using var imageMs = new MemoryStream(imageChunk.Data);
                var imagePath = await _storage.SaveFileAsync(tenantId, document.Id, $"img_{imageChunk.Index}_{fileName}", imageMs);

                var caption = imageChunk.Caption;
                if (string.IsNullOrWhiteSpace(caption))
                {
                    // Generate descriptive caption using Vision
                    caption = await GenerateImageCaptionAsync(imageChunk.Data, imageChunk.ContentType, fileName);
                }

                var captionEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(new[] { caption });
                var captionEmbedding = captionEmbeddings[0];

                var kbImage = new KnowledgeBaseImage
                {
                    TenantId = tenantId,
                    DocumentId = document.Id, // Use explicit ID
                    StoragePath = imagePath,
                    ContentType = imageChunk.ContentType,
                    AltText = caption,
                    Caption = caption, // For now original and alt are same
                    CaptionEmbedding = new Pgvector.Vector(captionEmbedding.ToArray())
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

            return new IngestionResult(document.Id, document.Chunks.Count, parsedDoc.ImageChunks.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Ingestion failed: {ex.Message}");
            // If the context is dirty from a failed SaveChangesAsync (common with InMemory duplication errors),
            // trying to save again in the same context often fails with "item already added".
            _dbContext.Entry(document).State = EntityState.Modified;
            document.Status = "Failed";
            document.ErrorMessage = ex.Message;
            try 
            {
                await _dbContext.SaveChangesAsync();
            }
            catch
            {
                // Last resort if context is totally corrupted
            }
            return new IngestionResult(document.Id, 0, 0, ex.Message);
        }
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
