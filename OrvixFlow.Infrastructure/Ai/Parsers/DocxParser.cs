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
            var mainPart = wordDoc.MainDocumentPart;
            var body = mainPart?.Document.Body;
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

            var imageChunks = new List<ImageChunk>();
            if (mainPart != null)
            {
                int imageIndex = 0;
                foreach (var imagePart in mainPart.ImageParts)
                {
                    using (var stream = imagePart.GetStream())
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        imageChunks.Add(new ImageChunk(
                            imageIndex++,
                            ms.ToArray(),
                            imagePart.ContentType,
                            null // Caption not easily associated in simple run
                        ));
                    }
                }
            }

            return Task.FromResult(new ParsedDocument(fileName, textChunks, imageChunks));
        }
    }
}
