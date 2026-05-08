using Microsoft.AspNetCore.Mvc;
using SmartEventManagement.Services;

namespace SmartEventManagement.Controllers;

[Route("api/chat")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    public record ChatMessage(string Role, string Content);
    public record ChatRequest(List<ChatMessage> History);

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] ChatRequest request)
    {
        if (request.History == null || request.History.Count == 0)
            return BadRequest("No messages provided.");

        var history = request.History
            .Select(m => (m.Role, m.Content))
            .ToList();

        var reply = await _chatService.SendMessageAsync(history);
        return Ok(new { reply });
    }
}
