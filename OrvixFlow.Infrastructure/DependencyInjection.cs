using System;
using Amazon.S3;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
using OrvixFlow.Infrastructure.Services.Stripe;

namespace OrvixFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
                               ?? "Host=localhost;Database=orvixflow;Username=postgres;Password=postgres";
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        services.AddSingleton<PostgreSqlStorage>(_ => 
            new PostgreSqlStorage(connectionString));

        services.AddSingleton<ITenantProviderFactory, TenantProviderFactory>();
        services.AddScoped<ScopedTenantProviderFactory>();

        // Auth / Scope
        services.AddScoped<IScopeContext, ScopeContext>();

        // AI
        services.AddOrvixSemanticKernel(configuration);

        // Phase 5: Stripe services
        services.AddScoped<IStripeService, StripeService>();
        services.AddScoped<StripeWebhookService>();

        // T3-4: Usage Alert Service
        services.AddScoped<IUsageAlertService, UsageAlertService>();

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
            var apiKey = aiSection["OpenAI:ApiKey"] ?? throw new Exception("AI:OpenAI:ApiKey missing");
            var modelId = aiSection["OpenAI:ModelId"] ?? "gpt-4o";
            var baseUrl = aiSection["OpenAI:BaseUrl"];

            System.Net.Http.HttpClient? customHttpClient = null;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                customHttpClient = new System.Net.Http.HttpClient();
                customHttpClient.BaseAddress = new Uri(baseUrl);
            }

            if (customHttpClient != null)
            {
                kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: customHttpClient);
                
                // Groq does not host embedding models. Fallback to mock generation for testing.
                if (baseUrl?.Contains("groq", StringComparison.OrdinalIgnoreCase) == true)
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
            client.BaseAddress = new Uri(n8nUrl);
            client.DefaultRequestHeaders.Add("X-Automation-Key", configuration.GetValue<string>("AutomationKey"));
        });

        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IInboxGuardianService, InboxGuardianService>();
        services.AddScoped<IIngestionService, IngestionService>();
        services.AddScoped<IAuthService, OrvixFlow.Infrastructure.Auth.AuthService>();
        services.AddScoped<ICompanyBootstrapService, CompanyBootstrapService>();
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

        var storageProvider = (configuration["Storage:Provider"] ?? "Local").ToUpperInvariant();
        switch (storageProvider)
        {
            case "MINIO":
                // MinIO: default object storage provider for local Docker, CI, and staging-like environments.
                var minioSection = configuration.GetSection("Storage:MinIO");
                var bucket = minioSection["Bucket"] ?? "orvixflow";

                services.AddSingleton<IAmazonS3>(_ =>
                {
                    var endpoint = minioSection["Endpoint"]
                        ?? throw new InvalidOperationException("Storage:MinIO:Endpoint is required when Storage:Provider=MinIO");
                    var accessKey = configuration["MINIO_ACCESS_KEY"]
                        ?? throw new InvalidOperationException("MINIO_ACCESS_KEY environment variable is required");
                    var secretKey = configuration["MINIO_SECRET_KEY"]
                        ?? throw new InvalidOperationException("MINIO_SECRET_KEY environment variable is required");

                    var minioConfig = new AmazonS3Config
                    {
                        ServiceURL = endpoint,
                        ForcePathStyle = true,
                        UseHttp = !minioSection.GetValue<bool>("UseSSL")
                    };

                    return new AmazonS3Client(accessKey, secretKey, minioConfig);
                });

                services.AddSingleton<MinIOBucketInitializer>(sp =>
                    new MinIOBucketInitializer(
                        sp.GetRequiredService<IAmazonS3>(),
                        bucket,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MinIOBucketInitializer>>()));
                services.AddHostedService(sp => sp.GetRequiredService<MinIOBucketInitializer>());

                services.AddScoped<IFileStorage>(sp =>
                    new MinIOFileStorage(
                        sp.GetRequiredService<IAmazonS3>(),
                        bucket,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MinIOFileStorage>>()));
                break;

            case "AZUREBLOB":
                // Azure Blob: production object storage provider.
                var azureSection = configuration.GetSection("Storage:AzureBlob");
                var connectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"]
                    ?? throw new InvalidOperationException(
                        "AZURE_STORAGE_CONNECTION_STRING env var is required when Storage:Provider=AzureBlob");
                var containerName = azureSection["ContainerName"] ?? "orvixflow";

                services.AddSingleton(sp =>
                {
                    var serviceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
                    return serviceClient.GetBlobContainerClient(containerName);
                });

                services.AddSingleton<AzureBlobContainerInitializer>(sp =>
                    new AzureBlobContainerInitializer(
                        sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureBlobContainerInitializer>>()));
                services.AddHostedService(sp => sp.GetRequiredService<AzureBlobContainerInitializer>());

                services.AddScoped<IFileStorage>(sp =>
                    new AzureBlobFileStorage(
                        sp.GetRequiredService<Azure.Storage.Blobs.BlobContainerClient>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AzureBlobFileStorage>>()));
                break;

            case "LOCAL":
                // Local: legacy dev-only fallback. Keep for tests and non-Docker local development only.
                services.AddScoped<IFileStorage, LocalFileStorage>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Storage:Provider '{storageProvider}'. Expected Local, MinIO, or AzureBlob.");
        }

        services.AddScoped<IIngestionPipelineService, IngestionPipelineService>();
        services.AddScoped<IDocumentParser, PlainTextParser>();
        services.AddScoped<IDocumentParser, PdfParser>();
        services.AddScoped<IDocumentParser, DocxParser>();
        services.AddScoped<IDocumentParser, ImageFileParser>();
        services.AddScoped<IImageResolver, ImageResolver>();
        services.AddScoped<FileIngestionJob>();
        services.AddScoped<LocalToMinioMigrationJob>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var bucket = config["Storage:MinIO:Bucket"] ?? "orvixflow";
            var basePath = config["Storage:Local:BasePath"] ?? "/app/uploads";
            var s3 = sp.GetService<IAmazonS3>();
            var ownsS3Client = false;

            if (s3 == null)
            {
                var endpoint = config["Storage:MinIO:Endpoint"] ?? "http://minio:9000";
                var accessKey = config["MINIO_ACCESS_KEY"]
                    ?? throw new InvalidOperationException("MINIO_ACCESS_KEY environment variable is required for storage migration");
                var secretKey = config["MINIO_SECRET_KEY"]
                    ?? throw new InvalidOperationException("MINIO_SECRET_KEY environment variable is required for storage migration");

                var minioConfig = new AmazonS3Config
                {
                    ServiceURL = endpoint,
                    ForcePathStyle = true,
                    UseHttp = !config.GetValue<bool>("Storage:MinIO:UseSSL")
                };

                s3 = new AmazonS3Client(accessKey, secretKey, minioConfig);
                ownsS3Client = true;
            }

            return new LocalToMinioMigrationJob(
                sp.GetRequiredService<AppDbContext>(),
                s3,
                bucket,
                basePath,
                sp.GetRequiredService<ILogger<LocalToMinioMigrationJob>>(),
                ownsS3Client);
        });

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
        ValidateEmailConfiguration(configuration, emailProvider);
        services.AddHttpClient("resend-email", client =>
        {
            var resendBaseUrl = configuration[$"{EmailOptions.SectionName}:ResendBaseUrl"] ?? "https://api.resend.com";
            client.BaseAddress = new Uri(resendBaseUrl.TrimEnd('/') + "/");
        });

        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else if (emailProvider.Equals("Resend", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, ResendEmailService>();
        }
        else if (emailProvider.Equals("Console", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailService, MockEmailService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported Email:Provider '{emailProvider}'. Expected Console, Smtp, or Resend.");
        }

        services.AddScoped<IRagMetricsCollector, RagMetricsCollector>();

        return services;
    }

    private static void ValidateEmailConfiguration(IConfiguration configuration, string emailProvider)
    {
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();

        if (string.IsNullOrWhiteSpace(emailOptions.FromEmail))
        {
            throw new InvalidOperationException("Email:FromEmail is required.");
        }

        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(emailOptions.SmtpHost))
            {
                throw new InvalidOperationException("Email:SmtpHost is required when Email:Provider=Smtp.");
            }

            if (emailOptions.SmtpPort <= 0)
            {
                throw new InvalidOperationException("Email:SmtpPort must be greater than 0 when Email:Provider=Smtp.");
            }

            var hasUser = !string.IsNullOrWhiteSpace(emailOptions.SmtpUser);
            var hasPass = !string.IsNullOrWhiteSpace(emailOptions.SmtpPass);
            if (hasUser != hasPass)
            {
                throw new InvalidOperationException(
                    "Email:SmtpUser and Email:SmtpPass must both be provided together when Email:Provider=Smtp.");
            }

            return;
        }

        if (emailProvider.Equals("Resend", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(emailOptions.ResendApiKey))
        {
            throw new InvalidOperationException("Email:ResendApiKey is required when Email:Provider=Resend.");
        }
    }
}
