using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEventManagement.Models;

public class Notification
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info"; // approval, cancellation, reminder, update, info
    public int? EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}
