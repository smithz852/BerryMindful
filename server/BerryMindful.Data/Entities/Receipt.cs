namespace BerryMindful.Data.Entities;

public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string? StoreNameRaw { get; set; }
    public DateTime PurchasedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? RawOcrText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PantryItem> Items { get; set; } = new List<PantryItem>();
}
