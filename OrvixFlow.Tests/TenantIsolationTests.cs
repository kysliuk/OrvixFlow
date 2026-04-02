using System;
using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;
using Xunit;

namespace OrvixFlow.Tests;

public class TenantIsolationTests
{
    [Fact]
    public void Should_Fail_To_Access_Other_Tenant_Data()
    {
        // Arrange
        var tenantA_Id = Guid.NewGuid();
        var tenantB_Id = Guid.NewGuid();
        
        Guid currentTenantId = Guid.Empty;

        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(m => m.GetTenantId()).Returns(() => currentTenantId);

        var dbName = "OrvixTestDb_" + Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(mockTenantProvider.Object);
        services.AddDbContext<AppDbContext>(options => 
            options.UseInMemoryDatabase(dbName));
            
        var provider = services.BuildServiceProvider();

        // Seed data for Tenant A
        currentTenantId = tenantA_Id;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument { TenantId = tenantA_Id, FileName = "DocA" });
            db.KnowledgeBases.Add(new KnowledgeBase { TenantId = tenantA_Id, RawContent = "ContentA" });
            db.KnowledgeBaseImages.Add(new KnowledgeBaseImage { TenantId = tenantA_Id, AltText = "ImgA" });
            db.SaveChanges();
        }

        // Seed data for Tenant B
        currentTenantId = tenantB_Id;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KnowledgeBaseDocuments.Add(new KnowledgeBaseDocument { TenantId = tenantB_Id, FileName = "DocB" });
            db.KnowledgeBases.Add(new KnowledgeBase { TenantId = tenantB_Id, RawContent = "ContentB" });
            db.KnowledgeBaseImages.Add(new KnowledgeBaseImage { TenantId = tenantB_Id, AltText = "ImgB" });
            db.SaveChanges();
        }

        // Act - Querying as Tenant A
        currentTenantId = tenantA_Id;
        using (var scope = provider.CreateScope())
        {
            var actContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            // Check KnowledgeBase
            var knowledgeBases = actContext.KnowledgeBases.ToList();
            knowledgeBases.Should().HaveCount(1, "because the global query filter should exclude Tenant B's data");
            knowledgeBases.First().TenantId.Should().Be(tenantA_Id);

            // Check KnowledgeBaseDocument
            var docs = actContext.KnowledgeBaseDocuments.ToList();
            docs.Should().HaveCount(1);
            docs.First().TenantId.Should().Be(tenantA_Id);

            // Check KnowledgeBaseImage
            var images = actContext.KnowledgeBaseImages.ToList();
            images.Should().HaveCount(1);
            images.First().TenantId.Should().Be(tenantA_Id);
        }
    }

    [Fact]
    public void Should_Be_Able_To_Bypass_Isolation_As_Admin()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var mockTenantProvider = new Mock<ITenantProvider>();
        mockTenantProvider.Setup(m => m.GetTenantId()).Returns(tenantId);

        var dbName = "OrvixAdminDb_" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(mockTenantProvider.Object);
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KnowledgeBases.Add(new KnowledgeBase { TenantId = tenantId, RawContent = "T1" });
            db.KnowledgeBases.Add(new KnowledgeBase { TenantId = Guid.NewGuid(), RawContent = "T2" });
            db.SaveChanges();
        }

        // Act
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var allCount = db.KnowledgeBases.IgnoreQueryFilters().Count();

            // Assert
            allCount.Should().Be(2);
        }
    }
}
