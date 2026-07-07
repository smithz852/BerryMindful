using BerryMindful.Data.Entities;

namespace BerryMindful.Services.DTOs;

public record WasteAnalyticsDto(
    WasteTotalsDto Totals,
    List<WeeklyWasteDto> Weekly,
    List<CategoryWasteDto> ByCategory,
    List<TossedItemDto> MostTossed);

public record WasteTotalsDto(
    int Used,
    int Tossed,
    double WasteRate,
    int TossedAfterExpiry);

public record WeeklyWasteDto(
    DateTime WeekStart, // Monday, UTC date
    int Used,
    int Tossed);

public record CategoryWasteDto(
    ItemCategory Category,
    int Tossed,
    int Used);

public record TossedItemDto(
    string Name,
    int Count);
