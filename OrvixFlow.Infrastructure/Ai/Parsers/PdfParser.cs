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
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            textChunks.Add(new TextChunk(0, sb.ToString(), null));

            return Task.FromResult(new ParsedDocument(fileName, textChunks, new List<ImageChunk>()));
        }
    }
}
