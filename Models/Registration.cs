using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEventManagement.Models;

public class Registration
{
    public int Id { get; set; }
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    public string AttendeeId { get; set; } = string.Empty;

    [ForeignKey(nameof(AttendeeId))]
    public ApplicationUser? Attendee { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public bool Cancelled { get; set; } = false;
}
