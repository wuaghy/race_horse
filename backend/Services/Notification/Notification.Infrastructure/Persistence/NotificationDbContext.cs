using Microsoft.EntityFrameworkCore;
using Notification.Application.Abstractions;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Persistence;

public class NotificationDbContext : DbContext, INotificationDbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<Notification.Domain.Entities.Notification> Notifications => Set<Notification.Domain.Entities.Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification.Domain.Entities.Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.ReferenceType).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ReadAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt).HasPrecision(3);

            entity.HasIndex(e => new { e.RecipientUserId, e.Status });
            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId });
        });

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.ToTable("DeviceTokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Platform).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(1000).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.CreatedAt).HasPrecision(3);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);

            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("InboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(150).IsRequired();
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.Property(e => e.ProcessedAt).HasPrecision(3);
        });
    }
}
