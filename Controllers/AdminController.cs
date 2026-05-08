using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;
using SmartEventManagement.Models;
using SmartEventManagement.Services;
using SmartEventManagement.ViewModels;

namespace SmartEventManagement.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notificationService;
    private readonly AnalyticsService _analyticsService;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        NotificationService notificationService, AnalyticsService analyticsService)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            TotalEvents = await _db.Events.CountAsync(),
            PendingApprovals = await _db.Users.CountAsync(u => !u.IsApproved),
            TotalRegistrations = await _db.Registrations.CountAsync(r => !r.Cancelled),
            RecentUsers = await _db.Users.OrderByDescending(u => u.RegisteredAt).Take(5).ToListAsync(),
            RecentEvents = await _db.Events.Include(e => e.Organiser).OrderByDescending(e => e.Id).Take(5).ToListAsync()
        };

        ViewBag.RegistrationsByMonth = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetRegistrationsByMonthAsync());
        ViewBag.EventsByCategory = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetEventsByCategoryAsync());

        return View(vm);
    }

    public async Task<IActionResult> ManageUsers()
    {
        var allUsers = await _db.Users.OrderByDescending(u => u.RegisteredAt).ToListAsync();
        var pendingUsers = allUsers.Where(u => !u.IsApproved).ToList();
        ViewBag.PendingUsers = pendingUsers;
        return View(allUsers);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.IsApproved = true;
            await _userManager.UpdateAsync(user);
            await _notificationService.SendAsync(userId,
                "Your account has been approved! You can now log in.", "approval");
            TempData["SuccessMessage"] = $"{user.FullName}'s account has been approved.";
        }
        return RedirectToAction(nameof(ManageUsers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            TempData["SuccessMessage"] = $"{user.FullName}'s account has been rejected and removed.";
            await _userManager.DeleteAsync(user);
        }
        return RedirectToAction(nameof(ManageUsers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == userId)
        {
            TempData["ErrorMessage"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(ManageUsers));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            // Remove related data
            var notifications = _db.Notifications.Where(n => n.UserId == userId);
            _db.Notifications.RemoveRange(notifications);

            var registrations = _db.Registrations.Where(r => r.AttendeeId == userId);
            _db.Registrations.RemoveRange(registrations);

            var feedbacks = _db.Feedbacks.Where(f => f.AttendeeId == userId);
            _db.Feedbacks.RemoveRange(feedbacks);

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"{user.FullName} has been deleted.";
            await _userManager.DeleteAsync(user);
        }
        return RedirectToAction(nameof(ManageUsers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserActive(string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == userId)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(ManageUsers));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = user.IsActive
                ? $"{user.FullName} has been activated."
                : $"{user.FullName} has been deactivated.";
        }
        return RedirectToAction(nameof(ManageUsers));
    }

    public async Task<IActionResult> Events()
    {
        var events = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Feedbacks).ThenInclude(f => f.Attendee)
            .OrderByDescending(e => e.Id)
            .ToListAsync();
        return View(events);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev != null)
        {
            ev.Status = "Approved";
            await _db.SaveChangesAsync();
            await _notificationService.SendAsync(ev.OrganiserId,
                $"Your event \"{ev.Title}\" has been approved!", "approval", eventId);
            TempData["SuccessMessage"] = $"Event \"{ev.Title}\" approved.";
        }
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev != null)
        {
            ev.Status = "Rejected";
            await _db.SaveChangesAsync();
            await _notificationService.SendAsync(ev.OrganiserId,
                $"Your event \"{ev.Title}\" was not approved.", "info", eventId);
            TempData["SuccessMessage"] = $"Event \"{ev.Title}\" rejected.";
        }
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkEventPending(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev != null)
        {
            ev.Status = "Pending";
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Event \"{ev.Title}\" moved back to Pending.";
        }
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev != null)
        {
            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Event deleted.";
        }
        return RedirectToAction(nameof(Events));
    }

    public async Task<IActionResult> Notifications()
    {
        var userId = _userManager.GetUserId(User)!;
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
        var userId = _userManager.GetUserId(User)!;
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (n != null && !n.IsRead) { n.IsRead = true; await _db.SaveChangesAsync(); }
        return Json(new { success = true });
    }

    public async Task<IActionResult> FeedbackManagement()
    {
        var feedbacks = await _db.Feedbacks
            .Include(f => f.Attendee)
            .Include(f => f.Event)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync();
        return View(feedbacks);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFeedback(int feedbackId)
    {
        var feedback = await _db.Feedbacks.FindAsync(feedbackId);
        if (feedback != null)
        {
            _db.Feedbacks.Remove(feedback);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Feedback deleted.";
        }
        return RedirectToAction(nameof(Events));
    }

    public async Task<IActionResult> SupportQueries()
    {
        var queries = await _db.SupportQueries
            .Include(q => q.User)
            .Include(q => q.AnsweredByAdmin)
            .OrderByDescending(q => q.AskedAt)
            .ToListAsync();
        return View(queries);
    }

    public async Task<IActionResult> Analytics()
    {
        ViewBag.RegistrationsByMonth = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetRegistrationsByMonthAsync());
        ViewBag.EventsByCategory = System.Text.Json.JsonSerializer.Serialize(
            await _analyticsService.GetEventsByCategoryAsync());
        ViewBag.AvgRating = await _analyticsService.GetAvgFeedbackRatingAsync();
        ViewBag.TotalUsers = await _db.Users.CountAsync();
        ViewBag.TotalEvents = await _db.Events.CountAsync();
        ViewBag.TotalRegistrations = await _db.Registrations.CountAsync(r => !r.Cancelled);
        return View();
    }
}
