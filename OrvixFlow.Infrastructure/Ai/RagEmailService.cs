using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Ai;

public class RagEmailService : IRagEmailService
{
    private readonly IIntentClassifierService _classifier;
    private readonly IHybridVectorSearchService _vectorSearch;
    private readonly IDraftGeneratorService _draftGenerator;
    private readonly IRagMetricsCollector _metrics;
    private readonly ILogger<RagEmailService> _logger;

    public RagEmailService(
        IIntentClassifierService classifier,
        IHybridVectorSearchService vectorSearch,
        IDraftGeneratorService draftGenerator,
        IRagMetricsCollector metrics,
        ILogger<RagEmailService> logger)
    {
        _classifier = classifier;
        _vectorSearch = vectorSearch;
        _draftGenerator = draftGenerator;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<N8nEmailPayload> ProcessRagEmailAsync(
        string senderEmail,
        string subject,
        string body,
        Guid tenantId,
        string messageId,
        Guid? traceId = null)
    {
        var sw = Stopwatch.StartNew();
        var currentTraceId = traceId ?? Guid.NewGuid();

        _logger.LogInformation("[{TraceId}] Starting RAG email processing for {MessageId}", currentTraceId, messageId);

        try
        {
            var classification = await _classifier.ClassifyEmailAsync(
                senderEmail, subject, body);

            var searchQuery = BuildFocusedQuery(subject, body);
            var context = await _vectorSearch.SearchAsync(searchQuery, maxResults: 5);

            var draftResult = await _draftGenerator.GenerateDraftAsync(
                senderEmail, subject, body, classification, context);

            sw.Stop();

            string action = "draft_ready";
            if (classification.RequiresHumanReview) action = "human_review_required";
            if (draftResult.IsInsufficientContext) action = "insufficient_context";

            var result = new N8nEmailPayload
            {
                TenantId = tenantId,
                MessageId = messageId,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Email = new EmailDraftDetails
                {
                    To = senderEmail,
                    Subject = $"Re: {subject}",
                    BodyText = draftResult.DraftBody
                },
                Classification = new ClassificationDetails
                {
                    Category = classification.Category,
                    ConfidenceScore = (float)classification.ConfidenceScore,
                    Reasoning = classification.Reasoning,
                    RequiresHumanReview = classification.RequiresHumanReview,
                    ReasonForReview = classification.ReasonForReview
                },
                Rag = new RagDetails
                {
                    SnippetsUsed = context.Count,
                    HasContext = context.Count > 0,
                    InsufficientContext = draftResult.IsInsufficientContext
                },
                Images = draftResult.RelevantImages,
                Action = action,
                Flags = new AutomationFlags
                {
                    AutoSendAllowed = !classification.RequiresHumanReview && !draftResult.IsInsufficientContext,
                    HumanReviewRequired = classification.RequiresHumanReview || draftResult.IsInsufficientContext
                },
                Audit = new AuditDetails
                {
                    TraceId = currentTraceId,
                    Model = "gpt-4o"
                }
            };

            await _metrics.RecordRetrievalMetricsAsync(
                tenantId, 
                currentTraceId, 
                context.Count, 
                draftResult.RelevantImages.Count, 
                sw.ElapsedMilliseconds, 
                "gpt-4o");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TraceId}] Failed to process RAG email", currentTraceId);
            throw;
        }
    }

    private static string BuildFocusedQuery(string subject, string body)
    {
        var terms = new List<string>();
        
        // Extract key terms from subject (capitalized words, important-looking terms)
        if (!string.IsNullOrEmpty(subject))
        {
            var subjectTerms = subject.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !IsCommonWord(w))
                .Take(5);
            terms.AddRange(subjectTerms);
        }
        
        // Extract key terms from body - focus on the first 200 chars (usually contains the main question)
        if (!string.IsNullOrEmpty(body))
        {
            var bodyPreview = body.Length > 200 ? body[..200] : body;
            var bodyTerms = bodyPreview.Split(new[] { ' ', '\n', '.', ',', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4 && !IsCommonWord(w) && w.Any(char.IsLetter))
                .Take(8);
            terms.AddRange(bodyTerms);
        }
        
        // If we couldn't extract good terms, fall back to subject + first sentence
        if (terms.Count < 3)
        {
            return $"{subject} {body.Split('.').FirstOrDefault() ?? string.Empty}";
        }
        
        return string.Join(" ", terms.Distinct());
    }

    private static bool IsCommonWord(string word)
    {
        var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "with", "this", "that", "from", "have", "has", "will",
            "would", "could", "should", "what", "when", "where", "which", "who", "how",
            "than", "then", "them", "they", "their", "there", "were", "been", "being",
            "some", "into", "more", "such", "your", "just", "also", "back", "about",
            "after", "before", "because", "other", "only", "over", "think", "know"
        };
        return commonWords.Contains(word);
    }
}
