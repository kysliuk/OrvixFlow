using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class IngestionPipelineServiceStreamTests
{
    [Fact]
    public async Task IngestFileAsync_WhenInputStreamIsNotSeekable_BuffersBeforeParsing()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var parserReceivedSeekableStream = false;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Ingestion:ChunkSize"] = "800",
                ["AI:Ingestion:ChunkOverlap"] = "150",
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            })
            .Build();

        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.Setup(x => x.GetTenantId()).Returns(tenantId);

        var parserMock = new Mock<IDocumentParser>();
        parserMock.Setup(x => x.CanParse("text/plain")).Returns(true);
        parserMock
            .Setup(x => x.ParseAsync(It.IsAny<Stream>(), "test.txt"))
            .Callback<Stream, string>((stream, _) =>
            {
                parserReceivedSeekableStream = stream.CanSeek;
                stream.Position.Should().Be(0);
            })
            .ReturnsAsync(new ParsedDocument(
                "test.txt",
                new List<TextChunk> { new(0, "content", null) },
                new List<ImageChunk>()));

        var chunkerMock = new Mock<IChunker>();
        chunkerMock.Setup(x => x.Chunk("content", It.IsAny<int>(), It.IsAny<int>())).Returns(new[] { "content" });

        var embeddingMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingMock
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[1536])]));

        var storageMock = new Mock<IFileStorage>(MockBehavior.Strict);
        var usageServiceMock = new Mock<IUsageService>();
        usageServiceMock.Setup(x => x.RecordKnowledgeBaseAsync(tenantId, "knowledge-base", 1, null, null)).Returns(Task.CompletedTask);
        usageServiceMock.Setup(x => x.RecordStorageAsync(tenantId, "knowledge-base", It.IsAny<int>(), null, null)).Returns(Task.CompletedTask);

        var chatCompletionMock = new Mock<IChatCompletionService>(MockBehavior.Strict);
        var metricsMock = new Mock<IRagMetricsCollector>();
        metricsMock
            .Setup(x => x.RecordIngestionMetricsAsync(tenantId, documentId, 1, 0, It.IsAny<long>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<IngestionPipelineService>>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options, tenantProviderMock.Object);
        dbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            FileName = "test.txt",
            ContentType = "text/plain",
            StoragePath = "docs/test.txt",
            Status = "Pending"
        });
        await dbContext.SaveChangesAsync();

        var service = new IngestionPipelineService(
            dbContext,
            new[] { parserMock.Object },
            chunkerMock.Object,
            embeddingMock.Object,
            storageMock.Object,
            tenantProviderMock.Object,
            usageServiceMock.Object,
            configuration,
            chatCompletionMock.Object,
            metricsMock.Object,
            loggerMock.Object);

        await using var stream = new NonSeekableReadStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content")));

        var result = await service.IngestFileAsync(stream, "test.txt", "text/plain", documentId, tenantId);

        result.ErrorMessage.Should().BeNull();
        parserReceivedSeekableStream.Should().BeTrue();
    }

    private sealed class NonSeekableReadStream(Stream innerStream) : Stream
    {
        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => innerStream.Length;
        public override long Position
        {
            get => innerStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask DisposeAsync() => innerStream.DisposeAsync();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
