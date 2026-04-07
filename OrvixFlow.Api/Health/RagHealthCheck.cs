using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrvixFlow.Infrastructure.Data;

namespace OrvixFlow.Api.Health;

public class RagHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public RagHealthCheck(AppDbContext dbContext, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _dbContext = dbContext;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check DB & pgvector
            // We run a simple query that uses vector operations to ensure pgvector is installed and working
            var canQueryVector = await _dbContext.Database.ExecuteSqlRawAsync(
                "SELECT '[1,1,1]'::vector <=> '[1,1,1]'::vector", cancellationToken) == -1; // ExecuteSqlRaw returns -1 for SELECT

            // 2. Check Embedding Service (Connectivity)
            var testEmbedding = await _embeddingGenerator.GenerateAsync(new[] { "healthcheck" }, cancellationToken: cancellationToken);
            var canEmbed = testEmbedding.Count > 0;

            if (canEmbed)
            {
                return HealthCheckResult.Healthy("RAG services are operational.");
            }

            return HealthCheckResult.Degraded("AI Embedding service returned empty results.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RAG health check failed.", ex);
        }
    }
}
