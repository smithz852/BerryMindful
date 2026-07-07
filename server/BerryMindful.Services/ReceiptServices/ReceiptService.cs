using BerryMindful.Data;
using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BerryMindful.Services.ReceiptServices;

public class ReceiptService(AppDbContext db)
{
    public async Task<List<PantryItemDto>> ConfirmAsync(string userId, ConfirmReceiptRequest request)
    {
        var receipt = new Receipt
        {
            UserId = userId,
            StoreNameRaw = request.StoreNameRaw,
            PurchasedAt = request.PurchasedAt,
            ImageUrl = request.ImageUrl,
            RawOcrText = request.RawOcrText,
        };

        foreach (var draft in request.Items)
        {
            receipt.Items.Add(new PantryItem
            {
                UserId = userId,
                Name = draft.Name,
                Category = draft.Category,
                PurchasedAt = request.PurchasedAt,
                EstimatedExpiryDays = draft.EstimatedExpiryDays,
                ExpiresAt = request.PurchasedAt.AddDays(draft.EstimatedExpiryDays),
            });
        }

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        return receipt.Items.Select(ToDto).ToList();
    }

    public async Task<List<ReceiptSummaryDto>> ListAsync(string userId)
    {
        return await db.Receipts
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.PurchasedAt)
            .Select(r => new ReceiptSummaryDto(r.Id, r.StoreNameRaw, r.PurchasedAt, r.CreatedAt, r.Items.Count))
            .ToListAsync();
    }

    internal static PantryItemDto ToDto(PantryItem item) => new(
        item.Id, item.Name, item.Category, item.PurchasedAt,
        item.EstimatedExpiryDays, item.ExpiresAt, item.Status, item.ReceiptId);
}
