using Microsoft.EntityFrameworkCore;
using Tracking.Domain;

namespace Tracking.Infrastructure.Persistence;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options)
    {
    }

    public DbSet<TripExecution> TripExecutions { get; set; }
    public DbSet<CheckpointEvent> CheckpointEvents { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<GpsPoint> GpsPoints { get; set; }
    public DbSet<LocationHistory> LocationHistories { get; set; }
    public DbSet<EstimatedTimeOfArrival> EstimatedTimesOfArrival { get; set; }
    public DbSet<HorseHealthLog> HealthLogs { get; set; }
    public DbSet<HealthAlert> HealthAlerts { get; set; }
    public DbSet<HorseHealthStatus> HealthStatuses { get; set; }

    public new async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TripExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripId).IsRequired();
            entity.Property(e => e.TripNo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripId).IsUnique();
        });

        modelBuilder.Entity<CheckpointEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.LocationId).IsRequired();
            entity.Property(e => e.CheckpointType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AggregateType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<GpsPoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId);
        });

        modelBuilder.Entity<LocationHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId);
        });

        modelBuilder.Entity<EstimatedTimeOfArrival>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId).IsUnique();
        });

        modelBuilder.Entity<HorseHealthLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId);
        });

        modelBuilder.Entity<HealthAlert>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.AlertType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId);
        });

        modelBuilder.Entity<HorseHealthStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TripExecutionId).IsRequired();
            entity.Property(e => e.CurrentStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(e => e.TripExecutionId).IsUnique();
        });
    }
}
