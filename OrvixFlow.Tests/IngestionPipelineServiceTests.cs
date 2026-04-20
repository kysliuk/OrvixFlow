using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class IngestionPipelineServiceTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingMock;
    private readonly Mock<ITenantProvider> _tenantProviderMock;
    private readonly Mock<IUsageService> _usageServiceMock;
    private readonly Mock<IFileStorage> _storageMock;
    private readonly Mock<IChunker> _chunkerMock;
    private readonly Mock<IChatCompletionService> _chatCompletionMock;
    private readonly Mock<IRagMetricsCollector> _metricsMock;
    private readonly Mock<ILogger<IngestionPipelineService>> _loggerMock;
    private readonly IConfiguration _configuration;


    public IngestionPipelineServiceTests()
    {
        _embeddingMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        _tenantProviderMock = new Mock<ITenantProvider>();
        _usageServiceMock = new Mock<IUsageService>();
        _storageMock = new Mock<IFileStorage>();
        _chunkerMock = new Mock<IChunker>();
        _chatCompletionMock = new Mock<IChatCompletionService>();
        _metricsMock = new Mock<IRagMetricsCollector>();
        _loggerMock = new Mock<ILogger<IngestionPipelineService>>();

        
        var inMemoryConfig = new Dictionary<string, string> {
            {"AI:Ingestion:ChunkSize", "800"},
            {"AI:Ingestion:ChunkOverlap", "150"},
            {"Storage:Provider", "MinIO"},
            {"Storage:MinIO:Bucket", "orvixflow"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig!)
            .Build();

        _tenantProviderMock.Setup(x => x.GetTenantId()).Returns(Guid.NewGuid());
        _embeddingMock.Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[1536])]));

        _chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, "Mock Caption") });

        _storageMock.Setup(x => x.SaveFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync("mock/path");
    }

    [Fact]
    public async Task IngestFileAsync_ImageChunk_CreatesStoredObjectMetadata()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        _tenantProviderMock.Setup(x => x.GetTenantId()).Returns(tenantId);

        var parserMock = new Mock<IDocumentParser>();
        parserMock.Setup(p => p.CanParse("application/pdf")).Returns(true);
        parserMock.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "test.pdf"))
            .ReturnsAsync(new ParsedDocument(
                "test",
                new List<TextChunk>(),
                new List<ImageChunk>
                {
                    new(1, new byte[] { 1, 2, 3, 4 }, "image/png", "A figure")
                }));

        using var dbContext = new AppDbContext(options, _tenantProviderMock.Object);
        dbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/test.pdf",
            Status = "Pending"
        });
        await dbContext.SaveChangesAsync();

        var documentId = dbContext.KnowledgeBaseDocuments.Single().Id;
        var service = new IngestionPipelineService(
            dbContext,
            new[] { parserMock.Object },
            _chunkerMock.Object,
            _embeddingMock.Object,
            _storageMock.Object,
            _tenantProviderMock.Object,
            _usageServiceMock.Object,
            _configuration,
            _chatCompletionMock.Object,
            _metricsMock.Object,
            _loggerMock.Object);

        using var stream = new MemoryStream(new byte[] { 9, 8, 7, 6 });

        var result = await service.IngestFileAsync(stream, "test.pdf", "application/pdf", documentId, tenantId, userId, departmentId);

        Assert.Null(result.ErrorMessage);
        var storedObject = await dbContext.StoredObjects.SingleAsync();
        storedObject.TenantId.Should().Be(tenantId);
        storedObject.DepartmentId.Should().Be(departmentId);
        storedObject.Module.Should().Be("knowledge-base");
        storedObject.EntityType.Should().Be("image");
        storedObject.EntityId.Should().Be(documentId);
        storedObject.StorageProvider.Should().Be("MinIO");
        storedObject.ContainerOrBucket.Should().Be("orvixflow");
        storedObject.StorageKey.Should().Be("mock/path");
        storedObject.OriginalFileName.Should().Be("img_1_test.pdf");
        storedObject.ContentType.Should().Be("image/png");
        storedObject.SizeBytes.Should().Be(4);
        storedObject.VirusScanStatus.Should().Be("Clean");
        storedObject.LifecycleStatus.Should().Be("Active");
        storedObject.CreatedByUserId.Should().Be(userId);
        storedObject.Sha256.Should().HaveLength(64);
    }

    [Fact(Skip = "EF Core InMemory provider has known issues with cross-context entity tracking. Tested manually with PostgreSQL.")]
    public async Task IngestFileAsync_ValidTxtFile_CreatesDocumentAndChunks()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var parserMock = new Mock<IDocumentParser>();
        parserMock.Setup(p => p.CanParse("text/plain")).Returns(true);
        parserMock.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "test.txt"))
            .ReturnsAsync(new ParsedDocument("test.txt", new List<TextChunk> { new TextChunk(0, "content", null) }, new List<ImageChunk>()));

        _chunkerMock.Setup(c => c.Chunk(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string> { "content_chunk1" });

        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content"));
        var testTenantId = Guid.NewGuid();
        _tenantProviderMock.Setup(x => x.GetTenantId()).Returns(testTenantId);

        // Use single context for InMemory compatibility
        using var dbContext = new AppDbContext(options, _tenantProviderMock.Object);
        var service = new IngestionPipelineService(
            dbContext,
            new[] { parserMock.Object },
            _chunkerMock.Object,
            _embeddingMock.Object,
            _storageMock.Object,
            _tenantProviderMock.Object,
            _usageServiceMock.Object,
            _configuration,
            _chatCompletionMock.Object,
            _metricsMock.Object,
            _loggerMock.Object
        );

        // Act
        var docId = Guid.NewGuid();
        var result = await service.IngestFileAsync(stream, "test.txt", "text/plain", docId, testTenantId);

        // Assert - verify within same context
        Assert.Null(result.ErrorMessage);
        Assert.NotEqual(Guid.Empty, result.DocumentId);
        Assert.Equal(1, result.ChunkCount);
        
        var doc = dbContext.KnowledgeBaseDocuments.Include(d => d.Chunks).First();
        Assert.Equal("Indexed", doc.Status);
        Assert.Single(doc.Chunks);
    }
}
