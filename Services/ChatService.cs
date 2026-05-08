using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartEventManagement.Services;

public class ChatService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string Model = "claude-sonnet-4-6";
    private const string SystemPrompt = """
        You are EventHub's smart assistant. Help users with:
        - Finding and browsing events (guide them to Browse Events or Recommendations)
        - Understanding how to register, cancel, or manage bookings
        - Explaining how to create or manage events (for organisers)
        - Answering questions about notifications, feedback, and account settings
        - General FAQs about the EventHub / SmartEvents platform
        Be concise, friendly, and helpful. If asked about specific event data, guide users to use the app's search and browse features. Keep responses short and to the point.
        """;

    public ChatService(IConfiguration config, HttpClient http)
    {
        _http = http;
        _apiKey = config["Anthropic:ApiKey"] ?? string.Empty;
    }

    public async Task<string> SendMessageAsync(List<(string role, string content)> history)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "Chatbot is not configured. Please add an Anthropic API key in settings.";

        var messages = history.Select(m => new { role = m.role, content = m.content }).ToArray();

        var body = JsonSerializer.Serialize(new
        {
            model = Model,
            max_tokens = 512,
            system = SystemPrompt,
            messages
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return "Sorry, I couldn't process that request. Please try again.";

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "No response.";
    }
}
