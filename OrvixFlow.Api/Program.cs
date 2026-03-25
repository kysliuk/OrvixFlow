using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrvixFlow.Api.Services;
using OrvixFlow.Core.Interfaces;
using OrvixFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// CORS – allow Next.js frontend
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(
        "http://localhost:3000",
        builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

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
builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddControllers();
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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OrvixFlow.Api.Middleware.HmacSignatureMiddleware>();
app.MapControllers();
app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.Run();
