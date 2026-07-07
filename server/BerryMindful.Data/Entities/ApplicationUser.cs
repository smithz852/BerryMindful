using Microsoft.AspNetCore.Identity;

namespace BerryMindful.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? NotificationEmail { get; set; }
    public bool NotificationsEnabled { get; set; } = true;

    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
    public ICollection<PantryItem> PantryItems { get; set; } = new List<PantryItem>();
}
