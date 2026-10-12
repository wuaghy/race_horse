namespace Contracts.IntegrationEvents;

public sealed record TripDepartedData(
    Guid TripId,
    string TripNo,
    DateTime DepartedAt,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TripDepartedEvent : IntegrationEvent<TripDepartedData>
{
    public override string EventType => "Tracking.TripDeparted";
    public override string Source => "tracking-service";
}

public sealed record CheckpointEventData(
    Guid TripExecutionId,
    Guid TripId,
    int SequenceNo,
    string CheckpointType,
    string Status,
    Guid LocationId,
    DateTime EventAt,
    decimal? Latitude,
    decimal? Longitude,
    string? Notes = null);

public sealed record CheckpointEventOccurredEvent : IntegrationEvent<CheckpointEventData>
{
    public override string EventType => "Tracking.CheckpointEventOccurred";
    public override string Source => "tracking-service";
}

public sealed record TripArrivedData(
    Guid TripId,
    string TripNo,
    DateTime ArrivedAt,
    decimal? Latitude,
    decimal? Longitude);

public sealed record TripArrivedEvent : IntegrationEvent<TripArrivedData>
{
    public override string EventType => "Tracking.TripArrived";
    public override string Source => "tracking-service";
}

public sealed record HealthAlertRaisedData(
    Guid TripExecutionId,
    Guid? HorseId,
    string AlertType,
    string Severity,
    string Message,
    DateTime AlertAt);

public sealed record HealthAlertRaisedEvent : IntegrationEvent<HealthAlertRaisedData>
{
    public override string EventType => "Tracking.HealthAlertRaised";
    public override string Source => "tracking-service";
}

public sealed record GpsPointRecordedData(
    Guid TripExecutionId,
    Guid TripId,
    decimal Latitude,
    decimal Longitude,
    decimal? Altitude,
    decimal? Accuracy,
    decimal? Speed,
    decimal? Heading,
    DateTime RecordedAt);

public sealed record GpsPointRecordedEvent : IntegrationEvent<GpsPointRecordedData>
{
    public override string EventType => "Tracking.GpsPointRecorded";
    public override string Source => "tracking-service";
}

public sealed record EtaUpdatedData(
    Guid TripExecutionId,
    Guid TripId,
    DateTime CurrentEta,
    DateTime? PreviousEta,
    decimal? DistanceRemainingKm,
    int? EstimatedMinutesRemaining,
    decimal? AverageSpeedKph,
    bool? OnTrack,
    string? DelayReason = null);

public sealed record EtaUpdatedEvent : IntegrationEvent<EtaUpdatedData>
{
    public override string EventType => "Tracking.EtaUpdated";
    public override string Source => "tracking-service";
}
