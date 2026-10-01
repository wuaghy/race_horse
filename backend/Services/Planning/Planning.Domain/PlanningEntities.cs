namespace Planning.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public static class PlanningConstants
{
    public static class TripStatus
    {
        public const string Planned = "PLANNED";
        public const string Ready = "READY";
        public const string Departed = "DEPARTED";
        public const string InTransit = "IN_TRANSIT";
        public const string Delayed = "DELAYED";
        public const string Arrived = "ARRIVED";
        public const string HandedOver = "HANDED_OVER";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

    public static class RouteVersionStatus
    {
        public const string Draft = "DRAFT";
        public const string PendingApproval = "PENDING_APPROVAL";
        public const string Rejected = "REJECTED";
        public const string Approved = "APPROVED";
        public const string Active = "ACTIVE";
        public const string Archived = "ARCHIVED";
    }

    public static class TransportMode
    {
        public const string Road = "ROAD";
        public const string Air = "AIR";
    }

    public static class StaffRole
    {
        public const string Driver = "DRIVER";
        public const string Escort = "ESCORT";
    }

    public static class VehicleStatus
    {
        public const string Available = "AVAILABLE";
        public const string InUse = "IN_USE";
        public const string Maintenance = "MAINTENANCE";
        public const string Retired = "RETIRED";
    }

    public static class LocationType
    {
        public const string Stable = "STABLE";
        public const string Airport = "AIRPORT";
        public const string BorderCrossing = "BORDER_CROSSING";
        public const string QuarantineStation = "QUARANTINE_STATION";
        public const string RestStop = "REST_STOP";
        public const string FuelStop = "FUEL_STOP";
        public const string CustomerSite = "CUSTOMER_SITE";
        public const string HandoverPoint = "HANDOVER_POINT";
    }
}

public sealed class Country : Entity
{
    public string IsoCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool Active { get; set; } = true;
}

public sealed class Location : Entity
{
    public string? LocationCode { get; set; }
    public string Name { get; set; } = null!;
    public string LocationType { get; set; } = null!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public Guid CountryId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class Vehicle : Entity
{
    public string VehicleCode { get; set; } = null!;
    public string PlateNumber { get; set; } = null!;
    public string VehicleType { get; set; } = null!;
    public int HorseCapacity { get; set; }
    public int StallCapacity { get; set; }
    public bool TemperatureControlled { get; set; }
    public string Status { get; set; } = PlanningConstants.VehicleStatus.Available;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
}

public sealed class Stall : Entity
{
    public string StallCode { get; set; } = null!;
    public string? StallType { get; set; }
    public int Capacity { get; set; }
    public bool Active { get; set; } = true;
    public string? Notes { get; set; }
}

public sealed class Airline : Entity
{
    public string AirlineCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool Active { get; set; } = true;
}

public sealed class FlightBooking : Entity
{
    public string BookingReference { get; set; } = null!;
    public Guid AirlineId { get; set; }
    public string FlightNumber { get; set; } = null!;
    public Guid OriginAirportId { get; set; }
    public Guid DestinationAirportId { get; set; }
    public DateTime ScheduledDepartureAt { get; set; }
    public DateTime ScheduledArrivalAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
}

public sealed class TransportTrip : Entity
{
    public string TripNo { get; set; } = null!;
    public Guid OrderId { get; set; }
    public string Status { get; set; } = PlanningConstants.TripStatus.Planned;
    public DateTime? PlannedDepartureAt { get; set; }
    public DateTime? PlannedArrivalAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
    public Guid? CurrentLocationId { get; set; }
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTime? CurrentEta { get; set; }
    public int DelayMinutes { get; set; }
    public bool? OnTime { get; set; }
    public bool ComplianceReady { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
}

public sealed class RoutePlan : Entity
{
    public Guid TripId { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
}

public sealed class RoutePlanVersion : Entity
{
    public Guid RoutePlanId { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = PlanningConstants.RouteVersionStatus.Draft;
    public string? Reason { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public DateTime? PlannedDepartureAt { get; set; }
    public DateTime? PlannedArrivalAt { get; set; }
    public decimal AdditionalCost { get; set; }
    public string? CurrencyCode { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class RouteLeg : Entity
{
    public Guid RoutePlanVersionId { get; set; }
    public int SequenceNo { get; set; }
    public Guid OriginLocationId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public string TransportMode { get; set; } = PlanningConstants.TransportMode.Road;
    public DateTime? PlannedDepartureAt { get; set; }
    public DateTime? PlannedArrivalAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
    public decimal? DistanceKm { get; set; }
    public string Status { get; set; } = "PLANNED";
}

public sealed class Checkpoint : Entity
{
    public Guid RouteLegId { get; set; }
    public int SequenceNo { get; set; }
    public Guid LocationId { get; set; }
    public string Type { get; set; } = null!;
    public DateTime? PlannedArrivalAt { get; set; }
    public DateTime? PlannedDepartureAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public string Status { get; set; } = "PLANNED";
}

public sealed class TripLegAssignment : Entity
{
    public Guid RouteLegId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? FlightBookingId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StaffAssignment : Entity
{
    public Guid TripId { get; set; }
    public Guid? RouteLegId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
}

public sealed class HorseLegAssignment : Entity
{
    public Guid TripId { get; set; }
    public Guid HorseId { get; set; }
    public Guid RouteLegId { get; set; }
    public Guid? StallId { get; set; }
    public DateTime? BoardedAt { get; set; }
    public DateTime? UnboardedAt { get; set; }
}

public sealed class OutboxMessage : Entity
{
    public string EventType { get; set; } = null!;
    public string AggregateType { get; set; } = null!;
    public Guid AggregateId { get; set; }
    public string Payload { get; set; } = null!;
    public Guid CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Status { get; set; } = "PENDING";
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AuditLog : Entity
{
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = null!;
    public string? OldState { get; set; }
    public string? NewState { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
