using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai.Parsers;

public class ImageFileParser : IDocumentParser
{
    private static readonly HashSet<string> SupportedMimeTypes = new()
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    public bool CanParse(string contentType)
    {
        return SupportedMimeTypes.Contains(contentType.ToLowerInvariant());
    }

    public async Task<ParsedDocument> ParseAsync(Stream content, string fileName)
    {
        using (var ms = new MemoryStream())
        {
            await content.CopyToAsync(ms);
            var data = ms.ToArray();

            var imageChunks = new List<ImageChunk>
            {
                new ImageChunk(0, data, GetMimeType(fileName) ?? "image/png", null)
            };

            // Return a ParsedDocument with empty text chunks initially.
            // The captioning will happen in the ingestion pipeline.
            return new ParsedDocument(fileName, new List<TextChunk>(), imageChunks);
        }
    }

    private string? GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };
    }
}
