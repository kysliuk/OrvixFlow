using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Ai;

public class LlmScorerReranker : IReranker
{
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ILogger<LlmScorerReranker> _logger;

    public LlmScorerReranker(IChatCompletionService chatCompletionService, ILogger<LlmScorerReranker> logger)
    {
        _chatCompletionService = chatCompletionService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeSnippet>> RerankAsync(string query, IReadOnlyList<KnowledgeSnippet> snippets)
    {
        if (snippets.Count == 0 || string.IsNullOrWhiteSpace(query))
            return snippets;
        
        var prompt = $@"
You are a relevance scoring assistant for a retrieval-augmented generation (RAG) system.
Your job is to read a user query and a set of knowledge snippets, then score each snippet on a scale of 0 to 10 based on how relevant it is to answering the query.

Score 10: The snippet directly and completely answers the query.
Score 7-9: The snippet provides highly relevant information but may not be a complete answer.
Score 4-6: The snippet is somewhat relevant or provides useful background context.
Score 1-3: The snippet is tangentially relevant at best.
Score 0: The snippet is completely irrelevant.

Return ONLY a JSON array of objects, where each object has snippetId (the exact ID provided) and score (0-10 integer). DO NOT return any other text or markdown block.
Example: [{{""snippetId"": ""123e4567-e89b-12d3-a456-426614174000"", ""score"": 9}}]

Input Query: {query}
Snippets:
";
        foreach (var s in snippets)
        {
            prompt += $"\n[ID: {s.Id}]\n{s.Content}\n---\n";
        }

        try
        {
            var responseMsg = await _chatCompletionService.GetChatMessageContentAsync(prompt);
            var content = responseMsg.Content;
            
            if (string.IsNullOrWhiteSpace(content))
                return snippets;

            var cleanJson = content.Trim();
            if (cleanJson.StartsWith("```json")) cleanJson = cleanJson.Substring(7);
            if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            var scores = JsonSerializer.Deserialize<List<SnippetScore>>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (scores == null || scores.Count == 0)
                return snippets;

            var scoredSnippets = new List<KnowledgeSnippet>(snippets.Count);
            foreach (var snippet in snippets)
            {
                var scoreInfo = scores.FirstOrDefault(x => x.SnippetId == snippet.Id);
                var newScore = scoreInfo != null ? (float)scoreInfo.Score / 10f : 0f;
                
                // Construct a new instance to mutate score
                scoredSnippets.Add(new KnowledgeSnippet
                {
                    Id = snippet.Id,
                    Content = snippet.Content,
                    Metadata = snippet.Metadata,
                    SimilarityScore = newScore,
                    Title = snippet.Title,
                    ChunkType = snippet.ChunkType,
                    DocumentId = snippet.DocumentId,
                    RelatedImages = snippet.RelatedImages
                });
            }
            
            return scoredSnippets.OrderByDescending(x => x.SimilarityScore).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rerank snippets using LLM. Falling back to original RRF order.");
            return snippets; // Fallback
        }
    }

    private class SnippetScore
    {
        public Guid SnippetId { get; set; }
        public int Score { get; set; }
    }
}
