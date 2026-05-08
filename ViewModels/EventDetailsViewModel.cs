using SmartEventManagement.Models;

namespace SmartEventManagement.ViewModels;

public class EventDetailsViewModel
{
    public Event Event { get; set; } = null!;
    public int RegisteredCount { get; set; }
    public bool IsRegistered { get; set; }
    public bool CanRegister { get; set; }
    public List<Feedback> Feedbacks { get; set; } = new();
    public double AvgRating { get; set; }
}
