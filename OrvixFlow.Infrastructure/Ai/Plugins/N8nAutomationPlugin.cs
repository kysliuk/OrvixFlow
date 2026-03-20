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
        [Description("The unique path name of the n8n webhook to trigger (e.g. 'send-email', 'process-data')")] string webhookPath,
        [Description("A valid JSON string representing the structured parameter data required by the workflow")] string payloadJson)
    {
        try
        {
            var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"/webhook/{webhookPath}", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                return $"Workflow '{webhookPath}' successfully triggered. Response from n8n: {responseBody}";
            }
            
            var errorBody = await response.Content.ReadAsStringAsync();
            return $"Failed to trigger workflow '{webhookPath}'. HTTP {response.StatusCode} - {errorBody}";
        }
        catch (HttpRequestException ex)
        {
            return $"Critical Error connecting to n8n automation engine: {ex.Message}";
        }
    }
}
