using Microsoft.EntityFrameworkCore;
using SmartEventManagement.Data;

namespace SmartEventManagement.Services;

public class AnalyticsService
{
    private readonly ApplicationDbContext _db;

    public AnalyticsService(ApplicationDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> GetRegistrationsByMonthAsync(string? organiserId = null)
    {
        var query = _db.Registrations.AsQueryable();
        if (organiserId != null)
            query = query.Where(r => r.Event!.OrganiserId == organiserId);

        var data = await query
            .GroupBy(r => new { r.RegisteredAt.Year, r.RegisteredAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        return data.ToDictionary(
            x => $"{x.Year}-{x.Month:D2}",
            x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetEventsByCategoryAsync(string? organiserId = null)
    {
        var query = _db.Events.AsQueryable();
        if (organiserId != null)
            query = query.Where(e => e.OrganiserId == organiserId);

        var data = await query
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        return data.ToDictionary(x => x.Category, x => x.Count);
    }

    public async Task<double> GetAvgFeedbackRatingAsync(string? organiserId = null)
    {
        var query = _db.Feedbacks.AsQueryable();
        if (organiserId != null)
            query = query.Where(f => f.Event!.OrganiserId == organiserId);

        if (!await query.AnyAsync()) return 0;
        return Math.Round(await query.AverageAsync(f => f.Rating), 1);
    }
}
