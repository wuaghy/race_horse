namespace Planning.Application.DTOs.Planning;

public record CreateTripInput(Guid OrderId);

public record TripDto(
    Guid Id,
    string TripNo,
    Guid OrderId,
    string Status,
    DateTime? PlannedDepartureAt,
    DateTime? PlannedArrivalAt,
    bool ComplianceReady,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo
);

public record RoutePlanDto(
    Guid Id,
    Guid TripId,
    Guid? ActiveVersionId,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo
);

public record RoutePlanVersionDto(
    Guid Id,
    Guid RoutePlanId,
    int VersionNo,
    string Status,
    string? Reason,
    decimal? TotalDistanceKm,
    int? EstimatedDurationMinutes,
    DateTime? PlannedDepartureAt,
    DateTime? PlannedArrivalAt,
    decimal AdditionalCost,
    string? CurrencyCode,
    Guid CreatedByUserId,
    Guid? ApprovedByUserId,
    DateTime? ApprovedAt,
    DateTime CreatedAt
);

public record CreateRoutePlanVersionInput(Guid ActorUserId, string? Reason = null);

public record ActorInput(Guid ActorUserId, string? Reason = null);

public record RouteLegDto(
    Guid Id,
    Guid RoutePlanVersionId,
    int SequenceNo,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    string TransportMode,
    DateTime? PlannedDepartureAt,
    DateTime? PlannedArrivalAt,
    decimal? DistanceKm,
    string Status
);

public record RouteLegInput(
    int SequenceNo,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    string Mode,
    DateTime DepartureAt,
    DateTime ArrivalAt,
    decimal? DistanceKm = null
);

public record CheckpointDto(
    Guid Id,
    Guid RouteLegId,
    int SequenceNo,
    Guid LocationId,
    string Type,
    DateTime? PlannedArrivalAt,
    DateTime? PlannedDepartureAt,
    string Status
);

public record CheckpointInput(
    int SequenceNo,
    Guid LocationId,
    string Type,
    DateTime ArrivalAt,
    DateTime DepartureAt
);

public record ResourceAssignmentDto(
    Guid Id,
    Guid RouteLegId,
    Guid? VehicleId,
    Guid? FlightBookingId,
    Guid AssignedByUserId,
    DateTime AssignedAt
);

public record ResourceInput(
    Guid? VehicleId,
    Guid? FlightBookingId,
    Guid? AssignedByUserId = null
);

public record StaffAssignmentDto(
    Guid Id,
    Guid TripId,
    Guid? RouteLegId,
    Guid UserId,
    string Role,
    DateTime AssignedAt,
    DateTime? ReleasedAt
);

public record StaffInput(
    Guid LegId,
    Guid UserId,
    string Role
);

public record HorseAssignmentDto(
    Guid Id,
    Guid TripId,
    Guid HorseId,
    Guid RouteLegId,
    Guid? StallId,
    DateTime? BoardedAt,
    DateTime? UnboardedAt
);

public record HorseInput(
    Guid HorseId,
    Guid LegId,
    Guid? StallId
);

public record TripAssignmentsSummaryDto(
    IReadOnlyList<ResourceAssignmentDto> Resources,
    IReadOnlyList<StaffAssignmentDto> Staff,
    IReadOnlyList<HorseAssignmentDto> Horses
);
