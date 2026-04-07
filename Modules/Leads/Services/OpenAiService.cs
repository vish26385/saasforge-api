using System.Text.Json;

namespace SaaSForge.Api.Modules.Leads.Services
{
    public interface IOpenAiService
    {
        Task<string> GenerateAsync(string prompt);
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly HttpClient _http;

        public OpenAiService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            var request = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                new { role = "user", content = prompt }
            },
                temperature = 0.7
            };

            var response = await _http.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            return json
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";
        }
    }
}
