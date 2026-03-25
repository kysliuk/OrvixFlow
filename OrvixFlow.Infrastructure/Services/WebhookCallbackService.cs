using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrvixFlow.Core.Models;

namespace OrvixFlow.Infrastructure.Services;

public interface IWebhookCallbackService
{
    Task SendCallbackAsync(string webhookPath, PolicyDecision decision, Guid inboxEventId, string? draftResponse);
}

public class WebhookCallbackService : IWebhookCallbackService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookCallbackService> _logger;

    public WebhookCallbackService(IHttpClientFactory httpClientFactory, ILogger<WebhookCallbackService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendCallbackAsync(string webhookPath, PolicyDecision decision, Guid inboxEventId, string? draftResponse)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("n8n");

            var payload = new
            {
                inboxEventId = inboxEventId,
                decision = decision.Decision.ToString(),
                reason = decision.Reason,
                category = decision.Category,
                confidenceScore = decision.ConfidenceScore,
                actionRequestId = decision.ActionRequestId,
                draftResponse = draftResponse,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"/webhook-test/{webhookPath}", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Callback sent successfully: {WebhookPath} for EventId: {EventId}",
                    webhookPath,
                    inboxEventId);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "Callback returned non-success: {StatusCode} for {WebhookPath}. Body: {ErrorBody}",
                    response.StatusCode,
                    webhookPath,
                    errorBody);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to send callback to webhook: {WebhookPath} for EventId: {EventId}",
                webhookPath,
                inboxEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error sending callback: {WebhookPath} for EventId: {EventId}",
                webhookPath,
                inboxEventId);
        }
    }
}
