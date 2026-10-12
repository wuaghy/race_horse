namespace Tracking.Domain;

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
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
