using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OrvixFlow.Core.Interfaces;

public record TextChunk(int Index, string Content, string? Heading);
public record ImageChunk(int Index, byte[] Data, string ContentType, string? Caption);

public record ParsedDocument(
    string Title,
    IReadOnlyList<TextChunk> TextChunks,
    IReadOnlyList<ImageChunk> ImageChunks
);

public interface IDocumentParser
{
    bool CanParse(string contentType);
    Task<ParsedDocument> ParseAsync(Stream content, string fileName);
}
