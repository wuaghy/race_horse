namespace Planning.Application.DTOs.Readiness;

public record ComplianceTripQuery(
    Guid TripId,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    IReadOnlyList<Guid> HorseIds,
    DateOnly? PlannedDepartureDate
);
