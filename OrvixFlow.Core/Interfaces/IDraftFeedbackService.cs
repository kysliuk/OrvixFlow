using System;
using System.Threading.Tasks;
using OrvixFlow.Core.Entities;

namespace OrvixFlow.Core.Interfaces;

public interface IDraftFeedbackService
{
    Task<DraftFeedback> RecordFeedbackAsync(Guid tenantId, Guid actionRequestId, string originalDraft, string finalHumanDraft);
    Task<decimal> CalculateEditDistanceAsync(string original, string final);
}
