using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Api.Health;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Ai.Jobs;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Tests.Storage;

public class StorageIntegrationTests
{
    [Fact]
    public async Task UploadFile_CreatesKnowledgeBaseDocumentWithDepartmentIdAndStoragePath()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: true, allowedDepartmentIds: []);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), departmentId);

        result.Should().BeOfType<OkObjectResult>();
        var document = await harness.DbContext.KnowledgeBaseDocuments.SingleAsync();
        document.DepartmentId.Should().Be(departmentId);
        document.StoragePath.Should().Be("stored/object-key");
    }

    [Fact]
    public async Task UploadFile_CreatesStoredObjectWithEntityTypeAndSha256()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: true, allowedDepartmentIds: []);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), departmentId);

        result.Should().BeOfType<OkObjectResult>();
        var document = await harness.DbContext.KnowledgeBaseDocuments.SingleAsync();
        var storedObject = await harness.DbContext.StoredObjects.SingleAsync();
        storedObject.EntityType.Should().Be("document");
        storedObject.EntityId.Should().Be(document.Id);
        storedObject.Sha256.Should().HaveLength(64);
        storedObject.Sha256.All(Uri.IsHexDigit).Should().BeTrue();
    }

    [Fact]
    public async Task DownloadDocument_AuthorizedUser_ReturnsStream()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DownloadDocument(documentId);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("doc.pdf");
        harness.StorageMock.Verify(x => x.GetFileAsync("docs/path.pdf"), Times.Once);
    }

    [Fact]
    public async Task DownloadDocument_UnauthorizedDepartment_ReturnsForbid()
    {
        var tenantId = Guid.NewGuid();
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DepartmentId = otherDepartmentId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DownloadDocument(documentId);

        result.Should().BeOfType<ForbidResult>();
        harness.StorageMock.Verify(x => x.GetFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DownloadDocument_CrossTenantFilterReturnsNullAndControllerReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: true, allowedDepartmentIds: []);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = otherTenantId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        await harness.DbContext.SaveChangesAsync();

        var filteredDocument = await harness.DbContext.KnowledgeBaseDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);
        var result = await harness.Controller.DownloadDocument(documentId);

        filteredDocument.Should().BeNull();
        result.Should().BeOfType<NotFoundResult>();
        harness.StorageMock.Verify(x => x.GetFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDocument_RemovesStorageDocumentAndChunks()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        harness.DbContext.KnowledgeBases.Add(new KnowledgeBase
        {
            Id = chunkId,
            TenantId = tenantId,
            DocumentId = documentId,
            RawContent = "chunk"
        });
        harness.DbContext.KnowledgeBaseImages.Add(new KnowledgeBaseImage
        {
            TenantId = tenantId,
            DocumentId = documentId,
            ChunkId = chunkId,
            StoragePath = "images/path.png"
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DeleteDocument(documentId);

        result.Should().BeOfType<NoContentResult>();
        harness.StorageMock.Verify(x => x.DeleteFileAsync("docs/path.pdf"), Times.Once);
        (await harness.DbContext.KnowledgeBaseDocuments.CountAsync()).Should().Be(0);
        (await harness.DbContext.KnowledgeBases.CountAsync()).Should().Be(0);
        (await harness.DbContext.KnowledgeBaseImages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteDocument_UnauthorizedDepartment_ReturnsForbid()
    {
        var tenantId = Guid.NewGuid();
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateControllerHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DepartmentId = otherDepartmentId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DeleteDocument(documentId);

        result.Should().BeOfType<ForbidResult>();
        harness.StorageMock.Verify(x => x.DeleteFileAsync(It.IsAny<string>()), Times.Never);
        (await harness.DbContext.KnowledgeBaseDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FileIngestionJob_NonSeekableStorageStream_IsBufferedBeforeParsing()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var parserReceivedSeekableStream = false;
        var storageMock = new Mock<IFileStorage>();
        storageMock
            .Setup(x => x.GetFileAsync("docs/non-seekable.txt"))
            .ReturnsAsync(new NonSeekableReadStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("content"))));

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
                [new TextChunk(0, "content", null)],
                []));

        var chunkerMock = new Mock<IChunker>();
        chunkerMock.Setup(x => x.Chunk("content", It.IsAny<int>(), It.IsAny<int>())).Returns(["content"]);

        var embeddingMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddingMock
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[1536])]));

        var usageServiceMock = new Mock<IUsageService>();
        usageServiceMock.Setup(x => x.RecordKnowledgeBaseAsync(tenantId, "knowledge-base", 1, null, null)).Returns(Task.CompletedTask);
        usageServiceMock.Setup(x => x.RecordStorageAsync(tenantId, "knowledge-base", It.IsAny<int>(), null, null)).Returns(Task.CompletedTask);

        var metricsMock = new Mock<IRagMetricsCollector>();
        metricsMock
            .Setup(x => x.RecordIngestionMetricsAsync(tenantId, documentId, 1, 0, It.IsAny<long>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(storageMock.Object);
        services.AddSingleton(parserMock.Object);
        services.AddSingleton(chunkerMock.Object);
        services.AddSingleton(embeddingMock.Object);
        services.AddSingleton(usageServiceMock.Object);
        services.AddSingleton(metricsMock.Object);
        services.AddSingleton(new Mock<IChatCompletionService>(MockBehavior.Strict).Object);
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddSingleton<ITenantProviderFactory>(new Mock<ITenantProviderFactory>().Object);
        services.AddSingleton<IIngestionPipelineService>(sp => new IngestionPipelineService(
            sp.GetRequiredService<AppDbContext>(),
            [sp.GetRequiredService<IDocumentParser>()],
            sp.GetRequiredService<IChunker>(),
            sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>(),
            sp.GetRequiredService<IFileStorage>(),
            sp.GetRequiredService<ITenantProvider>(),
            sp.GetRequiredService<IUsageService>(),
            CreateIngestionConfiguration(),
            sp.GetRequiredService<IChatCompletionService>(),
            sp.GetRequiredService<IRagMetricsCollector>(),
            sp.GetRequiredService<ILogger<IngestionPipelineService>>()));
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
            {
                Id = documentId,
                TenantId = tenantId,
                FileName = "test.txt",
                ContentType = "text/plain",
                StoragePath = "docs/non-seekable.txt",
                Status = "Pending"
            });
            await db.SaveChangesAsync();
        }

        var job = new FileIngestionJob(
            provider,
            provider.GetRequiredService<ITenantProviderFactory>(),
            provider.GetRequiredService<ILogger<FileIngestionJob>>());

        await job.ProcessFileAsync(documentId, "docs/non-seekable.txt", "test.txt", "text/plain", null, null, tenantId);

        parserReceivedSeekableStream.Should().BeTrue();
    }

    [Fact]
    public async Task MinIOFileStorage_PathTraversalFilename_DoesNotLeakSegments()
    {
        var s3Mock = new Mock<Amazon.S3.IAmazonS3>();
        s3Mock.Setup(x => x.PutObjectAsync(It.IsAny<Amazon.S3.Model.PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Amazon.S3.Model.PutObjectResponse());
        var storage = new MinIOFileStorage(s3Mock.Object, "test-bucket", Mock.Of<ILogger<MinIOFileStorage>>());

        var key = await storage.SaveFileAsync(
            new StorageContext(Guid.NewGuid(), null, Guid.NewGuid(), "../etc/passwd"),
            new MemoryStream([0x01]));

        key.Should().NotContain("..");
        key.Should().NotContain("/etc/");
        key.Should().NotContain("passwd");
    }

    [Fact]
    public async Task StoredObject_LifecycleStatus_DefaultsActiveAndCanBeSoftDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(tenantId);
        var storedObject = new StoredObject
        {
            TenantId = tenantId,
            Module = "knowledge-base",
            EntityType = "document",
            EntityId = Guid.NewGuid(),
            StorageProvider = "MinIO",
            ContainerOrBucket = "orvixflow",
            StorageKey = "docs/path.pdf",
            OriginalFileName = "doc.pdf",
            ContentType = "application/pdf",
            SizeBytes = 10,
            Sha256 = new string('a', 64),
            CreatedByUserId = Guid.NewGuid()
        };

        dbContext.StoredObjects.Add(storedObject);
        await dbContext.SaveChangesAsync();

        storedObject.LifecycleStatus.Should().Be("Active");

        storedObject.LifecycleStatus = "SoftDeleted";
        storedObject.DeletedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        var reloaded = await dbContext.StoredObjects.SingleAsync();
        reloaded.LifecycleStatus.Should().Be("SoftDeleted");
        reloaded.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task StorageHealthCheck_LocalProvider_ReturnsHealthy()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var check = new StorageHealthCheck(
            provider,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "Local"
            }).Build(),
            Mock.Of<ILogger<StorageHealthCheck>>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task StorageHealthCheck_RemoteProviderWithoutClient_ReturnsUnhealthy()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var check = new StorageHealthCheck(
            provider,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            }).Build(),
            Mock.Of<ILogger<StorageHealthCheck>>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task StorageHealthCheck_AccessibleBucket_ReturnsHealthy()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(x => x.ListObjectsV2Async(It.Is<ListObjectsV2Request>(r => r.BucketName == "orvixflow" && r.MaxKeys == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response());

        var services = new ServiceCollection();
        services.AddSingleton(s3Mock.Object);
        using var provider = services.BuildServiceProvider();
        var check = new StorageHealthCheck(
            provider,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            }).Build(),
            Mock.Of<ILogger<StorageHealthCheck>>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task StorageHealthCheck_UnreachableBucket_ReturnsUnhealthy()
    {
        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("down"));

        var services = new ServiceCollection();
        services.AddSingleton(s3Mock.Object);
        using var provider = services.BuildServiceProvider();
        var check = new StorageHealthCheck(
            provider,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            }).Build(),
            Mock.Of<ILogger<StorageHealthCheck>>());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task OrphanDetectionJob_PaginatesAndRemainsReadOnly()
    {
        var tenantId = Guid.NewGuid();
        await using var dbContext = CreateDbContext(tenantId);
        dbContext.StoredObjects.Add(new StoredObject
        {
            TenantId = tenantId,
            Module = "knowledge-base",
            EntityType = "document",
            EntityId = Guid.NewGuid(),
            StorageProvider = "MinIO",
            ContainerOrBucket = "orvixflow",
            StorageKey = "docs/known.pdf",
            OriginalFileName = "known.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1,
            Sha256 = new string('b', 64),
            CreatedByUserId = Guid.NewGuid()
        });
        await dbContext.SaveChangesAsync();

        var s3Mock = new Mock<IAmazonS3>();
        s3Mock
            .SetupSequence(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = true,
                NextContinuationToken = "next",
                S3Objects =
                [
                    new S3Object { Key = "docs/known.pdf", Size = 1 },
                    new S3Object { Key = "docs/orphan-a.pdf", Size = 2 }
                ]
            })
            .ReturnsAsync(new ListObjectsV2Response
            {
                IsTruncated = false,
                S3Objects =
                [
                    new S3Object { Key = "docs/orphan-b.pdf", Size = 3 }
                ]
            });

        var services = new ServiceCollection();
        services.AddSingleton(s3Mock.Object);
        using var provider = services.BuildServiceProvider();
        var logger = new TestLogger<OrphanDetectionJob>();
        var job = new OrphanDetectionJob(
            provider,
            dbContext,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:MinIO:Bucket"] = "orvixflow"
            }).Build(),
            logger);

        await job.RunAsync();

        s3Mock.Verify(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        s3Mock.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        logger.Entries.Count(x => x.Level == LogLevel.Warning).Should().Be(2);
    }

    private static ControllerHarness CreateControllerHarness(
        Guid tenantId,
        bool hasCompanyWideAccess,
        IReadOnlyList<Guid> allowedDepartmentIds)
    {
        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.Setup(x => x.GetTenantId()).Returns(tenantId);

        var storageMock = new Mock<IFileStorage>();
        storageMock.Setup(x => x.SaveFileAsync(It.IsAny<StorageContext>(), It.IsAny<Stream>()))
            .ReturnsAsync("stored/object-key");
        storageMock.Setup(x => x.GetFileAsync(It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream([1, 2, 3]));
        storageMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var virusScanMock = new Mock<IVirusScanService>();
        virusScanMock.Setup(x => x.IsFileSafeAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        backgroundJobClientMock.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new AppDbContext(options, tenantProviderMock.Object);

        var controller = new FileIngestionController(
            dbContext,
            storageMock.Object,
            tenantProviderMock.Object,
            new TestScopeContext(Guid.NewGuid(), tenantId, hasCompanyWideAccess, allowedDepartmentIds),
            backgroundJobClientMock.Object,
            CreateControllerConfiguration(),
            virusScanMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            }
        };

        return new ControllerHarness(controller, dbContext, storageMock);
    }

    private static AppDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new TestTenantProvider(tenantId));
    }

    private static IConfiguration CreateControllerConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Ingestion:MaxFileSizeMb"] = "20",
                ["AI:Ingestion:AllowedMimeTypes:0"] = "application/pdf",
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            })
            .Build();
    }

    private static IConfiguration CreateIngestionConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Ingestion:ChunkSize"] = "800",
                ["AI:Ingestion:ChunkOverlap"] = "150",
                ["Storage:Provider"] = "MinIO",
                ["Storage:MinIO:Bucket"] = "orvixflow"
            })
            .Build();
    }

    private static IFormFile CreatePdfFile()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    private sealed record ControllerHarness(
        FileIngestionController Controller,
        AppDbContext DbContext,
        Mock<IFileStorage> StorageMock);

    private sealed class TestScopeContext(
        Guid userId,
        Guid companyId,
        bool hasCompanyWideAccess,
        IReadOnlyList<Guid> allowedDepartmentIds) : IScopeContext
    {
        public Guid UserId { get; } = userId;
        public Guid CompanyId { get; } = companyId;
        public bool HasCompanyWideAccess { get; } = hasCompanyWideAccess;
        public IReadOnlyList<Guid> AllowedDepartmentIds { get; } = allowedDepartmentIds;
        public Task InitializeAsync() => Task.CompletedTask;
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetTenantId() => tenantId;
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
