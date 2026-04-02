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
    options.AddFixedWindowLimiter(policyName: "upload", options =>
    {
        options.PermitLimit = 10;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 2;
    });
});
builder.Services.AddScoped<ITenantProvider, TenantProvider>(); 
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
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrvixFlow.Infrastructure.Data.AppDbContext>();
    db.Database.Migrate(); // Apply pending migrations on startup
}

app.Run();
