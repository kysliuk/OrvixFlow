using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace OrvixFlow.Infrastructure.Ai.Mock;

public class MockTextEmbeddingGenerationService : ITextEmbeddingGenerationService
{
    public IReadOnlyDictionary<string, object?>? Attributes => new Dictionary<string, object?>();

    public Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        // Generate a pseudo-random, deterministic vector based on the first character so different texts have different distances if needed
        var result = data.Select(text => 
        {
            var vector = new float[1536];
            var val = string.IsNullOrEmpty(text) ? 0.0f : (float)text[0] / 255.0f;
            for (int i = 0; i < vector.Length; i++) vector[i] = val;
            return new ReadOnlyMemory<float>(vector);
        }).ToList();
        
        return Task.FromResult<IList<ReadOnlyMemory<float>>>(result);
    }
}
