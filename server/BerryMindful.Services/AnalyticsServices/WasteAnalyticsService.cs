using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BerryMindful.Services.AnalyticsServices;

public class WasteAnalyticsService(AppDbContext db)
{
    /// <param name="days">Look-back window in days; 0 means all time.</param>
    public async Task<WasteAnalyticsDto> GetWasteAsync(string userId, int days)
    {
        var query = db.PantryItems
            .Where(p => p.UserId == userId
                && p.Status != PantryItemStatus.Active
                && p.StatusChangedAt != null);

        var rangeStart = days > 0 ? DateTime.UtcNow.Date.AddDays(-days) : (DateTime?)null;
        if (rangeStart is not null)
        {
            query = query.Where(p => p.StatusChangedAt >= rangeStart);
        }

        var rows = await query
            .Select(p => new ResolvedItem(p.Name, p.Category, p.Status, p.StatusChangedAt!.Value, p.ExpiresAt))
            .ToListAsync();

        var used = rows.Count(r => r.Status == PantryItemStatus.Used);
        var tossed = rows.Count(r => r.Status == PantryItemStatus.Tossed);
        var tossedAfterExpiry = rows.Count(r =>
            r.Status == PantryItemStatus.Tossed && r.StatusChangedAt >= r.ExpiresAt);
        var totals = new WasteTotalsDto(
            used,
            tossed,
            used + tossed == 0 ? 0 : tossed / (double)(used + tossed),
            tossedAfterExpiry);

        return new WasteAnalyticsDto(totals, BuildWeekly(rows, rangeStart), BuildByCategory(rows), BuildMostTossed(rows));
    }

    private static List<WeeklyWasteDto> BuildWeekly(List<ResolvedItem> rows, DateTime? rangeStart)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var byWeek = rows
            .GroupBy(r => StartOfWeek(r.StatusChangedAt))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Zero-fill so the time axis is continuous from the range start (or earliest data) through this week.
        // SpecifyKind: DB-sourced dates come back Unspecified; keep every bucket serializing as UTC.
        var firstWeek = DateTime.SpecifyKind(StartOfWeek(rangeStart ?? byWeek.Keys.Min()), DateTimeKind.Utc);
        var lastWeek = StartOfWeek(DateTime.UtcNow.Date);
        var weekly = new List<WeeklyWasteDto>();
        for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
        {
            var bucket = byWeek.GetValueOrDefault(week);
            weekly.Add(new WeeklyWasteDto(
                week,
                bucket?.Count(r => r.Status == PantryItemStatus.Used) ?? 0,
                bucket?.Count(r => r.Status == PantryItemStatus.Tossed) ?? 0));
        }

        return weekly;
    }

    private static List<CategoryWasteDto> BuildByCategory(List<ResolvedItem> rows)
    {
        return rows
            .GroupBy(r => r.Category)
            .Select(g => new CategoryWasteDto(
                g.Key,
                g.Count(r => r.Status == PantryItemStatus.Tossed),
                g.Count(r => r.Status == PantryItemStatus.Used)))
            .OrderByDescending(c => c.Tossed)
            .ThenByDescending(c => c.Used)
            .ToList();
    }

    private static List<TossedItemDto> BuildMostTossed(List<ResolvedItem> rows)
    {
        return rows
            .Where(r => r.Status == PantryItemStatus.Tossed)
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new TossedItemDto(g.First().Name, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name)
            .Take(5)
            .ToList();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var d = date.Date;
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }

    private record ResolvedItem(
        string Name,
        ItemCategory Category,
        PantryItemStatus Status,
        DateTime StatusChangedAt,
        DateTime ExpiresAt);
}
