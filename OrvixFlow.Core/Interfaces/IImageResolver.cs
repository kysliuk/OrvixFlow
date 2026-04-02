using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface IImageResolver
{
    /// <summary>
    /// Resolves relevant images from the knowledge base based on the query and retrieved text snippets.
    /// </summary>
    Task<IReadOnlyList<KnowledgeBaseImage>> ResolveRelevantImagesAsync(string query, IEnumerable<Guid> documentIds, int maxResults = 3);
}
