using Microsoft.AspNetCore.SignalR;
using SmartEventManagement.Data;
using SmartEventManagement.Hubs;
using SmartEventManagement.Models;

namespace SmartEventManagement.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task SendAsync(string userId, string message, string type = "info", int? eventId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            EventId = eventId,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            message,
            type,
            sentAt = notification.SentAt
        });
    }

    public async Task SendToAllAttendeesAsync(int eventId, string message, string type = "info")
    {
        var registrations = _db.Registrations
            .Where(r => r.EventId == eventId && !r.Cancelled)
            .ToList();

        foreach (var reg in registrations)
            await SendAsync(reg.AttendeeId, message, type, eventId);
    }

    public async Task MarkReadAsync(int notificationId, string userId)
    {
        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification != null && notification.UserId == userId)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }
}
