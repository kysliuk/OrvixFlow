using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Auth;
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

        // Auth / Scope
        services.AddScoped<OrvixFlow.Core.Interfaces.IScopeContext, OrvixFlow.Infrastructure.Auth.ScopeContext>();

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
            var baseUrl = aiSection["OpenAI:BaseUrl"];

            System.Net.Http.HttpClient? customHttpClient = null;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                customHttpClient = new System.Net.Http.HttpClient();
                customHttpClient.BaseAddress = new System.Uri(baseUrl);
            }

            if (customHttpClient != null)
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: customHttpClient);
                
                // Groq does not host embedding models. Fallback to mock generation for testing.
                if (baseUrl?.Contains("groq", System.StringComparison.OrdinalIgnoreCase) == true)
                {
                    kernelBuilder.Services.AddSingleton<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService, OrvixFlow.Infrastructure.Ai.Mock.MockTextEmbeddingGenerationService>();
                }
                else
                {
                    kernelBuilder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey, httpClient: customHttpClient);
                }
            }
            else
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
                kernelBuilder.AddOpenAITextEmbeddingGeneration("text-embedding-3-small", apiKey);
            }
        }
            
        services.AddHttpClient("n8n", client =>
        {
            var n8nUrl = configuration["Automation:N8nBaseUrl"] ?? "http://localhost:5678";
            client.BaseAddress = new System.Uri(n8nUrl);
            client.DefaultRequestHeaders.Add("X-Automation-Key", configuration.GetValue<string>("AutomationKey"));
        });

        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IInboxGuardianService, InboxGuardianService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IAuthService, OrvixFlow.Infrastructure.Auth.AuthService>();
        services.AddScoped<IAccessResolver, AccessResolver>();
        services.AddScoped<OrvixFlow.Infrastructure.Ai.Plugins.KnowledgeBaseSearchPlugin>();
        services.AddScoped<OrvixFlow.Infrastructure.Ai.Plugins.N8nAutomationPlugin>();

        // Shadow modules
        services.AddScoped<IAuditService, OrvixFlow.Infrastructure.Shadow.AuditService>();
        services.AddScoped<IUsageService, OrvixFlow.Infrastructure.Shadow.UsageService>();

        return services;
    }
}
