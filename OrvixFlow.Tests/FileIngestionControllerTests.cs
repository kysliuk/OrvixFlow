using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrvixFlow.Api.Controllers;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class FileIngestionControllerTests
{
    [Fact]
    public async Task UploadFile_DepartmentManagerUploadingToOwnDepartment_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
        var file = CreatePdfFile();

        var result = await harness.Controller.UploadFile(file, departmentId);

        result.Should().BeOfType<OkObjectResult>();
        var document = await harness.DbContext.KnowledgeBaseDocuments.SingleAsync();
        document.DepartmentId.Should().Be(departmentId);
        harness.StorageMock.Verify(
            x => x.SaveFileAsync(
                It.Is<StorageContext>(ctx =>
                    ctx.TenantId == tenantId &&
                    ctx.DepartmentId == departmentId &&
                    ctx.DocumentId == document.Id &&
                    ctx.OriginalFileName == file.FileName),
                It.IsAny<Stream>()),
            Times.Once);
        harness.StorageMock.Verify(
            x => x.SaveFileAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadFile_DepartmentManagerUploadingToAnotherDepartment_ReturnsForbid()
    {
        var tenantId = Guid.NewGuid();
        var allowedDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [allowedDepartmentId]);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), otherDepartmentId);

        result.Should().BeOfType<ForbidResult>();
        (await harness.DbContext.KnowledgeBaseDocuments.CountAsync()).Should().Be(0);
        harness.StorageMock.Verify(x => x.SaveFileAsync(It.IsAny<StorageContext>(), It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task UploadFile_DepartmentManagerUploadingCompanyWideFile_ReturnsForbid()
    {
        var harness = CreateHarness(Guid.NewGuid(), hasCompanyWideAccess: false, allowedDepartmentIds: [Guid.NewGuid()]);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), null);

        result.Should().BeOfType<ForbidResult>();
        (await harness.DbContext.KnowledgeBaseDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UploadFile_CompanyAdminUploadingToAnyDepartment_ReturnsOk()
    {
        var departmentId = Guid.NewGuid();
        var harness = CreateHarness(Guid.NewGuid(), hasCompanyWideAccess: true, allowedDepartmentIds: []);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), departmentId);

        result.Should().BeOfType<OkObjectResult>();
        var document = await harness.DbContext.KnowledgeBaseDocuments.SingleAsync();
        document.DepartmentId.Should().Be(departmentId);
    }

    [Fact]
    public async Task UploadFile_CompanyAdminUploadingCompanyWideFile_ReturnsOk()
    {
        var harness = CreateHarness(Guid.NewGuid(), hasCompanyWideAccess: true, allowedDepartmentIds: []);

        var result = await harness.Controller.UploadFile(CreatePdfFile(), null);

        result.Should().BeOfType<OkObjectResult>();
        var document = await harness.DbContext.KnowledgeBaseDocuments.SingleAsync();
        document.DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task DownloadDocument_DocumentInOwnDepartment_ReturnsFileStream()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
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
    public async Task DownloadDocument_DocumentInAnotherDepartment_ReturnsForbid()
    {
        var tenantId = Guid.NewGuid();
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);
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
    public async Task DownloadDocument_DocumentFromAnotherTenant_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: true, allowedDepartmentIds: []);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = otherTenantId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = "docs/path.pdf"
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DownloadDocument(documentId);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DownloadDocument_DocumentWithEmptyStoragePath_ReturnsNotFound()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
        harness.DbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            Id = documentId,
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            StoragePath = string.Empty
        });
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.DownloadDocument(documentId);

        result.Should().BeOfType<NotFoundObjectResult>();
        harness.StorageMock.Verify(x => x.GetFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetDocuments_DepartmentManagerSeesOnlyOwnDepartmentDocuments()
    {
        var tenantId = Guid.NewGuid();
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);
        SeedDocument(harness.DbContext, tenantId, ownDepartmentId, "own.pdf");
        SeedDocument(harness.DbContext, tenantId, otherDepartmentId, "other.pdf");
        SeedDocument(harness.DbContext, tenantId, null, "company.pdf");
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.GetDocuments(page: 1, pageSize: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetItems(ok.Value).Should().ContainSingle();
        GetItems(ok.Value).Single().fileName.Should().Be("own.pdf");
    }

    [Fact]
    public async Task GetDocuments_CompanyAdminSeesAllTenantDocumentsIncludingCompanyWide()
    {
        var tenantId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: true, allowedDepartmentIds: []);
        SeedDocument(harness.DbContext, tenantId, Guid.NewGuid(), "dept-a.pdf");
        SeedDocument(harness.DbContext, tenantId, Guid.NewGuid(), "dept-b.pdf");
        SeedDocument(harness.DbContext, tenantId, null, "company.pdf");
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.GetDocuments(page: 1, pageSize: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetItems(ok.Value).Should().HaveCount(3);
        GetItems(ok.Value).Select(x => x.fileName).Should().Contain(["dept-a.pdf", "dept-b.pdf", "company.pdf"]);
    }

    [Fact]
    public async Task GetDocuments_DepartmentManagerFilteringAnotherDepartment_ReturnsForbid()
    {
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var harness = CreateHarness(Guid.NewGuid(), hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);

        var result = await harness.Controller.GetDocuments(otherDepartmentId, 1, 20);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetDocuments_NonAdminWithNoAllowedDepartments_ReturnsNoDocuments()
    {
        var tenantId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: []);
        SeedDocument(harness.DbContext, tenantId, Guid.NewGuid(), "dept-a.pdf");
        SeedDocument(harness.DbContext, tenantId, null, "company.pdf");
        await harness.DbContext.SaveChangesAsync();

        var result = await harness.Controller.GetDocuments(page: 1, pageSize: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetItems(ok.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDocument_DocumentInOwnDepartment_ReturnsNoContent()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [departmentId]);
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

        var result = await harness.Controller.DeleteDocument(documentId);

        result.Should().BeOfType<NoContentResult>();
        harness.StorageMock.Verify(x => x.DeleteFileAsync("docs/path.pdf"), Times.Once);
        (await harness.DbContext.KnowledgeBaseDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteDocument_DocumentInAnotherDepartment_ReturnsForbid()
    {
        var tenantId = Guid.NewGuid();
        var ownDepartmentId = Guid.NewGuid();
        var otherDepartmentId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var harness = CreateHarness(tenantId, hasCompanyWideAccess: false, allowedDepartmentIds: [ownDepartmentId]);
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

    private static TestHarness CreateHarness(Guid tenantId, bool hasCompanyWideAccess, IReadOnlyList<Guid> allowedDepartmentIds)
    {
        var tenantProviderMock = new Mock<ITenantProvider>();
        tenantProviderMock.Setup(x => x.GetTenantId()).Returns(tenantId);

        var storageMock = new Mock<IFileStorage>();
        storageMock.Setup(x => x.SaveFileAsync(It.IsAny<StorageContext>(), It.IsAny<Stream>()))
            .ReturnsAsync("stored/object-key");
        storageMock.Setup(x => x.GetFileAsync(It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        storageMock.Setup(x => x.DeleteFileAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var virusScanMock = new Mock<IVirusScanService>();
        virusScanMock.Setup(x => x.IsFileSafeAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        backgroundJobClientMock.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>()))
            .Returns("job-id");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Ingestion:MaxFileSizeMb"] = "20",
                ["AI:Ingestion:AllowedMimeTypes:0"] = "application/pdf"
            })
            .Build();

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
            configuration,
            virusScanMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            }
        };

        return new TestHarness(controller, dbContext, storageMock, virusScanMock, backgroundJobClientMock);
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

    private static void SeedDocument(AppDbContext dbContext, Guid tenantId, Guid? departmentId, string fileName)
    {
        dbContext.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = fileName,
            ContentType = "application/pdf",
            StoragePath = fileName,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private static List<(Guid id, string fileName, Guid? departmentId)> GetItems(object? value)
    {
        value.Should().NotBeNull();
        var itemsProperty = value!.GetType().GetProperty("items");
        itemsProperty.Should().NotBeNull();
        var items = (System.Collections.IEnumerable?)itemsProperty!.GetValue(value);
        items.Should().NotBeNull();

        return items!
            .Cast<object>()
            .Select(item =>
            {
                var type = item.GetType();
                return (
                    (Guid)type.GetProperty("id")!.GetValue(item)!,
                    (string)type.GetProperty("fileName")!.GetValue(item)!,
                    (Guid?)type.GetProperty("departmentId")!.GetValue(item));
            })
            .ToList();
    }

    private sealed record TestHarness(
        FileIngestionController Controller,
        AppDbContext DbContext,
        Mock<IFileStorage> StorageMock,
        Mock<IVirusScanService> VirusScanMock,
        Mock<IBackgroundJobClient> BackgroundJobClientMock);

    private sealed class TestScopeContext : IScopeContext
    {
        public TestScopeContext(Guid userId, Guid companyId, bool hasCompanyWideAccess, IReadOnlyList<Guid> allowedDepartmentIds)
        {
            UserId = userId;
            CompanyId = companyId;
            HasCompanyWideAccess = hasCompanyWideAccess;
            AllowedDepartmentIds = allowedDepartmentIds;
        }

        public Guid UserId { get; }
        public Guid CompanyId { get; }
        public bool HasCompanyWideAccess { get; }
        public IReadOnlyList<Guid> AllowedDepartmentIds { get; }
        public Task InitializeAsync() => Task.CompletedTask;
    }
}
