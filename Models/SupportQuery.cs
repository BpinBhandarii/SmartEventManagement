using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEventManagement.Models;

public class SupportQuery
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public string QuestionText { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Answered

    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }

    public string? AnsweredByAdminId { get; set; }

    [ForeignKey(nameof(AnsweredByAdminId))]
    public ApplicationUser? AnsweredByAdmin { get; set; }
}
