using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure.Ai;
using OrvixFlow.Infrastructure.Auth;
using OrvixFlow.Infrastructure.Data;
using OrvixFlow.Infrastructure.Services;
using OrvixFlow.Infrastructure.Ai.Parsers;
using OrvixFlow.Infrastructure.Ai.Chunking;
using OrvixFlow.Infrastructure.Ai.Jobs;
using OrvixFlow.Infrastructure.Storage;
using OrvixFlow.Infrastructure.Services.Security;

namespace OrvixFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Host=localhost;Database=orvixflow;Username=postgres;Password=postgres";
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        services.AddSingleton<Hangfire.PostgreSql.PostgreSqlStorage>(_ => 
            new Hangfire.PostgreSql.PostgreSqlStorage(connectionString));

        services.AddSingleton<OrvixFlow.Core.Interfaces.ITenantProviderFactory, TenantProviderFactory>();
        services.AddScoped<ScopedTenantProviderFactory>();

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
            kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OrvixFlow.Infrastructure.Ai.Mock.MockEmbeddingGenerator>();
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
                    kernelBuilder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, OrvixFlow.Infrastructure.Ai.Mock.MockEmbeddingGenerator>();
                }
                else
                {
#pragma warning disable SKEXP0010
                    kernelBuilder.Services.AddOpenAIEmbeddingGenerator("text-embedding-3-small", apiKey, httpClient: customHttpClient);
#pragma warning restore SKEXP0010
                }
            }
            else
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
#pragma warning disable SKEXP0010
                kernelBuilder.Services.AddOpenAIEmbeddingGenerator("text-embedding-3-small", apiKey);
#pragma warning restore SKEXP0010
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

        services.AddScoped<IIntentClassifierService, IntentClassifierService>();
        services.AddScoped<IDraftGeneratorService, DraftGeneratorService>();
        services.AddScoped<IHybridVectorSearchService, HybridVectorSearchService>();
        services.AddScoped<IInboxEventRepository, InboxEventRepository>();
        services.AddScoped<IPolicyGateService, PolicyGateService>();
        services.AddScoped<IWebhookCallbackService, WebhookCallbackService>();
        services.AddScoped<ITenantWebhookRateLimiter, TenantWebhookRateLimiter>();
        services.AddScoped<IDraftFeedbackService, DraftFeedbackService>();
        services.AddScoped<IN8nProvisioningService, N8nProvisioningService>();
        services.AddScoped<IPlanService, PlanService>();
        services.AddScoped<IEntitlementResolver, EntitlementResolver>();
        services.AddScoped<ICompanySubscriptionService, CompanySubscriptionService>();
        
        services.AddScoped<IReranker, LlmScorerReranker>();

        // RAG Extension Phase 1 & 3
        services.AddScoped<IChunker, OverlapChunker>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IIngestionPipelineService, IngestionPipelineService>();
        services.AddScoped<IDocumentParser, PlainTextParser>();
        services.AddScoped<IDocumentParser, PdfParser>();
        services.AddScoped<IDocumentParser, DocxParser>();
        services.AddScoped<IDocumentParser, ImageFileParser>();
        services.AddScoped<IImageResolver, ImageResolver>();
        services.AddScoped<FileIngestionJob>();

        // Virus scanning (configurable)
        var virusScanProvider = configuration["Security:VirusScan:Provider"] ?? "Noop";
        if (virusScanProvider == "ClamAv")
        {
            services.Configure<ClamAvOptions>(configuration.GetSection("Security:VirusScan:ClamAv"));
            services.AddScoped<IClamAvClient, NclamClient>();
            services.AddScoped<IVirusScanService, ClamAvVirusScanService>();
        }
        else
        {
            services.AddScoped<IVirusScanService, NoopVirusScanService>();
        }

        // Shadow modules
        services.AddScoped<IAuditService, OrvixFlow.Infrastructure.Shadow.AuditService>();
        services.AddScoped<IUsageService, OrvixFlow.Infrastructure.Shadow.UsageService>();
        
        // Email Service
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailProvider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Console";
        if (emailProvider.Equals("Smtp", System.StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, MockEmailService>();
        }

        services.AddScoped<IVirusScanService, NoopVirusScanService>();
        services.AddScoped<IRagMetricsCollector, RagMetricsCollector>();

        return services;
    }
}
