using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planning.Domain;

namespace Planning.Application.Abstractions;

public interface IPlanningDbContext
{
    DbSet<Country> Countries { get; }
    DbSet<Location> Locations { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Stall> Stalls { get; }
    DbSet<Airline> Airlines { get; }
    DbSet<FlightBooking> FlightBookings { get; }
    DbSet<TransportTrip> TransportTrips { get; }
    DbSet<RoutePlan> RoutePlans { get; }
    DbSet<RoutePlanVersion> RoutePlanVersions { get; }
    DbSet<RouteLeg> RouteLegs { get; }
    DbSet<Checkpoint> Checkpoints { get; }
    DbSet<TripLegAssignment> TripLegAssignments { get; }
    DbSet<StaffAssignment> StaffAssignments { get; }
    DbSet<HorseLegAssignment> HorseLegAssignments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
