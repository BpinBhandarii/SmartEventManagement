using Microsoft.AspNetCore.Identity;

namespace SmartEventManagement.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public string Interests { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
