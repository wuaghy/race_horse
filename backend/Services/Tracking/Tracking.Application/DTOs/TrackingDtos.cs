namespace Tracking.Application.DTOs;

public record TripExecutionDto(
    Guid Id,
    Guid TripId,
    string TripNo,
    string Status,
    DateTime? DepartureAt,
    DateTime? ArrivalAt,
    Guid? CurrentCheckpointId,
    decimal? CurrentLatitude,
    decimal? CurrentLongitude,
    DateTime? LastLocationUpdateAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo
);

public record InitiateTripExecutionInput(
    Guid TripId,
    string TripNo
);

public record CheckpointEventDto(
    Guid Id,
    Guid TripExecutionId,
    Guid LocationId,
    int SequenceNo,
    string CheckpointType,
    string Status,
    DateTime? PlannedArrivalAt,
    DateTime? ActualArrivalAt,
    DateTime? PlannedDepartureAt,
    DateTime? ActualDepartureAt,
    decimal? ArrivalLatitude,
    decimal? ArrivalLongitude,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo
);

public record RecordCheckpointEventInput(
    Guid LocationId,
    string CheckpointType,
    string Status,
    DateTime OccurredAt,
    decimal? Latitude,
    decimal? Longitude,
    string? Notes = null
);

public record UpdateTripLocationInput(
    decimal Latitude,
    decimal Longitude,
    DateTime UpdatedAt
);

public record GpsPointDto(
    Guid Id,
    Guid TripExecutionId,
    decimal Latitude,
    decimal Longitude,
    decimal? Altitude,
    decimal? Accuracy,
    decimal? Speed,
    decimal? Heading,
    DateTime RecordedAt,
    DateTime CreatedAt
);

public record IngestGpsPointInput(
    decimal Latitude,
    decimal Longitude,
    decimal? Altitude = null,
    decimal? Accuracy = null,
    decimal? Speed = null,
    decimal? Heading = null,
    DateTime? RecordedAt = null
);

public record LocationHistoryDto(
    Guid Id,
    Guid TripExecutionId,
    decimal Latitude,
    decimal Longitude,
    Guid? LocationId,
    string? LocationName,
    DateTime ArrivedAt,
    DateTime? DepartedAt,
    int SequenceNo,
    DateTime CreatedAt
);

public record EstimatedTimeOfArrivalDto(
    Guid Id,
    Guid TripExecutionId,
    DateTime CurrentEta,
    DateTime? PreviousEta,
    decimal? DistanceRemainingKm,
    int? EstimatedMinutesRemaining,
    decimal? AverageSpeedKph,
    string? DelayReason,
    bool? OnTrack,
    DateTime CalculatedAt,
    DateTime UpdatedAt
);

public record EtaCalculationInput(
    decimal DestinationLatitude,
    decimal DestinationLongitude,
    decimal? PlannedArrivalAtMinutes = null
);

public record HorseHealthLogDto(
    Guid Id,
    Guid TripExecutionId,
    Guid? HorseId,
    DateTime RecordedAt,
    string ConditionType,
    int? StressLevel,
    decimal? BodyTemperature,
    int? HeartRate,
    int? RespiratoryRate,
    string? Observation,
    string? PhotoUrl,
    string? RecordedByUserId,
    DateTime CreatedAt
);

public record RecordHealthLogInput(
    Guid? HorseId = null,
    DateTime? RecordedAt = null,
    string? ConditionType = null,
    int? StressLevel = null,
    decimal? BodyTemperature = null,
    int? HeartRate = null,
    int? RespiratoryRate = null,
    string? Observation = null,
    string? PhotoUrl = null,
    string? RecordedByUserId = null
);

public record HealthAlertDto(
    Guid Id,
    Guid TripExecutionId,
    Guid? HorseId,
    string AlertType,
    string Severity,
    string Message,
    int? MetricValue,
    int? MetricThreshold,
    string? Recommendation,
    bool Acknowledged,
    DateTime? AcknowledgedAt,
    string? AcknowledgedByUserId,
    DateTime AlertedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateHealthAlertInput(
    Guid? HorseId = null,
    string? AlertType = null,
    string? Severity = null,
    string? Message = null,
    int? MetricValue = null,
    int? MetricThreshold = null,
    string? Recommendation = null
);

public record AcknowledgeAlertInput(
    string? AcknowledgedByUserId = null
);

public record HorseHealthStatusDto(
    Guid Id,
    Guid TripExecutionId,
    Guid? HorseId,
    string CurrentStatus,
    int? CurrentStressLevel,
    int? AlertCount,
    int? AcknowledgedAlertCount,
    DateTime? LastHealthCheckAt,
    DateTime? LastAlertAt,
    DateTime UpdatedAt
);
