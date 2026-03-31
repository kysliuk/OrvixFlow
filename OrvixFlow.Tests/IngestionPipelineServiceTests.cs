using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class IngestionPipelineServiceTests
{
    private readonly Mock<ITextEmbeddingGenerationService> _embeddingMock;
    private readonly Mock<ITenantProvider> _tenantProviderMock;
    private readonly Mock<IUsageService> _usageServiceMock;
    private readonly Mock<IFileStorage> _storageMock;
    private readonly Mock<IChunker> _chunkerMock;
    private readonly IConfiguration _configuration;

    public IngestionPipelineServiceTests()
    {
        _embeddingMock = new Mock<ITextEmbeddingGenerationService>();
        _tenantProviderMock = new Mock<ITenantProvider>();
        _usageServiceMock = new Mock<IUsageService>();
        _storageMock = new Mock<IFileStorage>();
        _chunkerMock = new Mock<IChunker>();
        
        var inMemoryConfig = new Dictionary<string, string> {
            {"AI:Ingestion:ChunkSize", "800"},
            {"AI:Ingestion:ChunkOverlap", "150"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig!)
            .Build();

        _tenantProviderMock.Setup(x => x.GetTenantId()).Returns(Guid.NewGuid());
        _embeddingMock.Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<IList<string>>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(new float[1536]) });
    }

    [Fact]
    public async Task IngestFileAsync_ValidTxtFile_CreatesDocumentAndChunks()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new AppDbContext(options, _tenantProviderMock.Object);

        var parserMock = new Mock<IDocumentParser>();
        parserMock.Setup(p => p.CanParse("text/plain")).Returns(true);
        parserMock.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "test.txt"))
            .ReturnsAsync(new ParsedDocument("test.txt", new List<TextChunk> { new TextChunk(0, "content", null) }, new List<ImageChunk>()));

        _chunkerMock.Setup(c => c.Chunk(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string> { "content_chunk1" });

        var service = new IngestionPipelineService(
            dbContext,
            new[] { parserMock.Object },
            _chunkerMock.Object,
            _embeddingMock.Object,
            _storageMock.Object,
            _tenantProviderMock.Object,
            _usageServiceMock.Object,
            _configuration
        );

        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content"));

        // Act
        try {
            var result = await service.IngestFileAsync(stream, "test.txt", "text/plain");
            // Assert
            Assert.NotEqual(Guid.Empty, result.DocumentId);
            Assert.Equal(1, result.ChunkCount);
        } catch (Exception ex) {
            Console.WriteLine(ex.ToString());
            if (ex.InnerException != null) Console.WriteLine("INNER: " + ex.InnerException.ToString());
            throw;
        }
        
        var doc = dbContext.KnowledgeBaseDocuments.Include(d => d.Chunks).First();
        Assert.Equal("Indexed", doc.Status);
        Assert.Single(doc.Chunks);
    }
}
