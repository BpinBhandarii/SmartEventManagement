using System.ComponentModel.DataAnnotations;

namespace SmartEventManagement.ViewModels;

public class FeedbackViewModel
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    [Required, Range(1, 5)]
    public int Rating { get; set; }

    [Required, MinLength(10)]
    public string Comment { get; set; } = string.Empty;
}
