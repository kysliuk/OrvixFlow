using System.Collections.Generic;

namespace OrvixFlow.Core.Interfaces;

public interface IChunker
{
    IEnumerable<string> Chunk(string text, int maxTokens, int overlapTokens);
}
