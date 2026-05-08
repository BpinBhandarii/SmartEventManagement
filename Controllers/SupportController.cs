using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;
using SmartEventManagement.Hubs;
using SmartEventManagement.Models;

namespace SmartEventManagement.Controllers;

[Route("api/support")]
[ApiController]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<NotificationHub> _hub;

    public SupportController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IHubContext<NotificationHub> hub)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var userId = _userManager.GetUserId(User)!;
        var queries = await _db.SupportQueries
            .Where(q => q.UserId == userId)
            .OrderByDescending(q => q.AskedAt)
            .Take(20)
            .Select(q => new
            {
                q.Id,
                q.QuestionText,
                q.Answer,
                q.Status,
                q.AskedAt,
                q.AnsweredAt
            })
            .ToListAsync();
        return Ok(queries);
    }

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var userId = _userManager.GetUserId(User)!;
        var user = await _userManager.FindByIdAsync(userId);

        var query = new SupportQuery
        {
            UserId = userId,
            QuestionText = request.Question.Trim(),
            AskedAt = DateTime.UtcNow,
            Status = "Pending"
        };
        _db.SupportQueries.Add(query);
        await _db.SaveChangesAsync();

        var preview = query.QuestionText.Length > 60
            ? query.QuestionText[..60] + "…"
            : query.QuestionText;

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
        {
            await _hub.Clients.Group($"user_{admin.Id}").SendAsync("ReceiveNotification", new
            {
                message = $"New support question from {user?.FullName ?? "a user"}: \"{preview}\"",
                type = "info",
                sentAt = query.AskedAt
            });
            await _hub.Clients.Group($"user_{admin.Id}").SendAsync("ReceiveNewSupportQuery");
        }

        return Ok(new { id = query.Id, status = "Pending" });
    }

    [HttpPost("answer/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Answer(int id, [FromBody] AnswerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Answer))
            return BadRequest("Answer is required.");

        var query = await _db.SupportQueries.FindAsync(id);
        if (query == null) return NotFound();

        var adminId = _userManager.GetUserId(User)!;
        query.Answer = request.Answer.Trim();
        query.Status = "Answered";
        query.AnsweredAt = DateTime.UtcNow;
        query.AnsweredByAdminId = adminId;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"user_{query.UserId}").SendAsync("ReceiveSupportReply", new
        {
            queryId = query.Id,
            answer = query.Answer,
            answeredAt = query.AnsweredAt
        });

        var admins = await _userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
            await _hub.Clients.Group($"user_{admin.Id}").SendAsync("SupportQueryAnswered");

        return Ok(new { success = true });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var query = await _db.SupportQueries.FindAsync(id);
        if (query == null) return NotFound();
        _db.SupportQueries.Remove(query);
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    public record AskRequest(string Question);
    public record AnswerRequest(string Answer);
}
