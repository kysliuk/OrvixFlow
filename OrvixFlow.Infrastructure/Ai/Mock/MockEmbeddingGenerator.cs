using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace OrvixFlow.Infrastructure.Ai.Mock;

public class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata => new("mock", null, "mock-embed", 1536);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(text =>
        {
            var vector = new float[1536];
            var val = string.IsNullOrEmpty(text) ? 0.0f : (float)text[0] / 255.0f;
            for (int i = 0; i < vector.Length; i++) vector[i] = val;
            return new Embedding<float>(vector);
        }).ToList();

        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? key = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
