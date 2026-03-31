using System;
using Microsoft.EntityFrameworkCore;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;
using Moq;
using OrvixFlow.Core.Interfaces;

var tenantId = Guid.NewGuid();
var mockProvider = new Mock<ITenantProvider>();
mockProvider.Setup(x => x.GetTenantId()).Returns(tenantId);

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(databaseName: "TestDB")
    .Options;

using var db = new AppDbContext(options, mockProvider.Object);

try {
    var doc = new KnowledgeBaseDocument { TenantId = tenantId, FileName = "test.txt", ContentType = "text/plain" };
    db.KnowledgeBaseDocuments.Add(doc);
    db.SaveChanges();
    Console.WriteLine("Doc saved");

    var chunk = new KnowledgeBase { 
        TenantId = tenantId, 
        DocumentId = doc.Id, 
        RawContent = "test",
        EmbeddingVector = new Pgvector.Vector(new float[1536])
    };
    db.KnowledgeBases.Add(chunk);
    db.SaveChanges();
    Console.WriteLine("Chunk saved");
} catch (Exception ex) {
    Console.WriteLine(ex.ToString());
}
