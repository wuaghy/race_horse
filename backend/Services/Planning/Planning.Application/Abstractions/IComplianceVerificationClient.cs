using Planning.Application.DTOs.Readiness;

namespace Planning.Application.Abstractions;

public interface IComplianceVerificationClient
{
    Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        Guid tripId,
        Guid originCountryId,
        Guid destinationCountryId,
        IReadOnlyList<Guid> horseIds,
        DateOnly? plannedDepartureDate = null,
        CancellationToken cancellationToken = default);

    Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
