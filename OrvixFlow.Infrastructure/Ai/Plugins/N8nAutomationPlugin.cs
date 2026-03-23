using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace OrvixFlow.Infrastructure.Ai.Plugins;

public class N8nAutomationPlugin
{
    private readonly HttpClient _httpClient;

    public N8nAutomationPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("n8n");
    }

    [KernelFunction("trigger_automation_workflow")]
    [Description("Triggers an external automation workflow in n8n to perform real-world actions like sending emails, processing files, or scraping websites.")]
    public async Task<string> TriggerWorkflowAsync(
        [Description("The unique path name of the n8n webhook to trigger (e.g. 'draft-reply', 'escalate-to-human')")] string webhookPath,
        [Description("The text message, summary, or formulated response to send to the workflow")] string messageData)
    {
        try
        {
            var payloadInfo = new { data = messageData };
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payloadInfo);
            var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
            
            // In local development, n8n 'Listen for Test Event' requires the /webhook-test/ prefix instead of /webhook/
            var response = await _httpClient.PostAsync($"/webhook-test/{webhookPath}", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                return $"Workflow '{webhookPath}' successfully triggered. Response from n8n: {responseBody}";
            }
            
            var errorBody = await response.Content.ReadAsStringAsync();
            return $"CRITICAL ERROR: Failed to trigger workflow '{webhookPath}'. HTTP {response.StatusCode}. The workflow does not exist yet. DO NOT RETRY THIS TOOL. Please just report this failure to the user.";
        }
        catch (HttpRequestException ex)
        {
            return $"Critical Error connecting to n8n automation engine: {ex.Message}";
        }
    }
}
