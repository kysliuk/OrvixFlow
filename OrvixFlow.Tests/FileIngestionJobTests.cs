using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai.Jobs;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class FileIngestionJobTests
{
    [Fact]
    public async Task ProcessFileAsync_WhenStorageFetchSucceeds_CallsPipelineWithDepartmentId()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var storageMock = new Mock<IFileStorage>();
        storageMock.Setup(x => x.GetFileAsync("docs/key.pdf")).ReturnsAsync(stream);

        var pipelineMock = new Mock<IIngestionPipelineService>();
        pipelineMock
            .Setup(x => x.IngestFileAsync(stream, "doc.pdf", "application/pdf", documentId, tenantId, userId, departmentId))
            .ReturnsAsync(new IngestionResult(documentId, 1, 0));

        using var scopeFactory = CreateScopeFactory(tenantId, storageMock.Object, pipelineMock.Object, databaseName: Guid.NewGuid().ToString());
        var tenantProviderFactoryMock = new Mock<ITenantProviderFactory>();
        var loggerMock = new Mock<ILogger<FileIngestionJob>>();
        var job = new FileIngestionJob(scopeFactory, tenantProviderFactoryMock.Object, loggerMock.Object);

        await job.ProcessFileAsync(documentId, "docs/key.pdf", "doc.pdf", "application/pdf", userId, departmentId, tenantId);

        storageMock.Verify(x => x.GetFileAsync("docs/key.pdf"), Times.Once);
        pipelineMock.Verify(x => x.IngestFileAsync(stream, "doc.pdf", "application/pdf", documentId, tenantId, userId, departmentId), Times.Once);
    }

    [Fact]
    public async Task ProcessFileAsync_WhenStorageReturnsNoSuchKey_MarksDocumentFailedAndDoesNotThrow()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var storageMock = new Mock<IFileStorage>();
        storageMock
            .Setup(x => x.GetFileAsync("missing/key.pdf"))
            .ThrowsAsync(new AmazonS3Exception("Missing") { ErrorCode = "NoSuchKey" });

        var pipelineMock = new Mock<IIngestionPipelineService>(MockBehavior.Strict);

        using var scopeFactory = CreateScopeFactory(tenantId, storageMock.Object, pipelineMock.Object, databaseName: Guid.NewGuid().ToString(), seedDocumentId: documentId);
        var tenantProviderFactoryMock = new Mock<ITenantProviderFactory>();
        var loggerMock = new Mock<ILogger<FileIngestionJob>>();
        var job = new FileIngestionJob(scopeFactory, tenantProviderFactoryMock.Object, loggerMock.Object);

        var act = () => job.ProcessFileAsync(documentId, "missing/key.pdf", "doc.pdf", "application/pdf", null, null, tenantId);

        await act.Should().NotThrowAsync();

        storageMock.Verify(x => x.GetFileAsync("missing/key.pdf"), Times.Once);
        pipelineMock.VerifyNoOtherCalls();

        using var verificationScope = scopeFactory.CreateScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var document = await db.KnowledgeBaseDocuments.IgnoreQueryFilters().SingleAsync(x => x.Id == documentId);
        document.Status.Should().Be("Failed");
        document.ErrorMessage.Should().Be("File not found in storage at key: missing/key.pdf");
    }

    [Fact]
    public async Task ProcessFileAsync_WhenStorageThrowsTransientError_RetriesThreeTimesAndThrows()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var storageMock = new Mock<IFileStorage>();
        storageMock
            .Setup(x => x.GetFileAsync("docs/key.pdf"))
            .ThrowsAsync(new IOException("temporary network issue"));

        var pipelineMock = new Mock<IIngestionPipelineService>(MockBehavior.Strict);

        using var scopeFactory = CreateScopeFactory(tenantId, storageMock.Object, pipelineMock.Object, databaseName: Guid.NewGuid().ToString());
        var tenantProviderFactoryMock = new Mock<ITenantProviderFactory>();
        var loggerMock = new Mock<ILogger<FileIngestionJob>>();
        var job = new FileIngestionJob(scopeFactory, tenantProviderFactoryMock.Object, loggerMock.Object);

        var act = () => job.ProcessFileAsync(documentId, "docs/key.pdf", "doc.pdf", "application/pdf", null, null, tenantId);

        await act.Should().ThrowAsync<IOException>();
        storageMock.Verify(x => x.GetFileAsync("docs/key.pdf"), Times.Exactly(3));
        pipelineMock.VerifyNoOtherCalls();
    }

    private static ServiceProvider CreateScopeFactory(
        Guid tenantId,
        IFileStorage storage,
        IIngestionPipelineService pipeline,
        string databaseName,
        Guid? seedDocumentId = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(storage);
        services.AddSingleton(pipeline);
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(tenantId));
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));

        var provider = services.BuildServiceProvider();

        if (seedDocumentId.HasValue)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
            {
                Id = seedDocumentId.Value,
                TenantId = tenantId,
                FileName = "doc.pdf",
                ContentType = "application/pdf",
                StoragePath = "missing/key.pdf",
                Status = "Pending"
            });
            db.SaveChanges();
        }

        return provider;
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetTenantId() => tenantId;
    }
}
