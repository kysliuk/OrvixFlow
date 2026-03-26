using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:8080") };
        var email = $"test_{Guid.NewGuid()}@example.com";
        var pw = "Password123!";
        
        // 1. Register
        var regJson = JsonSerializer.Serialize(new { Email = email, Password = pw, DisplayName = "Test User" });
        var regRes = await client.PostAsync("/api/auth/register", new StringContent(regJson, Encoding.UTF8, "application/json"));
        var regBody = await regRes.Content.ReadAsStringAsync();
        Console.WriteLine($"Register: {regRes.StatusCode}");
        
        var token = JsonSerializer.Deserialize<JsonElement>(regBody).GetProperty("token").GetString();
        
        // 2. Add auth header
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        
        // 3. POST /api/org
        var orgJson = JsonSerializer.Serialize(new { Name = "Test Org" });
        var orgRes = await client.PostAsync("/api/org", new StringContent(orgJson, Encoding.UTF8, "application/json"));
        Console.WriteLine($"POST /api/org: {orgRes.StatusCode}");
        Console.WriteLine(await orgRes.Content.ReadAsStringAsync());
    }
}
