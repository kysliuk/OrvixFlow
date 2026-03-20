using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace OrvixFlow.Infrastructure.Ai.Mock;

public class MockChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?>? Attributes => new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var userInput = chatHistory.Count > 0 ? chatHistory[^1].Content : "nothing";
        var response = new ChatMessageContent(AuthorRole.Assistant, 
            $"[MOCK AI] This is a free local response! You asked: '{userInput}'. \n\n(Tokens saved!)");
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(new[] { response });
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        throw new System.NotImplementedException();
    }
}
