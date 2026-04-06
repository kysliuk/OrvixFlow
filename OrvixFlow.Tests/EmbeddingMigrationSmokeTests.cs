using Microsoft.Extensions.AI;
using OrvixFlow.Infrastructure.Ai.Mock;
using Xunit;

namespace OrvixFlow.Tests;

public class EmbeddingMigrationSmokeTests
{
    [Fact]
    public async Task MockEmbeddingGenerator_Returns1536DimVector()
    {
        var generator = new MockEmbeddingGenerator();
        var results = await generator.GenerateAsync(["hello world"]);
        Assert.Single(results);
        Assert.Equal(1536, results[0].Vector.Length);
    }
}
