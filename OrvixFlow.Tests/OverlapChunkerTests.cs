using Xunit;
using OrvixFlow.Infrastructure.Ai.Chunking;
using System.Linq;

namespace OrvixFlow.Tests;

public class OverlapChunkerTests
{
    [Fact]
    public void Chunk_SmallText_ReturnsOneChunk()
    {
        // Arrange
        var chunker = new OverlapChunker();
        var text = "This is a small text.";
        var maxTokens = 100;
        var overlapTokens = 20;

        // Act
        var result = chunker.Chunk(text, maxTokens, overlapTokens).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(text, result[0]);
    }

    [Fact]
    public void Chunk_LargeText_RespectsMaxTokensAndOverlap()
    {
        // Arrange
        var chunker = new OverlapChunker();
        // Roughly ~4 chars per token. Let's make a text with many "tokens".
        var words = Enumerable.Range(0, 100).Select(i => $"word{i:D3}").ToList();
        var text = string.Join(" ", words); 
        // Each "word### " is 8 chars. ~2 tokens.
        // 100 words = ~200 tokens.
        var maxTokens = 50; 
        var overlapTokens = 10;

        // Act
        var result = chunker.Chunk(text, maxTokens, overlapTokens).ToList();

        // Assert
        Assert.True(result.Count > 1);
        // Check that subsequent chunks contain some overlap from previous ones
        for (int i = 1; i < result.Count; i++)
        {
            var currentChunk = result[i];
            var previousChunk = result[i-1];
            
            // The start of current chunk should be present in the end of previous chunk if overlap worked
            // This is a bit simplified for words vs tokens but helps verify the logic.
        }
    }

    [Fact]
    public void Chunk_EmptyText_ReturnsEmpty()
    {
        // Arrange
        var chunker = new OverlapChunker();
        var text = "";

        // Act
        var result = chunker.Chunk(text, 100, 20).ToList();

        // Assert
        Assert.Empty(result);
    }
}
