using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class KnowledgeBaseDocumentTests
{
    [Fact]
    public async Task KnowledgeBaseDocument_CanBeCreated_WithNullDepartmentId_ForCompanyWide()
    {
        var tenantId = Guid.NewGuid();

        await using var db = CreateDbContext(tenantId);

        var doc = new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            DepartmentId = null,
            FileName = "policy.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            Status = "Pending"
        };

        db.KnowledgeBaseDocuments.Add(doc);
        await db.SaveChangesAsync();

        var loaded = await db.KnowledgeBaseDocuments.FindAsync(doc.Id);
        loaded.Should().NotBeNull();
        loaded!.DepartmentId.Should().BeNull();
    }

    [Fact]
    public async Task KnowledgeBaseDocument_CanBeCreated_WithDepartmentId()
    {
        var tenantId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        await using var db = CreateDbContext(tenantId);

        var doc = new KnowledgeBaseDocument
        {
            TenantId = tenantId,
            DepartmentId = departmentId,
            FileName = "sales-report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048,
            Status = "Pending"
        };

        db.KnowledgeBaseDocuments.Add(doc);
        await db.SaveChangesAsync();

        var loaded = await db.KnowledgeBaseDocuments.FindAsync(doc.Id);
        loaded.Should().NotBeNull();
        loaded!.DepartmentId.Should().Be(departmentId);
    }

    private static AppDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new MockTenantProvider(tenantId));
    }

    private sealed class MockTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public MockTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetTenantId() => _tenantId;
    }
}
