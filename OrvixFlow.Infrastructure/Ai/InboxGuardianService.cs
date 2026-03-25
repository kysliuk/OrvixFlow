using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Ai;

public class InboxGuardianService : IInboxGuardianService
{
    private readonly Kernel _kernel;
    private readonly IIntentClassifierService _classifier;
    private readonly IHybridVectorSearchService _vectorSearch;
    private readonly IDraftGeneratorService _draftGenerator;

    public InboxGuardianService(
        Kernel kernel,
        IIntentClassifierService classifier,
        IHybridVectorSearchService vectorSearch,
        IDraftGeneratorService draftGenerator,
        OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin automationPlugin)
    {
        _kernel = kernel;
        _classifier = classifier;
        _vectorSearch = vectorSearch;
        _draftGenerator = draftGenerator;
        _kernel.Plugins.AddFromObject(automationPlugin);
    }

    public async Task<AgentResponse> ProcessIncomingMessageAsync(InboxMessage message, Guid tenantId)
    {
        try
        {
            var senderDomain = message.SenderEmail.Contains('@') 
                ? message.SenderEmail.Split('@')[1] 
                : null;

            var classification = await _classifier.ClassifyEmailAsync(
                message.SenderEmail,
                message.Subject,
                message.Body,
                senderDomain);

            var searchQuery = $"{message.Subject} {message.Body}";
            var knowledgeContext = await _vectorSearch.SearchAsync(searchQuery, maxResults: 5);

            var draftResponse = await _draftGenerator.GenerateDraftAsync(
                message.SenderEmail,
                message.Subject,
                message.Body,
                classification,
                knowledgeContext);

            var metadata = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["category"] = classification.Category,
                ["confidenceScore"] = classification.ConfidenceScore,
                ["requiresHumanReview"] = classification.RequiresHumanReview,
                ["reasonForReview"] = classification.ReasonForReview,
                ["knowledgeBaseResults"] = knowledgeContext.Count,
                ["hasContext"] = knowledgeContext.Count > 0
            };

            return new AgentResponse
            {
                IsSuccess = true,
                Message = draftResponse,
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
