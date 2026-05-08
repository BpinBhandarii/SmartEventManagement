using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEventManagement.Models;

public class Event
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Location { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    public TimeSpan Time { get; set; }

    [Range(1, 10000)]
    public int Capacity { get; set; }

    // Pending, Approved, Cancelled, Completed
    public string Status { get; set; } = "Pending";

    [Required]
    public string OrganiserId { get; set; } = string.Empty;

    [ForeignKey(nameof(OrganiserId))]
    public ApplicationUser? Organiser { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
