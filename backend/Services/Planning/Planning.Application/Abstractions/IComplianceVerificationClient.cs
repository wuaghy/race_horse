using Planning.Application.DTOs.Readiness;

namespace Planning.Application.Abstractions;

public interface IComplianceVerificationClient
{
    Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        ComplianceTripQuery query,
        CancellationToken cancellationToken = default);
}
