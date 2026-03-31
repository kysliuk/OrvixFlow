using System;
using System.Collections.Generic;
using System.Linq;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai.Chunking;

public class OverlapChunker : IChunker
{
    private const int CharsPerToken = 4; // Standard heuristic: 1 token ~= 4 chars

    public IEnumerable<string> Chunk(string text, int maxTokens, int overlapTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var charLimit = maxTokens * CharsPerToken;
        var charOverlap = overlapTokens * CharsPerToken;

        if (text.Length <= charLimit)
        {
            yield return text;
            yield break;
        }

        int start = 0;
        int length = text.Length;

        while (start < length)
        {
            int currentChunkSize = Math.Min(charLimit, length - start);
            var chunk = text.Substring(start, currentChunkSize);
            
            // Try to find a better breakpoint within the chunk (boundary search)
            // Look for the last newline or space if possible
            if (start + currentChunkSize < length)
            {
                int breakpoint = FindBreakpoint(chunk);
                if (breakpoint > charLimit / 2) // only break if it's not too early
                {
                    currentChunkSize = breakpoint;
                    chunk = text.Substring(start, currentChunkSize);
                }
            }

            yield return chunk;

            // Move start forward, accounting for overlap
            start += (currentChunkSize - charOverlap);
            
            // Prevent infinite loop if overlap is larger than chunk size or start doesn't move
            if (currentChunkSize <= charOverlap)
            {
                start += charLimit / 2; // fall back to move at least half a chunk
            }
        }
    }

    private static int FindBreakpoint(string chunk)
    {
        // Try to break at last paragraph change
        int lastDoubleNewline = chunk.LastIndexOf("\n\n");
        if (lastDoubleNewline > 0) return lastDoubleNewline + 2;

        // Try to break at last newline
        int lastNewline = chunk.LastIndexOf("\n");
        if (lastNewline > 0) return lastNewline + 1;

        // Try to break at last sentence end
        int lastSentence = chunk.LastIndexOfAny(new[] { '.', '!', '?' });
        if (lastSentence > 0) return lastSentence + 1;

        // Try to break at last space
        int lastSpace = chunk.LastIndexOf(' ');
        if (lastSpace > 0) return lastSpace + 1;

        return chunk.Length;
    }
}
