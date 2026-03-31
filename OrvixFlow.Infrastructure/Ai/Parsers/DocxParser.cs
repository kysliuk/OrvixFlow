using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai.Parsers;

public class DocxParser : IDocumentParser
{
    public bool CanParse(string contentType)
    {
        return contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    }

    public Task<ParsedDocument> ParseAsync(Stream content, string fileName)
    {
        using (var wordDoc = WordprocessingDocument.Open(content, false))
        {
            var body = wordDoc.MainDocumentPart?.Document.Body;
            if (body == null)
            {
                return Task.FromResult(new ParsedDocument(fileName, new List<TextChunk>(), new List<ImageChunk>()));
            }

            var sb = new StringBuilder();
            foreach (var para in body.Elements<Paragraph>())
            {
                sb.AppendLine(para.InnerText);
            }

            var textChunks = new List<TextChunk>
            {
                new TextChunk(0, sb.ToString(), null)
            };

            return Task.FromResult(new ParsedDocument(fileName, textChunks, new List<ImageChunk>()));
        }
    }
}
