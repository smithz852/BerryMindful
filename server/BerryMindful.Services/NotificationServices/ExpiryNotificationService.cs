using System.Text;
using BerryMindful.Data;
using BerryMindful.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZlEmailProvider;

namespace BerryMindful.Services.NotificationServices;

// Composes and sends the daily expiry digest: one email per user covering their
// active items that are expired or expiring within the warning window, deduped via
// NotificationLogs so each item triggers at most one Warning and one Expired notice.
public class ExpiryNotificationService(
    AppDbContext db,
    IEmailService email,
    IOptions<NotificationOptions> options,
    ILogger<ExpiryNotificationService> logger)
{
    private const int WarningWindowDays = 2;

    public async Task<int> SendDigestsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.Date.AddDays(WarningWindowDays + 1); // include all of the last warning day

        var candidates = await db.PantryItems
            .Where(i => i.Status == PantryItemStatus.Active
                && i.ExpiresAt < cutoff
                && i.User.NotificationsEnabled)
            .Include(i => i.User)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return 0;
        }

        var candidateIds = candidates.Select(i => i.Id).ToList();
        var alreadyNotified = (await db.NotificationLogs
                .Where(l => candidateIds.Contains(l.PantryItemId))
                .Select(l => new { l.PantryItemId, l.Type })
                .ToListAsync(cancellationToken))
            .Select(l => (l.PantryItemId, l.Type))
            .ToHashSet();

        var pending = candidates
            .Select(item => (Item: item, Type: item.ExpiresAt.Date < now.Date
                ? NotificationType.Expired
                : NotificationType.Warning))
            .Where(x => !alreadyNotified.Contains((x.Item.Id, x.Type)))
            .ToList();

        var emailsSent = 0;
        foreach (var userGroup in pending.GroupBy(x => x.Item.UserId))
        {
            var user = userGroup.First().Item.User;
            var toAddress = user.NotificationEmail ?? user.Email;
            if (string.IsNullOrWhiteSpace(toAddress))
            {
                continue;
            }

            var (subject, body) = ComposeDigest(userGroup.Select(x => x.Item).ToList(), now);
            try
            {
                await email.SendAsync(toAddress, subject, body);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to send expiry digest to user {UserId}", user.Id);
                continue; // no NotificationLog rows — retried on the next run
            }

            db.NotificationLogs.AddRange(userGroup.Select(x => new NotificationLog
            {
                UserId = user.Id,
                PantryItemId = x.Item.Id,
                Type = x.Type,
                SentAt = now,
            }));
            emailsSent++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return emailsSent;
    }

    private (string Subject, string Body) ComposeDigest(List<PantryItem> items, DateTime now)
    {
        var ordered = items.OrderBy(i => i.ExpiresAt).ToList();
        var subject = ordered.Count == 1
            ? $"BerryMindful: \"{ordered[0].Name}\" needs attention"
            : $"BerryMindful: {ordered.Count} pantry items need attention";

        var body = new StringBuilder();
        body.AppendLine("Some items in your pantry are expired or expiring soon:");
        body.AppendLine();
        foreach (var item in ordered)
        {
            body.AppendLine($"- {item.Name} — {DescribeExpiry(item.ExpiresAt, now)}");
        }
        body.AppendLine();
        body.AppendLine($"Open your pantry to mark items Used or Tossed: {options.Value.AppBaseUrl}/pantry");

        return (subject, body.ToString());
    }

    private static string DescribeExpiry(DateTime expiresAt, DateTime now)
    {
        var daysLeft = (expiresAt.Date - now.Date).Days;
        return daysLeft switch
        {
            < -1 => $"expired {-daysLeft} days ago",
            -1 => "expired yesterday",
            0 => "expires today",
            1 => "expires tomorrow",
            _ => $"expires in {daysLeft} days",
        };
    }
}
