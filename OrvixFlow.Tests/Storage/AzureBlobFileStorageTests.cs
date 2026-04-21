using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Tests.Storage;

public class AzureBlobFileStorageTests
{
    private static AzureBlobFileStorage CreateService(Mock<BlobContainerClient> containerMock)
        => new(containerMock.Object, NullLogger<AzureBlobFileStorage>.Instance);

    [Fact]
    public async Task SaveFileAsync_WithDepartmentId_KeyContainsDepartmentSegment()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();
        var capturedBlobName = string.Empty;

        container.Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(name => capturedBlobName = name)
            .Returns(blob.Object);
        blob.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var service = CreateService(container);
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = new StorageContext(tenantId, departmentId, documentId, "report.pdf");

        var key = await service.SaveFileAsync(context, new MemoryStream(new byte[] { 0x01 }));

        key.Should().StartWith($"tenants/{tenantId}/depts/{departmentId}/docs/{documentId}/");
        key.Should().EndWith(".pdf");
        capturedBlobName.Should().Be(key);
    }

    [Fact]
    public async Task SaveFileAsync_NullDepartmentId_UsesCompanySentinel()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();
        var capturedBlobName = string.Empty;

        container.Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(name => capturedBlobName = name)
            .Returns(blob.Object);
        blob.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var service = CreateService(container);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = new StorageContext(tenantId, null, documentId, "company-policy.pdf");

        var key = await service.SaveFileAsync(context, new MemoryStream(new byte[] { 0x01 }));

        key.Should().Contain("__company__");
        key.Should().NotContain("null");
        capturedBlobName.Should().Be(key);
    }

    [Fact]
    public async Task SaveFileAsync_LegacyOverload_UsesCompanySentinel()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();

        container.Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(blob.Object);
        blob.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var service = CreateService(container);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var key = await service.SaveFileAsync(tenantId, documentId, "img_0_doc.png", new MemoryStream(new byte[] { 0x01 }));

        key.Should().Contain("__company__");
    }

    [Fact]
    public async Task SaveFileAsync_KeyFormatMatchesMinIoConvention()
    {
        var container = new Mock<BlobContainerClient>();
        var blob = new Mock<BlobClient>();

        container.Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(blob.Object);
        blob.Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<BlobContentInfo>)null!);

        var service = CreateService(container);
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var key = await service.SaveFileAsync(new StorageContext(tenantId, departmentId, documentId, "manual.docx"), new MemoryStream(new byte[] { 0x01 }));

        key.Should().MatchRegex($"^tenants/{tenantId}/depts/{departmentId}/docs/{documentId}/[0-9a-fA-F-]+\\.docx$");
    }

    [Fact]
    public async Task DeleteFileAsync_EmptyPath_DoesNotCallAzure()
    {
        var container = new Mock<BlobContainerClient>();
        var service = CreateService(container);

        await service.DeleteFileAsync(string.Empty);

        container.Verify(x => x.GetBlobClient(It.IsAny<string>()), Times.Never);
    }
}
