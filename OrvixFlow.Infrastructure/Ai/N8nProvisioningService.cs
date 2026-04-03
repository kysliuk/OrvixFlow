using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Interfaces;

namespace OrvixFlow.Infrastructure.Ai;

public interface IN8nProvisioningService
{
    Task<string> ProvisionWorkflowAsync(string templateWorkflowId, string mailboxEmail, Guid tenantId);
    Task<bool> DeleteWorkflowAsync(string workflowId);
    Task<string> CreateCredentialAsync(string provider, string email, object credentials);
    Task<bool> DeleteCredentialAsync(string credentialId);
    Task<bool> TestConnectionAsync();
}

public class N8nProvisioningService : IN8nProvisioningService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<N8nProvisioningService> _logger;

    public N8nProvisioningService(IHttpClientFactory httpClientFactory, ILogger<N8nProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> ProvisionWorkflowAsync(string templateWorkflowId, string mailboxEmail, Guid tenantId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");

            var getResponse = await client.GetAsync($"/api/v1/workflows/{templateWorkflowId}");
            getResponse.EnsureSuccessStatusCode();

            var templateJson = await getResponse.Content.ReadAsStringAsync();
            using var templateDoc = JsonDocument.Parse(templateJson);
            var templateData = templateDoc.RootElement.GetProperty("data");

            var payload = new
            {
                name = $"OrvixFlow - {mailboxEmail}",
                nodes = templateData.GetProperty("nodes"),
                connections = templateData.GetProperty("connections"),
                settings = templateData.GetProperty("settings")
            };

            var createResponse = await client.PostAsJsonAsync("/api/v1/workflows", payload);
            createResponse.EnsureSuccessStatusCode();

            var createdJson = await createResponse.Content.ReadAsStringAsync();
            using var createdDoc = JsonDocument.Parse(createdJson);
            var workflowId = createdDoc.RootElement.GetProperty("data").GetProperty("id").GetString() ?? string.Empty;

            _logger.LogInformation("Provisioned n8n workflow {WorkflowId} for {Email} tenant {TenantId}",
                workflowId, mailboxEmail, tenantId);

            return workflowId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision n8n workflow for {Email} tenant {TenantId}",
                mailboxEmail, tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteWorkflowAsync(string workflowId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");
            var response = await client.DeleteAsync($"/api/v1/workflows/{workflowId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete n8n workflow {WorkflowId}", workflowId);
            return false;
        }
    }

    public async Task<string> CreateCredentialAsync(string provider, string email, object credentials)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");

            var credentialType = provider.ToLowerInvariant() switch
            {
                "gmail" => "gmailOAuth2Api",
                "outlook" => "microsoftOAuth2Api",
                _ => "imap"
            };

            var payload = new
            {
                name = $"{provider} - {email}",
                type = credentialType,
                data = credentials
            };

            var response = await client.PostAsJsonAsync("/api/v1/credentials", payload);
            response.EnsureSuccessStatusCode();

            var createdJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(createdJson);
            var credentialId = doc.RootElement.GetProperty("data").GetProperty("id").GetString() ?? string.Empty;

            _logger.LogInformation("Created n8n credential {CredentialId} for {Email}", credentialId, email);

            return credentialId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create n8n credential for {Email}", email);
            throw;
        }
    }

    public async Task<bool> DeleteCredentialAsync(string credentialId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");
            var response = await client.DeleteAsync($"/api/v1/credentials/{credentialId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete n8n credential {CredentialId}", credentialId);
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");
            var response = await client.GetAsync("/api/v1/credentials");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "n8n connection test failed");
            return false;
        }
    }
}
