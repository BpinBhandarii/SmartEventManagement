using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;
using SmartEventManagement.Models;

namespace SmartEventManagement.Services;

public class RecommendationService
{
    private readonly ApplicationDbContext _db;

    public RecommendationService(ApplicationDbContext db) => _db = db;

    public async Task<List<Event>> GetRecommendationsAsync(string userId, int maxResults = 4)
    {
        var user = await _db.Users.FindAsync(userId);
        var userInterests = (user?.Interests ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(i => i.Trim().ToLower()).ToList();

        var registeredEventIds = await _db.Registrations
            .Where(r => r.AttendeeId == userId && !r.Cancelled)
            .Select(r => r.EventId)
            .ToListAsync();

        var upcomingEvents = await _db.Events
            .Where(e => e.Status == "Approved" && e.Date >= DateTime.Today && !registeredEventIds.Contains(e.Id))
            .Include(e => e.Organiser)
            .ToListAsync();

        if (userInterests.Any())
        {
            var matched = upcomingEvents
                .Where(e => userInterests.Any(i => e.Category.ToLower().Contains(i)))
                .Take(maxResults)
                .ToList();

            if (matched.Count >= maxResults)
                return matched;

            var rest = upcomingEvents
                .Except(matched)
                .Take(maxResults - matched.Count);

            return matched.Concat(rest).ToList();
        }

        return upcomingEvents.Take(maxResults).ToList();
    }
}
