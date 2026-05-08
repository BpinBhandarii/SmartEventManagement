using System.ComponentModel.DataAnnotations;

namespace SmartEventManagement.ViewModels;

public class EventViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Location { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; } = DateTime.Today.AddDays(7);

    [Required]
    public TimeSpan Time { get; set; } = new TimeSpan(9, 0, 0);

    [Required, Range(1, 10000)]
    public int Capacity { get; set; } = 50;
}
