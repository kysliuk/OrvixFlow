using System.Collections.Generic;
using System.Threading.Tasks;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Core.Interfaces;

public interface IReranker
{
    Task<IReadOnlyList<KnowledgeSnippet>> RerankAsync(string query, IReadOnlyList<KnowledgeSnippet> snippets);
}
