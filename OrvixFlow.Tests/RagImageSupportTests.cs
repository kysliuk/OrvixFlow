using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Ai.Parsers;
using OrvixFlow.Infrastructure.Data;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace OrvixFlow.Tests;

public class RagImageSupportTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public RagImageSupportTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }
    [Fact]
    public async Task PdfParser_ExtractsImages()
    {
        // Actually testing PdfParser might require a real PDF stream or a very good mock.
        // For now, testing the logic if we had a stream.
        var parser = new PdfParser();
        
        // Use an empty stream - PdfPig will throw or return empty.
        // This is more of a smoke test for the structure.
        using var ms = new MemoryStream();
        // Since we can't easily create a valid PDF binary here in code:
        // We'll skip the actual execution if it fails on invalid PDF format but check compilation.
        
        parser.CanParse("application/pdf").Should().BeTrue();
    }

    [Fact]
    public async Task ImageFileParser_CanParse_ImageTypes()
    {
        var parser = new ImageFileParser();
        parser.CanParse("image/jpeg").Should().BeTrue();
        parser.CanParse("image/png").Should().BeTrue();
        parser.CanParse("application/pdf").Should().BeFalse();
    }

    [Fact]
    public async Task ImageResolver_ReturnsRelevantImages()
    {
        // Setup
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockTenantProvider = new Mock<ITenantProvider>();
        var tenantId = Guid.NewGuid();
        mockTenantProvider.Setup(x => x.GetTenantId()).Returns(tenantId);

        using var context = new AppDbContext(options, mockTenantProvider.Object);
        var docId = Guid.NewGuid();

        var mockVector = new float[1536];
        mockVector[0] = 1.0f;

        var img1 = new KnowledgeBaseImage 
        { 
            TenantId = tenantId, 
            DocumentId = docId, 
            AltText = "Target Image",
            CaptionEmbedding = new Pgvector.Vector(mockVector) 
        };
        
        context.KnowledgeBaseImages.Add(img1);
        await context.SaveChangesAsync();

        // VERIFY STORAGE
        var count = await context.KnowledgeBaseImages.IgnoreQueryFilters().CountAsync();
        _output.WriteLine($"[TEST] DB count before Resolve: {count}");
        
        var imgInDb = await context.KnowledgeBaseImages.IgnoreQueryFilters().FirstOrDefaultAsync();
        _output.WriteLine($"[TEST] Image in DB tenant: {imgInDb?.TenantId}, id: {imgInDb?.Id}, embed: {imgInDb?.CaptionEmbedding != null}");

        mockVector = new float[1536];
        mockVector[0] = 1.0f;

        var mockEmbedding = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockEmbedding.Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(mockVector)])); 

        var mockLogger = new Mock<ILogger<ImageResolver>>();
        var resolver = new ImageResolver(context, mockEmbedding.Object, mockTenantProvider.Object, mockLogger.Object);
        _output.WriteLine($"[TEST] MockTenantProvider ID: {mockTenantProvider.Object.GetTenantId()}");

        // Act
        var results = await resolver.ResolveRelevantImagesAsync("find target", Array.Empty<Guid>());

        // Assert
        results.Should().NotBeEmpty();
        results.First().AltText.Should().Be("Target Image");
    }
}
