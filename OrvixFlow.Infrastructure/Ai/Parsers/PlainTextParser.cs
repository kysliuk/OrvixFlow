using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai.Parsers;

public class PlainTextParser : IDocumentParser
{
    public bool CanParse(string contentType)
    {
        return contentType == "text/plain" || contentType == "text/html";
    }

    public async Task<ParsedDocument> ParseAsync(Stream content, string fileName)
    {
        using var reader = new StreamReader(content);
        var text = await reader.ReadToEndAsync();
        
        // Very basic HTML stripping could go here if needed, 
        // but for now we follow the design and just read it.
        
        var textChunks = new List<TextChunk>
        {
            new TextChunk(0, text, null)
        };

        return new ParsedDocument(fileName, textChunks, new List<ImageChunk>());
    }
}
