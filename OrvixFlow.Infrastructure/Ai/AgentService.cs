using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Core.Models;
using OrvixFlow.Infrastructure.Ai.Plugins;

namespace OrvixFlow.Infrastructure.Ai;

public class AgentService : IAgentService
{
    private readonly Kernel _kernel;
    private readonly IAuditService _audit;
    private readonly IUsageService _usage;
    private readonly IScopeContext _scope;

    public AgentService(
        Kernel kernel,
        KnowledgeBaseSearchPlugin searchPlugin,
        N8nAutomationPlugin automationPlugin,
        IAuditService audit,
        IUsageService usage,
        IScopeContext scope)
    {
        _kernel = kernel;
        _audit  = audit;
        _usage  = usage;
        _scope  = scope;

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
                prompt, executionSettings, _kernel);

            var content = result.Content ?? string.Empty;

            // ── Shadow module side-effects (fire-and-forget style, but awaited) ──

            // Approximate token count from prompt + response length (real usage
            // would come from result.Metadata["Usage"] for OpenAI-compatible APIs).
            var estimatedTokens = EstimateTokens(prompt, content);

            await _usage.RecordTokensAsync(
                companyId:    _scope.CompanyId,
                moduleKey:    "agent",
                tokenCount:   estimatedTokens,
                userId:       _scope.UserId == Guid.Empty ? null : _scope.UserId);

            await _audit.RecordAsync(
                tenantId:        tenantId,
                action:          "agent.process",
                decisionDetails: $"prompt_length={prompt.Length} response_length={content.Length}",
                userId:          _scope.UserId == Guid.Empty ? null : _scope.UserId);

            return new AgentResponse
            {
                IsSuccess = true,
                Message   = content,
                Metadata  = result.Metadata
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

    /// <summary>
    /// Rough approximation: ~4 characters per token (standard heuristic).
    /// Replace with provider metadata when available.
    /// </summary>
    private static int EstimateTokens(string prompt, string response) =>
        (prompt.Length + response.Length) / 4;
}
