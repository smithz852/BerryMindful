namespace BerryMindful.Data.Entities;

public enum ItemCategory
{
    Produce,
    Dairy,
    Meat,
    Seafood,
    Bakery,
    Frozen,
    Pantry,
    Beverage,
    Other
}

public enum PantryItemStatus
{
    Active,
    Used,
    Tossed
}

public class PantryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public Guid? ReceiptId { get; set; }
    public Receipt? Receipt { get; set; }

    public string Name { get; set; } = null!;
    public ItemCategory Category { get; set; } = ItemCategory.Other;
    public DateTime PurchasedAt { get; set; }
    public int EstimatedExpiryDays { get; set; }
    public DateTime ExpiresAt { get; set; }
    public PantryItemStatus Status { get; set; } = PantryItemStatus.Active;

    /// <summary>When the item left Active (marked Used/Tossed); null while Active.</summary>
    public DateTime? StatusChangedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}
