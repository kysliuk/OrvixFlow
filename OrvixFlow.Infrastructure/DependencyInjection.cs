using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Data
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Host=localhost;Database=orvixflow;Username=postgres;Password=postgres";
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        // AI
        services.AddOrvixSemanticKernel(configuration);

        return services;
    }

    private static IServiceCollection AddOrvixSemanticKernel(this IServiceCollection services, IConfiguration configuration)
    {
        var aiSection = configuration.GetSection("AI");
        var provider = aiSection["Provider"] ?? "OpenAI";

        var kernelBuilder = services.AddKernel();

        if (provider == "Mock")
        {
            kernelBuilder.Services.AddSingleton<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService, OrvixFlow.Infrastructure.Ai.Mock.MockChatCompletionService>();
            kernelBuilder.Services.AddSingleton<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService, OrvixFlow.Infrastructure.Ai.Mock.MockTextEmbeddingGenerationService>();
        }
        else
        {
            var apiKey = aiSection["OpenAI:ApiKey"] ?? throw new System.Exception("AI:OpenAI:ApiKey missing");
            var modelId = aiSection["OpenAI:ModelId"] ?? "gpt-4o";

            kernelBuilder
                .AddOpenAIChatCompletion(modelId, apiKey)
                .AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey);
        }
            
        services.AddHttpClient("n8n", client =>
        {
            client.BaseAddress = new System.Uri("http://localhost:5678");
            client.DefaultRequestHeaders.Add("X-Automation-Key", configuration.GetValue<string>("AutomationKey"));
        });

        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin>();
        services.AddScoped<OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin>();

        return services;
    }
}
