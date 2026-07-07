using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;
using BerryMindful.Services.ReceiptServices;
using Microsoft.EntityFrameworkCore;

namespace BerryMindful.Services.PantryServices;

public class PantryService(AppDbContext db)
{
    public async Task<List<PantryItemDto>> GetActiveAsync(string userId)
    {
        return await db.PantryItems
            .Where(p => p.UserId == userId && p.Status == PantryItemStatus.Active)
            .OrderBy(p => p.ExpiresAt)
            .Select(p => ReceiptService.ToDto(p))
            .ToListAsync();
    }

    public async Task<PantryItemDto?> UpdateStatusAsync(string userId, Guid itemId, PantryItemStatus status)
    {
        var item = await db.PantryItems
            .SingleOrDefaultAsync(p => p.Id == itemId && p.UserId == userId);
        if (item is null)
        {
            return null;
        }

        item.Status = status;
        item.StatusChangedAt = status == PantryItemStatus.Active ? null : DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ReceiptService.ToDto(item);
    }

    public async Task<PantryItemDto> AddManualAsync(string userId, AddPantryItemRequest request)
    {
        var purchasedAt = request.PurchasedAt ?? DateTime.UtcNow.Date;
        var item = new PantryItem
        {
            UserId = userId,
            Name = request.Name,
            Category = request.Category,
            PurchasedAt = purchasedAt,
            EstimatedExpiryDays = request.EstimatedExpiryDays,
            ExpiresAt = purchasedAt.AddDays(request.EstimatedExpiryDays),
        };

        db.PantryItems.Add(item);
        await db.SaveChangesAsync();
        return ReceiptService.ToDto(item);
    }

    public async Task<bool> DeleteAsync(string userId, Guid itemId)
    {
        var deleted = await db.PantryItems
            .Where(p => p.Id == itemId && p.UserId == userId)
            .ExecuteDeleteAsync();
        return deleted > 0;
    }
}
