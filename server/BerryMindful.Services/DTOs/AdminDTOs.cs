namespace BerryMindful.Services.DTOs;

public record AdminUserDto(
    string Id,
    string Email,
    DateTime CreatedAt,
    bool IsAdmin,
    int PantryItemCount,
    int ReceiptCount);

public record AdminStatsDto(
    int TotalUsers,
    int NewUsersThisWeek,
    int TotalReceipts,
    int TotalPantryItems);

public record WeeklySignupsDto(DateTime WeekStart, int Count);
