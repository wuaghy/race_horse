namespace Tracking.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

public static class TrackingConstants
{
    public static class TripExecutionStatus
    {
        public const string Planned = "PLANNED";
        public const string Departed = "DEPARTED";
        public const string InTransit = "IN_TRANSIT";
        public const string Arrived = "ARRIVED";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

    public static class CheckpointStatus
    {
        public const string Planned = "PLANNED";
        public const string Arrived = "ARRIVED";
        public const string Departed = "DEPARTED";
        public const string Cancelled = "CANCELLED";
    }

    public static class CheckpointType
    {
        public const string Departure = "DEPARTURE";
        public const string Checkpoint = "CHECKPOINT";
        public const string CustomsClearing = "CUSTOMS_CLEARING";
        public const string QuarantineStation = "QUARANTINE_STATION";
        public const string Destination = "DESTINATION";
    }
}

public sealed class TripExecution : Entity
{
    public Guid TripId { get; set; }
    public string TripNo { get; set; } = null!;
    public string Status { get; set; } = TrackingConstants.TripExecutionStatus.Planned;
    public DateTime? DepartureAt { get; set; }
    public DateTime? ArrivalAt { get; set; }
    public DateTime? CurrentEta { get; set; }
    public Guid? CurrentCheckpointId { get; set; }
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdateAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
}

public sealed class CheckpointEvent : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? LocationId { get; set; }
    public int SequenceNo { get; set; }
    public string CheckpointType { get; set; } = null!;
    public string Status { get; set; } = TrackingConstants.CheckpointStatus.Planned;
    public DateTime? PlannedArrivalAt { get; set; }
    public DateTime? ActualArrivalAt { get; set; }
    public DateTime? PlannedDepartureAt { get; set; }
    public DateTime? ActualDepartureAt { get; set; }
    public decimal? ArrivalLatitude { get; set; }
    public decimal? ArrivalLongitude { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
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

