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
    public Guid LocationId { get; set; }
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

public sealed class GpsPoint : Entity
{
    public Guid TripExecutionId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? Altitude { get; set; }
    public decimal? Accuracy { get; set; }
    public decimal? Speed { get; set; }
    public decimal? Heading { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class LocationHistory : Entity
{
    public Guid TripExecutionId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime ArrivedAt { get; set; }
    public DateTime? DepartedAt { get; set; }
    public int SequenceNo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class EstimatedTimeOfArrival : Entity
{
    public Guid TripExecutionId { get; set; }
    public DateTime CurrentEta { get; set; }
    public DateTime? PreviousEta { get; set; }
    public decimal? DistanceRemainingKm { get; set; }
    public int? EstimatedMinutesRemaining { get; set; }
    public decimal? AverageSpeedKph { get; set; }
    public string? DelayReason { get; set; }
    public bool? OnTrack { get; set; }
    public DateTime CalculatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class HorseHealthLog : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public DateTime RecordedAt { get; set; }
    public string ConditionType { get; set; } = null!;
    public int? StressLevel { get; set; }
    public decimal? BodyTemperature { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public string? Observation { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HealthAlert : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public string AlertType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Message { get; set; } = null!;
    public int? MetricValue { get; set; }
    public int? MetricThreshold { get; set; }
    public string? Recommendation { get; set; }
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedByUserId { get; set; }
    public DateTime AlertedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HorseHealthStatus : Entity
{
    public Guid TripExecutionId { get; set; }
    public Guid? HorseId { get; set; }
    public string CurrentStatus { get; set; } = null!;
    public int? CurrentStressLevel { get; set; }
    public int? AlertCount { get; set; }
    public int? AcknowledgedAlertCount { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    public DateTime? LastAlertAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

