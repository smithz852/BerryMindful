using BerryMindful.Data.Entities;
using BerryMindful.Services.DTOs;

namespace BerryMindful.Services.ReceiptServices;

public class StubReceiptScanner : IReceiptScanner
{
    public Task<ReceiptScanResultDto> ScanAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        var result = new ReceiptScanResultDto(
            StoreNameRaw: "STUB MART #042",
            PurchasedAt: DateTime.UtcNow.Date,
            ImageUrl: null,
            RawOcrText: "ORG BNNA 3PK 1.49\nWHL MLK GAL 3.49\nGND BF 93/7 LB 5.99\nORG STRBRY PINT 4.99",
            Items:
            [
                new PantryItemDraftDto("Organic Bananas", ItemCategory.Produce, 5),
                new PantryItemDraftDto("Whole Milk", ItemCategory.Dairy, 10),
                new PantryItemDraftDto("Ground Beef 93/7", ItemCategory.Meat, 2),
                new PantryItemDraftDto("Organic Strawberries", ItemCategory.Produce, 4),
            ]);

        return Task.FromResult(result);
    }
}
