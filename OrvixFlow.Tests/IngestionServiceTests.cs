using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Moq;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class IngestionServiceTests
{
    [Fact]
    public async Task IngestTextAsync_Should_Generate_Embedding_And_Save_To_Db()
    {
        // Arrange
        var mockEmbeddingService = new Mock<ITextEmbeddingGenerationService>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        var mockUsageService = new Mock<IUsageService>();
        var tenantId = Guid.NewGuid();
        
        mockTenantProvider.Setup(p => p.GetTenantId()).Returns(tenantId);
        
        var dummyEmbedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        // The extension method calls GenerateEmbeddingsAsync internally.
        mockEmbeddingService.Setup(s => s.GenerateEmbeddingsAsync(
                It.IsAny<System.Collections.Generic.IList<string>>(), 
                It.IsAny<Kernel>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dummyEmbedding });
            
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
            
        // Use separate context instance to simulate fresh DB context
        using var dbContext = new AppDbContext(options, mockTenantProvider.Object);
        var ingestionService = new IngestionService(dbContext, mockEmbeddingService.Object, mockTenantProvider.Object, mockUsageService.Object);
        
        var testContent = "This is a dummy test content for ingestion.";
        
        // Act
        await ingestionService.IngestTextAsync(testContent);
        
        // Assert
        var savedRecord = await dbContext.KnowledgeBases.IgnoreQueryFilters().FirstOrDefaultAsync();
        savedRecord.Should().NotBeNull();
        savedRecord!.RawContent.Should().Be(testContent);
        savedRecord.TenantId.Should().Be(tenantId);
        
        mockEmbeddingService.Verify(s => s.GenerateEmbeddingsAsync(
            It.IsAny<System.Collections.Generic.IList<string>>(), 
            null, 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
