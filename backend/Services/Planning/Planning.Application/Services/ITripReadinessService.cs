using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;

namespace Planning.Application.Services;

public interface ITripReadinessService
{
    Task<TripDto> SetComplianceReadyAsync(Guid tripId, CancellationToken ct = default);
    Task<TripReadinessResponse> EvaluateReadinessAsync(Guid tripId, CancellationToken ct = default);
    Task<TripDto> ConfirmReadyAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default);
}
