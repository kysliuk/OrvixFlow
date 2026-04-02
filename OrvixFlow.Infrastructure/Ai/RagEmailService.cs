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

            var searchQuery = $"{subject} {body}";
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
}
