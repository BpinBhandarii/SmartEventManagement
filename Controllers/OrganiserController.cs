using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;
using SmartEventManagement.Models;
using SmartEventManagement.Services;
using SmartEventManagement.ViewModels;

namespace SmartEventManagement.Controllers;

[Authorize(Roles = "Organiser")]
public class OrganiserController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notificationService;
    private readonly AnalyticsService _analyticsService;

    public OrganiserController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        NotificationService notificationService, AnalyticsService analyticsService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
    }

    private async Task<string> GetUserIdAsync() =>
        (await _userManager.GetUserAsync(User))!.Id;

    public async Task<IActionResult> Dashboard()
    {
        var userId = await GetUserIdAsync();
        var user = await _userManager.GetUserAsync(User);

        var events = await _db.Events
            .Where(e => e.OrganiserId == userId)
            .Include(e => e.Registrations)
            .Include(e => e.Feedbacks)
            .ToListAsync();

        var avgRating = await _analyticsService.GetAvgFeedbackRatingAsync(userId);

        var vm = new OrganiserDashboardViewModel
        {
            OrganiserName = user!.FullName,
            TotalEvents = events.Count,
            PendingEvents = events.Count(e => e.Status == "Pending"),
            TotalRegistrations = events.Sum(e => e.Registrations.Count(r => !r.Cancelled)),
            AvgFeedbackRating = avgRating,
            Events = events.OrderByDescending(e => e.Id).Take(5).ToList()
        };

        ViewBag.RegistrationsByMonth = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetRegistrationsByMonthAsync(userId));

        return View(vm);
    }

    [HttpGet]
    public IActionResult CreateEvent() => View(new EventViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = await GetUserIdAsync();
        var ev = new Event
        {
            Title = model.Title,
            Description = model.Description,
            Location = model.Location,
            Category = model.Category,
            Date = model.Date,
            Time = model.Time,
            Capacity = model.Capacity,
            Status = "Pending",
            OrganiserId = userId
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Event created successfully! Awaiting admin approval.";
        return RedirectToAction(nameof(MyEvents));
    }

    public async Task<IActionResult> MyEvents()
    {
        var userId = await GetUserIdAsync();
        var events = await _db.Events
            .Where(e => e.OrganiserId == userId)
            .Include(e => e.Registrations)
            .OrderByDescending(e => e.Id)
            .ToListAsync();
        return View(events);
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.OrganiserId == userId);
        if (ev == null) return NotFound();

        var vm = new EventViewModel
        {
            Id = ev.Id,
            Title = ev.Title,
            Description = ev.Description,
            Location = ev.Location,
            Category = ev.Category,
            Date = ev.Date,
            Time = ev.Time,
            Capacity = ev.Capacity
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(EventViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = await GetUserIdAsync();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == model.Id && e.OrganiserId == userId);
        if (ev == null) return NotFound();

        ev.Title = model.Title;
        ev.Description = model.Description;
        ev.Location = model.Location;
        ev.Category = model.Category;
        ev.Date = model.Date;
        ev.Time = model.Time;
        ev.Capacity = model.Capacity;
        ev.Status = "Pending"; // Re-submit for approval after edit

        await _db.SaveChangesAsync();

        await _notificationService.SendToAllAttendeesAsync(ev.Id,
            $"Event \"{ev.Title}\" has been updated. Check the details!", "update");

        TempData["SuccessMessage"] = "Event updated and resubmitted for approval.";
        return RedirectToAction(nameof(MyEvents));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEvent(int id)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id && e.OrganiserId == userId);
        if (ev == null) return NotFound();

        ev.Status = "Cancelled";
        await _db.SaveChangesAsync();

        await _notificationService.SendToAllAttendeesAsync(ev.Id,
            $"Event \"{ev.Title}\" has been cancelled by the organiser.", "cancellation");

        TempData["SuccessMessage"] = "Event cancelled and attendees notified.";
        return RedirectToAction(nameof(MyEvents));
    }

    public async Task<IActionResult> EventAttendees(int id)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events
            .Include(e => e.Registrations).ThenInclude(r => r.Attendee)
            .Include(e => e.Feedbacks).ThenInclude(f => f.Attendee)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganiserId == userId);

        if (ev == null) return NotFound();
        return View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyAttendees(int eventId, string message)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganiserId == userId);
        if (ev == null) return NotFound();

        await _notificationService.SendToAllAttendeesAsync(eventId, message, "info");
        TempData["SuccessMessage"] = "Notification sent to all attendees.";
        return RedirectToAction(nameof(EventAttendees), new { id = eventId });
    }

    public async Task<IActionResult> Analytics()
    {
        var userId = await GetUserIdAsync();
        ViewBag.RegistrationsByMonth = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetRegistrationsByMonthAsync(userId));
        ViewBag.EventsByCategory = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetEventsByCategoryAsync(userId));
        ViewBag.AvgRating = await _analyticsService.GetAvgFeedbackRatingAsync(userId);
        ViewBag.TotalEvents = await _db.Events.CountAsync(e => e.OrganiserId == userId);
        ViewBag.TotalRegistrations = await _db.Registrations.CountAsync(r => r.Event!.OrganiserId == userId && !r.Cancelled);
        return View();
    }

    public async Task<IActionResult> Notifications()
    {
        var userId = await GetUserIdAsync();
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .Include(n => n.Event)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();
        return View(notifications);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        var userId = await GetUserIdAsync();
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (n != null && !n.IsRead) { n.IsRead = true; await _db.SaveChangesAsync(); }
        return Json(new { success = true });
    }
}
