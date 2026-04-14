using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrvixFlow.Api.Services;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;
using OrvixFlow.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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
    
    // Add a global header input in Swagger UI for the Tenant ID
    config.AddSecurity("TenantId", System.Linq.Enumerable.Empty<string>(), new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
        Name = "X-Tenant-ID",
        In = NSwag.OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter Tenant ID (e.g., 00000000-0000-0000-0000-000000000001)"
    });
    config.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("TenantId"));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<OrvixFlow.Api.Health.RagHealthCheck>("rag");

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    // F-03 FIX: Per-IP rate limiting on login endpoint to prevent brute-force attacks.
    // 5 attempts per minute per IP address.
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Existing upload policy
    options.AddFixedWindowLimiter(policyName: "upload", options =>
    {
        options.PermitLimit = 10;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 2;
    });

    // F-27 FIX: Rate limiting on AI-consuming endpoints
    // Per-tenant+IP to prevent AI API cost abuse
    options.AddPolicy("ai-process", context =>
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
    options.AddPolicy("ai-ingest", context =>
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
    options.AddPolicy("ai-search", context =>
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Database=orvixflow;Username=postgres;Password=postgres";

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(connectionString));
builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

// F-32 FIX: Add HTTP security headers before authentication middleware.
// These headers protect against XSS, clickjacking, MIME sniffing, and enforce HTTPS.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    // Content-Security-Policy should be added once inline scripts are audited.
    await next();
});

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
app.MapHealthChecks("/health/rag");

// F-22 FIX: Protect Hangfire dashboard with SuperAdmin JWT authentication.
// The custom HangfireDashboardAuthorizationFilter checks for the SuperAdmin role claim
// in the JWT, replacing the insecure LocalRequestsOnlyAuthorizationFilter.
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new OrvixFlow.Api.Filters.HangfireDashboardAuthorizationFilter() }
});

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

// F-21: AuditTrail retention job - run daily at 3am to purge records older than 90 days
recurringJobManager.AddOrUpdate<OrvixFlow.Api.Jobs.AuditRetentionJob>(
    "audit-retention",
    job => job.ExecuteAsync(),
    "0 3 * * *");

app.Run();
