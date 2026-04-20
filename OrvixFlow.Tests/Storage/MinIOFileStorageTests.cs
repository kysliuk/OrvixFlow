using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Storage;

namespace OrvixFlow.Tests.Storage;

public class MinIOFileStorageTests
{
    private static MinIOFileStorage CreateService(Mock<IAmazonS3> s3Mock)
        => new(s3Mock.Object, "test-bucket", NullLogger<MinIOFileStorage>.Instance);

    [Fact]
    public async Task SaveFileAsync_WithDepartmentId_KeyContainsDepartmentSegment()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = new StorageContext(tenantId, departmentId, documentId, "report.pdf");

        var key = await service.SaveFileAsync(context, new MemoryStream(new byte[] { 0x01 }));

        key.Should().StartWith($"tenants/{tenantId}/depts/{departmentId}/docs/{documentId}/");
        key.Should().EndWith(".pdf");
    }

    [Fact]
    public async Task SaveFileAsync_NullDepartmentId_UsesCompanySentinel()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var context = new StorageContext(tenantId, null, documentId, "company-policy.pdf");

        var key = await service.SaveFileAsync(context, new MemoryStream(new byte[] { 0x01 }));

        key.Should().Contain("__company__");
        key.Should().NotContain("null");
    }

    [Fact]
    public async Task SaveFileAsync_LegacyOverload_UsesCompanySentinel()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3);
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var key = await service.SaveFileAsync(tenantId, documentId, "img_0_doc.png", new MemoryStream(new byte[] { 0x01 }));

        key.Should().Contain("__company__");
    }

    [Fact]
    public async Task SaveFileAsync_KeyContainsNoUserControlledSegments()
    {
        var s3 = new Mock<IAmazonS3>();
        s3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var service = CreateService(s3);
        var context = new StorageContext(Guid.NewGuid(), null, Guid.NewGuid(), "../../../etc/passwd");

        var key = await service.SaveFileAsync(context, new MemoryStream(new byte[] { 0x01 }));

        key.Should().NotContain("..");
        key.Should().NotContain("/etc/");
        key.Should().NotContain("passwd");
    }

    [Fact]
    public async Task DeleteFileAsync_EmptyPath_DoesNotCallS3()
    {
        var s3 = new Mock<IAmazonS3>();
        var service = CreateService(s3);

        await service.DeleteFileAsync(string.Empty);

        s3.Verify(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
