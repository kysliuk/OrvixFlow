using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Ai;

public class AgentService : IAgentService
{
    private readonly Kernel _kernel;
    
    // We can inject Kernel because it's configured as Transient/Scoped in DI
    public AgentService(
        Kernel kernel, 
        OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin searchPlugin,
        OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin automationPlugin)
    {
        _kernel = kernel;
        _kernel.Plugins.AddFromObject(searchPlugin);
        _kernel.Plugins.AddFromObject(automationPlugin);
    }

    public async Task<AgentResponse> ProcessInternalAsync(string prompt, Guid tenantId)
    {
        try
        {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Temperature", 0.0 }
                },
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
            
            var result = await chatCompletionService.GetChatMessageContentAsync(
                prompt, 
                executionSettings, 
                _kernel);

            return new AgentResponse
            {
                IsSuccess = true,
                Message = result.Content ?? string.Empty,
                Metadata = result.Metadata
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
