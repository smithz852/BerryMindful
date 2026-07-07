using BerryMindful.Data;
using BerryMindful.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BerryMindful.Services.AdminServices;

public class AdminService(AppDbContext db)
{
    public async Task<List<AdminUserDto>> GetUsersAsync()
    {
        var adminRoleId = await db.Roles
            .Where(r => r.Name == Roles.Admin)
            .Select(r => r.Id)
            .SingleAsync();

        return await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                u.CreatedAt,
                db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == adminRoleId),
                u.PantryItems.Count,
                u.Receipts.Count))
            .ToListAsync();
    }

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var weekStart = StartOfWeek(DateTime.UtcNow.Date);
        return new AdminStatsDto(
            await db.Users.CountAsync(),
            await db.Users.CountAsync(u => u.CreatedAt >= weekStart),
            await db.Receipts.CountAsync(),
            await db.PantryItems.CountAsync());
    }

    /// <param name="weeks">Number of trailing weeks to report, clamped to 1–52.</param>
    public async Task<List<WeeklySignupsDto>> GetSignupsAsync(int weeks)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var lastWeek = StartOfWeek(DateTime.UtcNow.Date);
        var firstWeek = lastWeek.AddDays(-7 * (weeks - 1));

        var signups = await db.Users
            .Where(u => u.CreatedAt >= firstWeek)
            .Select(u => u.CreatedAt)
            .ToListAsync();

        // Zero-fill so the time axis is continuous; SpecifyKind because DB-sourced
        // dates come back Unspecified and every bucket should serialize as UTC.
        var byWeek = signups
            .GroupBy(d => StartOfWeek(d))
            .ToDictionary(g => g.Key, g => g.Count());
        var weekly = new List<WeeklySignupsDto>();
        for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
        {
            weekly.Add(new WeeklySignupsDto(
                DateTime.SpecifyKind(week, DateTimeKind.Utc),
                byWeek.GetValueOrDefault(week)));
        }

        return weekly;
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var d = date.Date;
        return d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
    }
}
