using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure.Services;

public class DraftFeedbackService : IDraftFeedbackService
{
    private readonly AppDbContext _dbContext;

    public DraftFeedbackService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DraftFeedback> RecordFeedbackAsync(Guid tenantId, Guid actionRequestId, string originalDraft, string finalHumanDraft)
    {
        var editDistance = await CalculateEditDistanceAsync(originalDraft, finalHumanDraft);

        var feedback = new DraftFeedback
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActionRequestId = actionRequestId,
            OriginalDraft = originalDraft,
            FinalHumanDraft = finalHumanDraft,
            EditDistance = editDistance,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DraftFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync();

        return feedback;
    }

    public Task<decimal> CalculateEditDistanceAsync(string original, string final)
    {
        var distance = LevenshteinDistance(original, final);
        var maxLength = Math.Max(original.Length, final.Length);
        var normalizedDistance = maxLength > 0 ? (decimal)distance / maxLength : 0m;
        return Task.FromResult(normalizedDistance);
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return string.IsNullOrEmpty(target) ? 0 : target.Length;
        if (string.IsNullOrEmpty(target))
            return source.Length;

        var n = source.Length;
        var m = target.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++)
            d[i, 0] = i;
        for (var j = 0; j <= m; j++)
            d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
