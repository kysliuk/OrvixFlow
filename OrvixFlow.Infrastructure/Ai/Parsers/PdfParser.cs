using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using OrvixFlow.Core.Interfaces;
using UglyToad.PdfPig;

namespace OrvixFlow.Infrastructure.Ai.Parsers;

public class PdfParser : IDocumentParser
{
    public bool CanParse(string contentType)
    {
        return contentType == "application/pdf";
    }

    public Task<ParsedDocument> ParseAsync(Stream content, string fileName)
    {
        using (var document = PdfDocument.Open(content))
        {
            var textChunks = new List<TextChunk>();
            var imageChunks = new List<ImageChunk>();
            var sb = new StringBuilder();
            int imageIndex = 0;

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
                
                foreach (var image in page.GetImages())
                {
                    var bytes = image.RawBytes;
                    if (bytes != null && bytes.Length > 0)
                    {
                        var contentType = "image/png"; // default
                        
                        imageChunks.Add(new ImageChunk(imageIndex++, bytes.ToArray(), contentType, null));
                    }
                }
            }

            textChunks.Add(new TextChunk(0, sb.ToString(), null));

            return Task.FromResult(new ParsedDocument(fileName, textChunks, imageChunks));
        }
    }
}
