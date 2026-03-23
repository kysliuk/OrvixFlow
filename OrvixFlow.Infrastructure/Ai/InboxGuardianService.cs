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

    public InboxGuardianService(
        Kernel kernel,
        OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin searchPlugin,
        OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin automationPlugin)
    {
        _kernel = kernel;
        _kernel.Plugins.AddFromObject(searchPlugin);
        _kernel.Plugins.AddFromObject(automationPlugin);
    }

    public async Task<AgentResponse> ProcessIncomingMessageAsync(InboxMessage message, Guid tenantId)
    {
        try
        {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var executionSettings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ExtensionData = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "Temperature", 0.1 }
                }
            };

            var systemPrompt = @"You are the 'Inbox Guardian', an automated customer support triage agent for OrvixFlow. 
You are processing an incoming message or email from a customer.
Your goals:
1. Understand the customer's question or issue.
2. Use the 'KnowledgeBaseSearchPlugin' to search for the specific factual answer in the company database.
3. If you find a confident answer, formulate a polite and professional reply, then USE the 'N8nAutomationPlugin' to trigger the 'draft-reply' webhook, passing the drafted reply as the messageData.
4. If you CANNOT find the answer or it requires human intervention, USE the 'N8nAutomationPlugin' to trigger the 'escalate-to-human' webhook, passing your explanation as the messageData.
If any webhook returns a 404 Not Found error, do NOT attempt to retry. The webhooks simply haven't been created yet. Just output a natural language summary to the user!";

            var chatHistory = new ChatHistory(systemPrompt);
            var userMessage = $"From: {message.SenderEmail}\nSubject: {message.Subject}\nBody: {message.Body}";
            chatHistory.AddUserMessage(userMessage);

            var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel);

            return new AgentResponse
            {
                IsSuccess = true,
                Message = result.Content ?? "Processed successfully.",
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
