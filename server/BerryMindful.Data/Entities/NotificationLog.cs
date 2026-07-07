namespace BerryMindful.Data.Entities;

public enum NotificationType
{
    Warning,
    Expired
}

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public Guid PantryItemId { get; set; }
    public PantryItem PantryItem { get; set; } = null!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public NotificationType Type { get; set; }
}
