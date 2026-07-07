using BerryMindful.Data.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BerryMindful.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<PantryItem> PantryItems => Set<PantryItem>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Receipt>(e =>
        {
            e.HasOne(r => r.User)
                .WithMany(u => u.Receipts)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.StoreNameRaw).HasMaxLength(256);
            e.Property(r => r.ImageUrl).HasMaxLength(1024);
        });

        builder.Entity<PantryItem>(e =>
        {
            e.HasOne(p => p.User)
                .WithMany(u => u.PantryItems)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // Deleting a receipt keeps its confirmed pantry items
            e.HasOne(p => p.Receipt)
                .WithMany(r => r.Items)
                .HasForeignKey(p => p.ReceiptId)
                .OnDelete(DeleteBehavior.SetNull);
            e.Property(p => p.Name).HasMaxLength(256);
            e.Property(p => p.Category).HasConversion<string>().HasMaxLength(32);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(p => new { p.UserId, p.Status, p.ExpiresAt });
        });

        builder.Entity<NotificationLog>(e =>
        {
            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.PantryItem)
                .WithMany(p => p.NotificationLogs)
                .HasForeignKey(n => n.PantryItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(n => n.Type).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(n => new { n.PantryItemId, n.Type });
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(t => t.TokenHash).HasMaxLength(88);
            e.HasIndex(t => t.TokenHash).IsUnique();
        });
    }
}
