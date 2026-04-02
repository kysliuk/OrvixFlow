using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace OrvixFlow.Infrastructure.Ai.Mock;

public class MockChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?>? Attributes => new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var systemMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System)?.Content ?? "";
        bool isVisionRequest = systemMessage.Contains("vision-capable", System.StringComparison.OrdinalIgnoreCase) ||
                             chatHistory.Any(m => m.Items.Any(i => i is ImageContent));

        string responseContent;
        if (isVisionRequest)
        {
            responseContent = "A high-quality image showing a professional business document snippet.";
        }
        else
        {
            var userInput = chatHistory.Count > 0 ? chatHistory[^1].Content ?? "nothing" : "nothing";
            responseContent = $"[MOCK AI] This is a free local response! You asked: '{userInput}'. \n\n(Tokens saved!)";
        }

        var response = new ChatMessageContent(AuthorRole.Assistant, responseContent);
        return new[] { response };
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }
}
