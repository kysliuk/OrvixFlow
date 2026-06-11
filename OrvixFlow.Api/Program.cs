using System.Text;
using System.Threading;
using Hangfire;
using Hangfire.PostgreSql;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrvixFlow.Api.Services;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;
using OrvixFlow.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using OrvixFlow.Api.Security;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OrvixFlow.Api.Filters;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrvixFlow")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(context.Configuration["Logging:SeqUrl"] ?? "http://localhost:5341",
        apiKey: context.Configuration["Logging:SeqApiKey"]));

// CORS – allow Next.js frontend
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(
        "http://localhost:3000",
        builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// Disable automatic JWT claims mapping to URI Schemas
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// JWT Bearer
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new Exception("Jwt:Secret missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "orvixflow",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "orvixflow-web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireClaim("Role", "SuperAdmin"));
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireAssertion(ctx =>
        {
            var role = ctx.User.FindFirst("Role")?.Value;
            return role == "SuperAdmin" || role == "InternalOperator";
        }));
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "v1";
    config.Title = "OrvixFlow API";
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<OrvixFlow.Api.Health.RagHealthCheck>("rag", tags: new[] { "rag" })
    .AddCheck<OrvixFlow.Api.Health.StorageHealthCheck>("storage", tags: new[] { "storage", "readiness" });

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // F-03 FIX: Per-IP rate limiting on login endpoint to prevent brute-force attacks.
    // 5 attempts per minute per IP address.
    options.AddPolicy(RateLimitPolicyNames.Login, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => RateLimitPolicies.CreateLoginOptions()));

    // P0-3: Per-IP rate limiting on register endpoint to prevent bulk account creation and email flooding.
    // 10 attempts per hour per IP address.
    options.AddPolicy(RateLimitPolicyNames.Register, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => RateLimitPolicies.CreateRegisterOptions()));

    // Existing upload policy
    options.AddFixedWindowLimiter(policyName: RateLimitPolicyNames.Upload, options =>
    {
        options.PermitLimit = 10;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 2;
    });

    // F-27 FIX: Rate limiting on AI-consuming endpoints
    // Per-tenant+IP to prevent AI API cost abuse
    options.AddPolicy(RateLimitPolicyNames.AiProcess, context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? context.User.FindFirst("ActiveCompanyId")?.Value ?? "no-tenant";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{tenantId}:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Rate limiting on embedding API calls
    options.AddPolicy(RateLimitPolicyNames.AiIngest, context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? context.User.FindFirst("ActiveCompanyId")?.Value ?? "no-tenant";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{tenantId}:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Rate limiting on knowledge base search
    options.AddPolicy(RateLimitPolicyNames.AiSearch, context =>
    {
        var tenantId = context.User.FindFirst("TenantId")?.Value ?? context.User.FindFirst("ActiveCompanyId")?.Value ?? "no-tenant";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{tenantId}:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddSingleton<ITenantProviderFactory, TenantProviderFactory>(); 
builder.Services.AddInfrastructure(builder.Configuration);

// P5-1: OpenTelemetry — traces and metrics
var otlpEndpoint = builder.Configuration["Telemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation();
        
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
        
        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Database=orvixflow;Username=postgres;Password=postgres";

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(connectionString));
builder.Services.AddHangfireServer();

var app = builder.Build();

GlobalJobFilters.Filters.Add(new JobFailureAlertFilter(
    app.Services.GetRequiredService<ILogger<JobFailureAlertFilter>>()));

// F-32 FIX: Add HTTP security headers before authentication middleware.
// These headers protect against XSS, clickjacking, MIME sniffing, and enforce HTTPS.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Content-Security-Policy"] = SecurityHeaderPolicies.ApiContentSecurityPolicy;

        return Task.CompletedTask;
    });

    await next();
});

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

// F-32 FIX: HSTS (HTTP Strict Transport Security) in production only.
// Tells browsers to only connect via HTTPS for the specified max-age.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<OrvixFlow.Api.Middleware.HmacSignatureMiddleware>();
app.MapControllers();
app.MapHealthChecks("/health/rag", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("rag")
});
app.MapHealthChecks("/health/storage", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("storage")
});

// F-22 FIX: Protect Hangfire dashboard with SuperAdmin JWT authentication.
// The custom HangfireDashboardAuthorizationFilter checks for the SuperAdmin role claim
// in the JWT, replacing the insecure LocalRequestsOnlyAuthorizationFilter.
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new OrvixFlow.Api.Filters.HangfireDashboardAuthorizationFilter() }
});

// F-18 FIX: Warn on startup if virus scanning is disabled in production
if (!app.Environment.IsDevelopment())
{
    var virusScanProvider = builder.Configuration["Security:VirusScan:Provider"] ?? "Noop";
    if (virusScanProvider == "Noop")
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  WARNING: Virus scanning is DISABLED in production!              ║");
        Console.WriteLine("║  Set Security:VirusScan:Provider to 'ClamAv' in appsettings.    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }
}

// T2-4: Warn on startup if Stripe is not configured
var webhookSecret = builder.Configuration["Stripe:WebhookSecret"];
if (string.IsNullOrEmpty(webhookSecret))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  SECURITY: Stripe:WebhookSecret is NOT configured.               ║");
    Console.WriteLine("║  Stripe webhook endpoint will reject all requests.                ║");
    Console.WriteLine("║  Configure Stripe__WebhookSecret in environment.                  ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
}

var stripeKey = builder.Configuration["Stripe:SecretKey"];
if (string.IsNullOrEmpty(stripeKey))
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║  WARNING: Stripe:SecretKey is NOT configured.                    ║");
    Console.WriteLine("║  Checkout and portal endpoints will throw InvalidOperationException.║");
    Console.WriteLine("║  Configure Stripe__SecretKey in environment.                     ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrvixFlow.Infrastructure.Data.AppDbContext>();
    db.Database.Migrate();

    var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
    await OrvixFlow.Infrastructure.Data.DbInitializer.SeedAsync(db, logger);
}

// Register recurring jobs using service-based API
var recurringJobManager = app.Services.GetRequiredService<Hangfire.IRecurringJobManager>();
recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.TrialExpirationJob>(
    "trial-expiration-check",
    job => job.ExecuteAsync(),
    "0 */6 * * *");

// Phase 3: Usage period rollover - run daily to advance expired billing periods
recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.UsagePeriodRolloverJob>(
    "usage-period-rollover",
    job => job.ExecuteAsync(),
    "0 0 * * *");

// F-21: AuditTrail retention job - run daily at 3am to purge records older than 90 days
recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.AuditRetentionJob>(
    "audit-retention",
    job => job.ExecuteAsync(),
    "0 3 * * *");

// T3-4: Notification processor - run every 5 minutes to send queued alerts
recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.NotificationProcessorJob>(
    "notification-processor",
    job => job.ExecuteAsync(),
    "*/5 * * * *");

recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.ArchivedCompanyPurgeJob>(
    "archived-company-purge",
    job => job.ExecuteAsync(),
    "0 4 * * *");

recurringJobManager.AddOrUpdate<OrvixFlow.Infrastructure.Storage.OrphanDetectionJob>(
    "storage-orphan-detection",
    job => job.RunAsync(CancellationToken.None),
    "0 2 * * *");

app.Run();
