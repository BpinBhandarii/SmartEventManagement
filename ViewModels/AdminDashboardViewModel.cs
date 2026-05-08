using SmartEventManagement.Models;

namespace SmartEventManagement.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalEvents { get; set; }
    public int PendingApprovals { get; set; }
    public int TotalRegistrations { get; set; }
    public List<ApplicationUser> RecentUsers { get; set; } = new();
    public List<Event> RecentEvents { get; set; } = new();
}
