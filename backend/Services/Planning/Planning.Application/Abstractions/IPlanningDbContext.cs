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
    DbSet<RouteTransportCapability> RouteTransportCapabilities { get; }
    DbSet<RouteTransportCapabilityMode> RouteTransportCapabilityModes { get; }
    DbSet<TransportTrip> TransportTrips { get; }
    DbSet<TripOrder> TripOrders { get; }
    DbSet<RoutePlan> RoutePlans { get; }
    DbSet<RoutePlanVersion> RoutePlanVersions { get; }
    DbSet<RouteLeg> RouteLegs { get; }
    DbSet<RouteTemplate> RouteTemplates { get; }
    DbSet<Checkpoint> Checkpoints { get; }
    DbSet<TripLegAssignment> TripLegAssignments { get; }
    DbSet<StaffAssignment> StaffAssignments { get; }
    DbSet<StaffRouteCountryAuthorization> StaffRouteCountryAuthorizations { get; }
    DbSet<StaffOperatingArea> StaffOperatingAreas { get; }
    DbSet<HorseLegAssignment> HorseLegAssignments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<TripIncidentFlag> TripIncidentFlags { get; }

    Task<TransportTrip?> GetTripForUpdateAsync(Guid tripId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
