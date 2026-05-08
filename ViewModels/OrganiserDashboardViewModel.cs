using SmartEventManagement.Models;

namespace SmartEventManagement.ViewModels;

public class OrganiserDashboardViewModel
{
    public string OrganiserName { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public int PendingEvents { get; set; }
    public int TotalRegistrations { get; set; }
    public double AvgFeedbackRating { get; set; }
    public List<Event> Events { get; set; } = new();
}
