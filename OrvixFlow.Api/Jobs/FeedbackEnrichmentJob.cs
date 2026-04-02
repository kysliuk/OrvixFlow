using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Entities;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Jobs;

public class FeedbackEnrichmentJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FeedbackEnrichmentJob> _logger;

    public FeedbackEnrichmentJob(IServiceProvider serviceProvider, ILogger<FeedbackEnrichmentJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 120 })]
    public async Task ProcessAsync(Guid feedbackId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FeedbackEnrichmentJob>>();

        var feedback = await dbContext.DraftFeedbacks.FindAsync(feedbackId);
        if (feedback == null)
        {
            logger.LogWarning("Feedback {FeedbackId} not found for enrichment", feedbackId);
            return;
        }

        if (feedback.EditDistance < 0.3m)
        {
            logger.LogInformation("Feedback {FeedbackId} edit distance {EditDistance} below threshold, skipping",
                feedbackId, feedback.EditDistance);
            return;
        }

        try
        {
            var guideline = ExtractGuideline(feedback.OriginalDraft, feedback.FinalHumanDraft);

            if (string.IsNullOrEmpty(guideline))
            {
                logger.LogInformation("No actionable guideline extracted from feedback {FeedbackId}", feedbackId);
                return;
            }

            var knowledgeBase = new KnowledgeBase
            {
                Id = Guid.NewGuid(),
                TenantId = feedback.TenantId,
                Title = $"AI Feedback Guideline - {DateTime.UtcNow:yyyy-MM-dd}",
                RawContent = guideline,
                Metadata = $"{{\"source\":\"feedback-loop\",\"actionRequestId\":\"{feedback.ActionRequestId}\",\"editDistance\":{feedback.EditDistance}}}",
                CreatedAtUtc = DateTime.UtcNow,
                ChunkIndex = 0,
                ChunkType = "text"
            };

            dbContext.KnowledgeBases.Add(knowledgeBase);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Enriched knowledge base with guideline from feedback {FeedbackId} for tenant {TenantId}",
                feedbackId, feedback.TenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process feedback enrichment for {FeedbackId}", feedbackId);
            throw;
        }
    }

    private static string? ExtractGuideline(string original, string final)
    {
        var originalLines = original.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var finalLines = final.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var addedLines = finalLines.Except(originalLines, StringComparer.OrdinalIgnoreCase).ToList();
        var removedLines = originalLines.Except(finalLines, StringComparer.OrdinalIgnoreCase).ToList();

        if (addedLines.Count == 0 && removedLines.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("GUIDELINE EXTRACTED FROM HUMAN EDIT:");
        sb.AppendLine();

        if (addedLines.Count > 0)
        {
            sb.AppendLine("HUMAN ADDED:");
            foreach (var line in addedLines.Take(5))
            {
                sb.AppendLine($"  + {line.Trim()}");
            }
        }

        if (removedLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("HUMAN REMOVED:");
            foreach (var line in removedLines.Take(5))
            {
                sb.AppendLine($"  - {line.Trim()}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("INSTRUCTION: When generating similar responses, follow the human-edited patterns above.");

        return sb.ToString();
    }
}
