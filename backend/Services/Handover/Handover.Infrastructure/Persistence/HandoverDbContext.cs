using Handover.Application.Abstractions;
using Handover.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Handover.Infrastructure.Persistence;

public sealed class HandoverDbContext(DbContextOptions<HandoverDbContext> options) : DbContext(options), IHandoverDbContext
{
    public DbSet<HandoverRecord> HandoverRecords => Set<HandoverRecord>();
    public DbSet<HandoverHorse> HandoverHorses => Set<HandoverHorse>();
    public DbSet<TripCostItem> TripCostItems => Set<TripCostItem>();
    public DbSet<TripRevenueItem> TripRevenueItems => Set<TripRevenueItem>();
    public DbSet<HandoverOutboxMessage> OutboxMessages => Set<HandoverOutboxMessage>();
    public DbSet<HandoverAuditLog> AuditLogs => Set<HandoverAuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<HandoverRecord>(e =>
        {
            e.ToTable("HandoverRecords");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.HandoverNo).IsUnique();
            e.HasIndex(x => x.TripId).IsUnique();
            e.HasIndex(x => x.Status);
            e.Property(x => x.HandoverNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.ReceiverName).HasMaxLength(200);
            e.Property(x => x.ReceiverPhone).HasMaxLength(50);
            e.Property(x => x.ReceiverEmail).HasMaxLength(255);
            e.Property(x => x.Notes).HasMaxLength(3000);
            e.Property(x => x.SignatureUrl).HasMaxLength(1000);
            e.Property(x => x.VersionNo).IsConcurrencyToken().HasDefaultValue(1);
            e.HasMany(x => x.Horses).WithOne(x => x.Handover)
                .HasForeignKey(x => x.HandoverId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<HandoverHorse>(e =>
        {
            e.ToTable("HandoverHorses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.HandoverId, x.HorseId }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.Condition).HasMaxLength(2000);
            e.Property(x => x.IssueNote).HasMaxLength(2000);
            e.Property(x => x.EvidenceImageUrl).HasMaxLength(1000);
        });

        b.Entity<TripCostItem>(e =>
        {
            e.ToTable("TripCostItems");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TripId);
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
        });

        b.Entity<TripRevenueItem>(e =>
        {
            e.ToTable("TripRevenueItems");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TripId);
            e.Property(x => x.Category).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CurrencyCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
        });

        b.Entity<HandoverOutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(150).IsRequired();
            e.Property(x => x.AggregateType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("PENDING");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<HandoverAuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.OldState).HasMaxLength(50);
            e.Property(x => x.NewState).HasMaxLength(50);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.IsRelational()
            ? await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
}
