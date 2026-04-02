using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OrvixFlow.Core.Entities;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Ai;

public class InboxGuardianService : IInboxGuardianService
{
    private readonly Kernel _kernel;
    private readonly IIntentClassifierService _classifier;
    private readonly IHybridVectorSearchService _vectorSearch;
    private readonly IDraftGeneratorService _draftGenerator;
    private readonly IUsageService _usageService;

    public InboxGuardianService(
        Kernel kernel,
        IIntentClassifierService classifier,
        IHybridVectorSearchService vectorSearch,
        IDraftGeneratorService draftGenerator,
        OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin automationPlugin,
        IUsageService usageService)
    {
        _kernel = kernel;
        _classifier = classifier;
        _vectorSearch = vectorSearch;
        _draftGenerator = draftGenerator;
        _usageService = usageService;
        _kernel.Plugins.AddFromObject(automationPlugin);
    }

    public async Task<AgentResponse> ProcessIncomingMessageAsync(InboxMessage message, Guid tenantId, AgentPersona? persona = null, Guid? userId = null, Guid? departmentId = null)
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
                knowledgeContext,
                persona);

            var metadata = new System.Collections.Generic.Dictionary<string, object?>
            {
                ["category"] = classification.Category,
                ["confidenceScore"] = classification.ConfidenceScore,
                ["requiresHumanReview"] = classification.RequiresHumanReview,
                ["reasonForReview"] = classification.ReasonForReview,
                ["knowledgeBaseResults"] = knowledgeContext.Count,
                ["hasContext"] = knowledgeContext.Count > 0,
                ["personaApplied"] = persona != null
            };

            await _usageService.RecordInboxMessageAsync(tenantId, "inbox-guardian", 1, userId, departmentId);

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
                IsSuccess    = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
