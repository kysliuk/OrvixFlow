using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Storage;
using Xunit;

namespace OrvixFlow.Tests;

public class LocalToMinioMigrationJobTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IAmazonS3> _s3Mock;
    private readonly Mock<ILogger<LocalToMinioMigrationJob>> _loggerMock;
    private readonly Mock<ITenantProvider> _tenantProviderMock;
    private readonly string _basePath;

    public LocalToMinioMigrationJobTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProviderMock = new Mock<ITenantProvider>();
        _db = new AppDbContext(options, _tenantProviderMock.Object);
        _s3Mock = new Mock<IAmazonS3>();
        _loggerMock = new Mock<ILogger<LocalToMinioMigrationJob>>();
        
        // Setup a temporary directory for tests
        _basePath = Path.Combine(Path.GetTempPath(), "orvixflow_test_uploads");
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, true);
        }
    }

    [Fact]
    public async Task RunAsync_ValidFileInsideBasePath_MigratesAndReplacesDbStoragePath()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var document = new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "valid.txt",
            ContentType = "text/plain",
            FileSizeBytes = 11,
            Status = "Pending"
        };
        var validFilePath = Path.Combine(_basePath, "valid_test.txt");
        await File.WriteAllTextAsync(validFilePath, "valid content");
        
        document.StoragePath = validFilePath;
        _db.KnowledgeBaseDocuments.Add(document);
        await _db.SaveChangesAsync();

        _tenantProviderMock.Setup(t => t.GetTenantId()).Returns(tenantId);

        _s3Mock.Setup(x => x.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectMetadataResponse());

        var job = new LocalToMinioMigrationJob(_db, _s3Mock.Object, "orvixflow", _basePath, _loggerMock.Object);

        // Act
        await job.RunAsync(dryRun: false);

        // Assert
        var doc = await _db.KnowledgeBaseDocuments.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == document.Id);
        doc!.StoragePath.Should().NotContain(_basePath).And.Contain("tenants/");
        
        var storedObj = await _db.StoredObjects.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.EntityId == document.Id);
        storedObj.Should().NotBeNull();
        storedObj!.StorageKey.Should().Be(doc.StoragePath);
        
        _s3Mock.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_PathOutsideBasePath_IsRejected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var document = new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "outside.txt",
            ContentType = "text/plain",
            FileSizeBytes = 11,
            Status = "Pending"
        };
        // Use an arbitrary outside path (doesn't have to exist because path check is before existence check)
        var outsidePath = Path.GetFullPath(Path.Combine(_basePath, "..", "outside.txt"));
        
        document.StoragePath = outsidePath;
        _db.KnowledgeBaseDocuments.Add(document);
        await _db.SaveChangesAsync();

        var job = new LocalToMinioMigrationJob(_db, _s3Mock.Object, "orvixflow", _basePath, _loggerMock.Object);

        // Act
        await job.RunAsync(dryRun: false);

        // Assert
        var doc = await _db.KnowledgeBaseDocuments.FindAsync(document.Id);
        doc!.StoragePath.Should().Be(outsidePath); // Not changed
        
        var storedObj = await _db.StoredObjects.FirstOrDefaultAsync(s => s.EntityId == document.Id);
        storedObj.Should().BeNull(); // Metadata not created
        
        _s3Mock.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_NormalizedTraversalPath_IsRejected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var document = new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "traversal.txt",
            ContentType = "text/plain",
            FileSizeBytes = 11,
            Status = "Pending"
        };
        // Traversal path that "starts" with base path string but steps out
        var traversalPath = Path.Combine(_basePath, "..", "traversal.txt");
        
        document.StoragePath = traversalPath;
        _db.KnowledgeBaseDocuments.Add(document);
        await _db.SaveChangesAsync();

        var job = new LocalToMinioMigrationJob(_db, _s3Mock.Object, "orvixflow", _basePath, _loggerMock.Object);

        // Act
        await job.RunAsync(dryRun: false);

        // Assert
        var doc = await _db.KnowledgeBaseDocuments.FindAsync(document.Id);
        doc!.StoragePath.Should().Be(traversalPath); // Not changed
        _s3Mock.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MissingFile_IsSkippedNonFatal()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var document = new KnowledgeBaseDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FileName = "missing.txt",
            ContentType = "text/plain",
            FileSizeBytes = 11,
            Status = "Pending"
        };
        var validFilePath = Path.Combine(_basePath, "missing.txt"); // Doesn't exist
        
        document.StoragePath = validFilePath;
        _db.KnowledgeBaseDocuments.Add(document);
        await _db.SaveChangesAsync();

        var job = new LocalToMinioMigrationJob(_db, _s3Mock.Object, "orvixflow", _basePath, _loggerMock.Object);

        // Act
        await job.RunAsync(dryRun: false);

        // Assert
        var doc = await _db.KnowledgeBaseDocuments.FindAsync(document.Id);
        doc!.StoragePath.Should().Be(validFilePath); // Not changed
        _s3Mock.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
