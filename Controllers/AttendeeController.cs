using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;
using SmartEventManagement.Models;
using SmartEventManagement.Services;
using SmartEventManagement.ViewModels;

namespace SmartEventManagement.Controllers;

[Authorize(Roles = "Attendee")]
public class AttendeeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notificationService;
    private readonly RecommendationService _recommendationService;

    public AttendeeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        NotificationService notificationService, RecommendationService recommendationService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _recommendationService = recommendationService;
    }

    private async Task<string> GetUserIdAsync() =>
        (await _userManager.GetUserAsync(User))!.Id;

    public async Task<IActionResult> Dashboard()
    {
        var userId = await GetUserIdAsync();
        var user = await _userManager.GetUserAsync(User);

        var myRegistrations = await _db.Registrations
            .Where(r => r.AttendeeId == userId && !r.Cancelled)
            .Include(r => r.Event)
            .ToListAsync();

        var attended = myRegistrations.Count(r => r.Event!.Date < DateTime.Today && r.Event.Status == "Approved");

        var attendedEventIds = myRegistrations
            .Where(r => r.Event!.Date < DateTime.Today)
            .Select(r => r.EventId).ToList();

        var hasFeedback = await _db.Feedbacks
            .Where(f => f.AttendeeId == userId)
            .Select(f => f.EventId)
            .ToListAsync();

        var pendingFeedback = attendedEventIds.Except(hasFeedback).Count();

        var recommended = await _recommendationService.GetRecommendationsAsync(userId);

        var vm = new AttendeeDashboardViewModel
        {
            AttendeeName = user!.FullName,
            RegisteredEvents = myRegistrations.Count,
            AttendedEvents = attended,
            PendingFeedback = pendingFeedback,
            RecommendedEvents = recommended,
            MyRegistrations = myRegistrations.OrderByDescending(r => r.RegisteredAt).Take(5).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> BrowseEvents(string? search, string? category, string? dateFilter)
    {
        var query = _db.Events
            .Where(e => e.Status == "Approved")
            .Include(e => e.Organiser)
            .Include(e => e.Registrations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.Title.Contains(search) || e.Description.Contains(search) || e.Location.Contains(search));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (dateFilter == "today")
            query = query.Where(e => e.Date.Date == DateTime.Today);
        else if (dateFilter == "week")
            query = query.Where(e => e.Date.Date >= DateTime.Today && e.Date.Date <= DateTime.Today.AddDays(7));
        else if (dateFilter == "month")
            query = query.Where(e => e.Date.Date >= DateTime.Today && e.Date.Date <= DateTime.Today.AddDays(30));
        else
            query = query.Where(e => e.Date.Date >= DateTime.Today);

        var events = await query.OrderBy(e => e.Date).ToListAsync();

        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.DateFilter = dateFilter;
        ViewBag.Categories = await _db.Events.Select(e => e.Category).Distinct().ToListAsync();

        var userId = await GetUserIdAsync();
        ViewBag.RegisteredEventIds = await _db.Registrations
            .Where(r => r.AttendeeId == userId && !r.Cancelled)
            .Select(r => r.EventId)
            .ToListAsync();

        return View(events);
    }

    public async Task<IActionResult> EventDetails(int id)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Registrations)
            .Include(e => e.Feedbacks).ThenInclude(f => f.Attendee)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev == null) return NotFound();

        var registeredCount = ev.Registrations.Count(r => !r.Cancelled);
        var isRegistered = ev.Registrations.Any(r => r.AttendeeId == userId && !r.Cancelled);
        var hasCapacity = registeredCount < ev.Capacity;
        var avgRating = ev.Feedbacks.Any() ? Math.Round(ev.Feedbacks.Average(f => f.Rating), 1) : 0;

        var vm = new EventDetailsViewModel
        {
            Event = ev,
            RegisteredCount = registeredCount,
            IsRegistered = isRegistered,
            CanRegister = !isRegistered && hasCapacity && ev.Date >= DateTime.Today,
            Feedbacks = ev.Feedbacks.OrderByDescending(f => f.SubmittedAt).ToList(),
            AvgRating = avgRating
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(int eventId)
    {
        var userId = await GetUserIdAsync();
        var ev = await _db.Events.Include(e => e.Registrations).FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev == null) return NotFound();

        var alreadyRegistered = ev.Registrations.Any(r => r.AttendeeId == userId && !r.Cancelled);
        if (alreadyRegistered)
        {
            TempData["ErrorMessage"] = "You are already registered for this event.";
            return RedirectToAction(nameof(EventDetails), new { id = eventId });
        }

        var activeCount = ev.Registrations.Count(r => !r.Cancelled);
        if (activeCount >= ev.Capacity)
        {
            TempData["ErrorMessage"] = "This event is fully booked.";
            return RedirectToAction(nameof(EventDetails), new { id = eventId });
        }

        _db.Registrations.Add(new Registration
        {
            EventId = eventId,
            AttendeeId = userId,
            RegisteredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _notificationService.SendAsync(userId,
            $"You have successfully registered for \"{ev.Title}\"!", "approval", eventId);

        TempData["SuccessMessage"] = $"Successfully registered for \"{ev.Title}\"!";
        return RedirectToAction(nameof(MyRegistrations));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRegistration(int registrationId)
    {
        var userId = await GetUserIdAsync();
        var reg = await _db.Registrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.AttendeeId == userId);

        if (reg == null) return NotFound();

        if (reg.Event!.Date <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Cannot cancel registration for past events.";
            return RedirectToAction(nameof(MyRegistrations));
        }

        reg.Cancelled = true;
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Registration for \"{reg.Event.Title}\" cancelled.";
        return RedirectToAction(nameof(MyRegistrations));
    }

    public async Task<IActionResult> MyRegistrations()
    {
        var userId = await GetUserIdAsync();
        var registrations = await _db.Registrations
            .Where(r => r.AttendeeId == userId)
            .Include(r => r.Event).ThenInclude(e => e!.Organiser)
            .OrderByDescending(r => r.RegisteredAt)
            .ToListAsync();

        var feedbackGiven = await _db.Feedbacks
            .Where(f => f.AttendeeId == userId)
            .Select(f => f.EventId)
            .ToListAsync();

        ViewBag.FeedbackGiven = feedbackGiven;
        return View(registrations);
    }

    [HttpGet]
    public async Task<IActionResult> SubmitFeedback(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null) return NotFound();

        return View(new FeedbackViewModel { EventId = eventId, EventTitle = ev.Title });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(FeedbackViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = await GetUserIdAsync();
        var existing = await _db.Feedbacks.FirstOrDefaultAsync(f => f.EventId == model.EventId && f.AttendeeId == userId);
        if (existing != null)
        {
            TempData["ErrorMessage"] = "You have already submitted feedback for this event.";
            return RedirectToAction(nameof(MyRegistrations));
        }

        _db.Feedbacks.Add(new Feedback
        {
            EventId = model.EventId,
            AttendeeId = userId,
            Rating = model.Rating,
            Comment = model.Comment,
            SubmittedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = "Thank you for your feedback!";
        return RedirectToAction(nameof(MyRegistrations));
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

    public IActionResult CalendarView()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CalendarEvents()
    {
        var userId = await GetUserIdAsync();
        var registrations = await _db.Registrations
            .Where(r => r.AttendeeId == userId && !r.Cancelled)
            .Include(r => r.Event)
            .ToListAsync();

        var events = registrations
            .Where(r => r.Event != null)
            .Select(r => new
            {
                id = r.EventId,
                title = r.Event!.Title,
                start = r.Event.Date.ToString("yyyy-MM-dd") + "T" + r.Event.Time.ToString(@"hh\:mm"),
                url = Url.Action("EventDetails", "Attendee", new { id = r.EventId }),
                backgroundColor = r.Event.Status == "Approved" ? "#6C63FF" : "#FF6584",
                borderColor = r.Event.Status == "Approved" ? "#6C63FF" : "#FF6584",
                extendedProps = new { location = r.Event.Location, category = r.Event.Category }
            });

        return Json(events);
    }

    public async Task<IActionResult> Recommendations()
    {
        var userId = await GetUserIdAsync();
        var recommendations = await _recommendationService.GetRecommendationsAsync(userId, 12);

        var registeredIds = await _db.Registrations
            .Where(r => r.AttendeeId == userId && !r.Cancelled)
            .Select(r => r.EventId)
            .ToListAsync();

        ViewBag.RegisteredEventIds = registeredIds;
        return View(recommendations);
    }
}
