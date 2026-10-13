using Incident.Application.Abstractions;
using Incident.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Incident.Infrastructure.Persistence;

public class IncidentDbContext : DbContext, IIncidentDbContext
{
    public IncidentDbContext(DbContextOptions<IncidentDbContext> options) : base(options)
    {
    }

    public DbSet<Incident.Domain.Entities.Incident> Incidents => Set<Incident.Domain.Entities.Incident>();
    public DbSet<IncidentAttachment> IncidentAttachments => Set<IncidentAttachment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Incident.Domain.Entities.Incident>(entity =>
        {
            entity.ToTable("Incidents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IncidentNo).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.IncidentNo).IsUnique();

            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();

            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);

            entity.Property(e => e.Title).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ResolutionNote).HasMaxLength(4000);

            entity.Property(e => e.OccurredAt).HasPrecision(3);
            entity.Property(e => e.ResolvedAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt).HasPrecision(3);
            entity.Property(e => e.UpdatedAt).HasPrecision(3);
            entity.Property(e => e.VersionNo).HasDefaultValue(1);

            entity.HasIndex(e => e.TripId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Severity);

            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.Incident)
                  .HasForeignKey(a => a.IncidentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IncidentAttachment>(entity =>
        {
            entity.ToTable("IncidentAttachments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileUrl).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.UploadedAt).HasPrecision(3);

            entity.HasIndex(e => e.IncidentId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(150).IsRequired();
            entity.Property(e => e.AggregateType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("PENDING");
            entity.Property(e => e.OccurredAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt).HasPrecision(3);
            entity.Property(e => e.ProcessedAt).HasPrecision(3);

            entity.HasIndex(e => new { e.Status, e.CreatedAt });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OldState).HasMaxLength(50);
            entity.Property(e => e.NewState).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(2000);
            entity.Property(e => e.OccurredAt).HasPrecision(3);
            entity.Property(e => e.CreatedAt).HasPrecision(3);

            entity.HasIndex(e => new { e.EntityType, e.EntityId });
        });
    }
}
