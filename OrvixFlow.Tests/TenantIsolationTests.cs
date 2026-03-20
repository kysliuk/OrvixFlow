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
            var seedContextA = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedContextA.KnowledgeBases.Add(new KnowledgeBase { TenantId = tenantA_Id, RawContent = "Tenant A Data" });
            seedContextA.SaveChanges();
        }

        // Seed data for Tenant B
        currentTenantId = tenantB_Id;
        using (var scope = provider.CreateScope())
        {
            var seedContextB = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedContextB.KnowledgeBases.Add(new KnowledgeBase { TenantId = tenantB_Id, RawContent = "Tenant B Data" });
            seedContextB.SaveChanges();
        }

        // Act - Querying as Tenant A
        currentTenantId = tenantA_Id;
        using (var scope = provider.CreateScope())
        {
            var actContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var knowledgeBases = actContext.KnowledgeBases.ToList();

            // Assert
            knowledgeBases.Should().HaveCount(1, "because the global query filter should exclude Tenant B's data");
            knowledgeBases.First().TenantId.Should().Be(tenantA_Id);
            knowledgeBases.First().RawContent.Should().Be("Tenant A Data");
        }
    }
}
