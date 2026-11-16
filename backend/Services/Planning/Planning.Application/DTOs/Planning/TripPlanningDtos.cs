namespace Planning.Application.DTOs.Planning;

public record CreateTripInput(
    Guid OrderId,
    Guid? RequestId = null,
    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    DateTime? RequestedDepartureAt = null,
    DateTime? RequestedArrivalAt = null,
    string? PreferredTransportMode = null,
    decimal? CustomPickupLatitude = null,
    decimal? CustomPickupLongitude = null,
    string? CustomPickupLabel = null,
    Guid? CustomerId = null,
    IReadOnlyList<Guid>? HorseIds = null,
    string? ReceiverName = null,
    string? ReceiverPhone = null,
    string? ReceiverEmail = null,
    string? OrderNo = null,
    string? RequestNo = null,
    string? CustomerName = null,
      IReadOnlyDictionary<Guid, string>? HorseNames = null,
      string PaymentStatus = "PAID",
      Guid? CustomerUserId = null);

public record TripDto(
    Guid Id,
    string TripNo,
    Guid? OrderId,
    string Status,
    DateTime? PlannedDepartureAt,
    DateTime? PlannedArrivalAt,
    bool ComplianceReady,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo,
    decimal? CustomPickupLatitude = null,
    decimal? CustomPickupLongitude = null,
    string? CustomPickupLabel = null,
    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    int OrderCount = 1,
    Guid? MergedIntoTripId = null,
    IReadOnlyList<string>? CustomerNames = null,
    IReadOnlyList<string>? HorseNames = null
);

public record TripOrderDto(Guid OrderId, Guid? RequestId, Guid? CustomerId, Guid? OriginLocationId, Guid? DestinationLocationId,
    DateTime? RequestedDepartureAt, DateTime? RequestedArrivalAt, string? PreferredTransportMode, IReadOnlyList<Guid> HorseIds,
    string? ReceiverName, string? ReceiverPhone, string? ReceiverEmail, DateTime? CompletedAt,
    decimal? CustomPickupLatitude = null, decimal? CustomPickupLongitude = null, string? CustomPickupLabel = null,
    string? OrderNo = null, string? RequestNo = null, string? CustomerName = null,
    IReadOnlyDictionary<Guid, string>? HorseNames = null, string PaymentStatus = "PAID");
public record DispatchTripDto(TripDto Trip, IReadOnlyList<TripOrderDto> Orders);
public record DispatchCalendarDayDto(DateOnly Date, int OrderCount, int TripCount, int UnassignedOrderCount);
public record CreateDispatchTripInput(IReadOnlyList<Guid> OrderIds);

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

public record CreateRoutePlanVersionInput(string? Reason = null);

public record CreateRouteChangeRequestInput(
    string Reason,
    int DelayEstimateMinutes,
    decimal AdditionalCost,
    string CurrencyCode);

public record RejectRouteChangeInput(string Reason);

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
    string Status,
    IReadOnlyList<RouteShapePoint>? RouteShape = null
);

public record RouteShapePoint(double Latitude, double Longitude);
public record RouteShapeInput(IReadOnlyList<RouteShapePoint> Points);
public record RouteTemplateInput(string Name, Guid OriginLocationId, Guid DestinationLocationId, string TransportMode, IReadOnlyList<RouteShapePoint> Points, decimal? CustomPickupLatitude = null, decimal? CustomPickupLongitude = null);
public record RouteTemplateDto(Guid Id, string Name, Guid OriginLocationId, Guid DestinationLocationId, string TransportMode, IReadOnlyList<RouteShapePoint> Points, DateTime CreatedAt, decimal? CustomPickupLatitude, decimal? CustomPickupLongitude);

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
    DateTime? ArrivalAt = null,
    DateTime? DepartureAt = null
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
      string Role,
      string? OutsideAreaOverrideReason = null
);

public record StaffRouteCountryDto(Guid CountryId, string CountryName, DateOnly? ValidUntil);
public record StaffRouteCountriesInput(IReadOnlyList<Guid> CountryIds, DateOnly? ValidUntil = null);
public record StaffOperatingAreaDto(Guid Id, Guid UserId, Guid LocationId, string LocationName, string City, string CountryName,
    decimal Latitude, decimal Longitude, decimal RadiusKm, DateTime UpdatedAt);
public record StaffOperatingAreaInput(Guid LocationId, decimal RadiusKm);
public record ReplaceStaffOperatingAreasInput(IReadOnlyList<StaffOperatingAreaInput> Areas);
public record NearestDriverDto(Guid UserId, double DistanceKm, string OperatingAreaName, DateTime AreaUpdatedAt);

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
