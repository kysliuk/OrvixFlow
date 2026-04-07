using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
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
        var mockEmbeddingService = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var mockTenantProvider = new Mock<ITenantProvider>();
        var mockUsageService = new Mock<IUsageService>();
        var tenantId = Guid.NewGuid();
        
        mockTenantProvider.Setup(p => p.GetTenantId()).Returns(tenantId);
        
        var dummyEmbedding = new Embedding<float>(new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f }));
        mockEmbeddingService.Setup(s => s.GenerateAsync(
                It.IsAny<System.Collections.Generic.IEnumerable<string>>(), 
                It.IsAny<EmbeddingGenerationOptions?>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(new[] { dummyEmbedding }));
            
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
        
        mockEmbeddingService.Verify(s => s.GenerateAsync(
            It.IsAny<System.Collections.Generic.IEnumerable<string>>(), 
            null, 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
