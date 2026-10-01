using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planning.Application.Abstractions;
using Planning.Domain;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for PlanningDb adhering to scripts/init-sql/04_planning_db.sql.
/// </summary>
public sealed class PlanningDbContext(DbContextOptions<PlanningDbContext> options) : DbContext(options), IPlanningDbContext
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Stall> Stalls => Set<Stall>();
    public DbSet<Airline> Airlines => Set<Airline>();
    public DbSet<FlightBooking> FlightBookings => Set<FlightBooking>();
    public DbSet<TransportTrip> TransportTrips => Set<TransportTrip>();
    public DbSet<RoutePlan> RoutePlans => Set<RoutePlan>();
    public DbSet<RoutePlanVersion> RoutePlanVersions => Set<RoutePlanVersion>();
    public DbSet<RouteLeg> RouteLegs => Set<RouteLeg>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<TripLegAssignment> TripLegAssignments => Set<TripLegAssignment>();
    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<HorseLegAssignment> HorseLegAssignments => Set<HorseLegAssignment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Country>(e =>
        {
            e.ToTable("Countries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IsoCode).IsUnique();
            e.Property(x => x.IsoCode).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(150);
        });

        b.Entity<Location>(e =>
        {
            e.ToTable("Locations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LocationCode).IsUnique();
            e.Property(x => x.LocationCode).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.LocationType).HasMaxLength(40);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.City).HasMaxLength(150);
            e.Property(x => x.Latitude).HasPrecision(10, 7);
            e.Property(x => x.Longitude).HasPrecision(10, 7);
            e.HasIndex(x => x.CountryId);
            e.HasIndex(x => x.LocationType);
        });

        b.Entity<Vehicle>(e =>
        {
            e.ToTable("Vehicles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VehicleCode).IsUnique();
            e.HasIndex(x => x.PlateNumber).IsUnique();
            e.Property(x => x.VehicleCode).HasMaxLength(50);
            e.Property(x => x.PlateNumber).HasMaxLength(50);
            e.Property(x => x.VehicleType).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(40);
            e.HasIndex(x => x.Status);
        });

        b.Entity<Stall>(e =>
        {
            e.ToTable("Stalls");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StallCode).IsUnique();
            e.Property(x => x.StallCode).HasMaxLength(50);
            e.Property(x => x.StallType).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        b.Entity<Airline>(e =>
        {
            e.ToTable("Airlines");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AirlineCode).IsUnique();
            e.Property(x => x.AirlineCode).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
        });

        b.Entity<FlightBooking>(e =>
        {
            e.ToTable("FlightBookings");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.BookingReference).IsUnique();
            e.Property(x => x.BookingReference).HasMaxLength(100);
            e.Property(x => x.FlightNumber).HasMaxLength(50);
        });

        b.Entity<TransportTrip>(e =>
        {
            e.ToTable("TransportTrips");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasIndex(x => x.TripNo).IsUnique();
            e.Property(x => x.TripNo).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.ComplianceReady).HasDefaultValue(false);
            e.Property(x => x.CurrentLatitude).HasPrecision(10, 7);
            e.Property(x => x.CurrentLongitude).HasPrecision(10, 7);
            e.HasIndex(x => x.Status);
        });

        b.Entity<RoutePlan>(e =>
        {
            e.ToTable("RoutePlans");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TripId).IsUnique();
        });

        b.Entity<RoutePlanVersion>(e =>
        {
            e.ToTable("RoutePlanVersions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RoutePlanId, x.VersionNo }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.TotalDistanceKm).HasPrecision(12, 2);
            e.Property(x => x.AdditionalCost).HasPrecision(18, 2);
            e.Property(x => x.CurrencyCode).HasMaxLength(10);
            e.Property(x => x.Reason).HasMaxLength(2000);
        });

        b.Entity<RouteLeg>(e =>
        {
            e.ToTable("RouteLegs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RoutePlanVersionId, x.SequenceNo }).IsUnique();
            e.Property(x => x.TransportMode).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.DistanceKm).HasPrecision(12, 2);
        });

        b.Entity<Checkpoint>(e =>
        {
            e.ToTable("Checkpoints");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RouteLegId, x.SequenceNo }).IsUnique();
            e.Property(x => x.Type).HasMaxLength(40);
            e.Property(x => x.Status).HasMaxLength(40);
        });

        b.Entity<TripLegAssignment>(e =>
        {
            e.ToTable("TripLegAssignments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.VehicleId);
        });

        b.Entity<StaffAssignment>(e =>
        {
            e.ToTable("StaffAssignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(40);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<HorseLegAssignment>(e =>
        {
            e.ToTable("HorseLegAssignments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RouteLegId, x.HorseId }).IsUnique();
            e.HasIndex(x => x.HorseId);
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(150);
            e.Property(x => x.AggregateType).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(100);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.OldState).HasMaxLength(50);
            e.Property(x => x.NewState).HasMaxLength(50);
            e.Property(x => x.Reason).HasMaxLength(2000);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });
    }

    public async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.IsRelational())
        {
            return await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        }
        return null;
    }
}
