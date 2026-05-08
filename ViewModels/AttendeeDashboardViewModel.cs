using SmartEventManagement.Models;

namespace SmartEventManagement.ViewModels;

public class AttendeeDashboardViewModel
{
    public string AttendeeName { get; set; } = string.Empty;
    public int RegisteredEvents { get; set; }
    public int AttendedEvents { get; set; }
    public int PendingFeedback { get; set; }
    public List<Event> RecommendedEvents { get; set; } = new();
    public List<Registration> MyRegistrations { get; set; } = new();
}
