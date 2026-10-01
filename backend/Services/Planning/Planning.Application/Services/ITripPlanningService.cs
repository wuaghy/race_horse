using Planning.Application.DTOs.Planning;

namespace Planning.Application.Services;

public interface ITripPlanningService
{
    // Trips
    Task<TripDto> CreateTripAsync(CreateTripInput input, CancellationToken ct = default);
    Task<TripDto> GetTripByIdAsync(Guid id, CancellationToken ct = default);

    // Route Plans & Versions
    Task<RoutePlanDto> CreateRoutePlanAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<RoutePlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> CreateNewVersionAsync(Guid planId, Guid actorUserId, string? reason = null, CancellationToken ct = default);

    // Route Legs (editable only when version is DRAFT)
    Task<IReadOnlyList<RouteLegDto>> GetLegsAsync(Guid versionId, CancellationToken ct = default);
    Task<RouteLegDto> AddLegAsync(Guid versionId, RouteLegInput input, CancellationToken ct = default);
    Task<RouteLegDto> UpdateLegAsync(Guid legId, RouteLegInput input, CancellationToken ct = default);
    Task DeleteLegAsync(Guid legId, CancellationToken ct = default);

    // Checkpoints (editable only when version is DRAFT)
    Task<IReadOnlyList<CheckpointDto>> GetCheckpointsAsync(Guid legId, CancellationToken ct = default);
    Task<CheckpointDto> AddCheckpointAsync(Guid legId, CheckpointInput input, CancellationToken ct = default);
    Task<CheckpointDto> UpdateCheckpointAsync(Guid checkpointId, CheckpointInput input, CancellationToken ct = default);
    Task DeleteCheckpointAsync(Guid checkpointId, CancellationToken ct = default);

    // Resource, Staff & Horse Assignments
    Task<ResourceAssignmentDto> AssignResourceAsync(Guid legId, ResourceInput input, CancellationToken ct = default);
    Task<StaffAssignmentDto> AssignStaffAsync(Guid tripId, StaffInput input, CancellationToken ct = default);
    Task<HorseAssignmentDto> AssignHorseAsync(Guid tripId, HorseInput input, CancellationToken ct = default);
    Task<TripAssignmentsSummaryDto> GetTripAssignmentsAsync(Guid tripId, CancellationToken ct = default);

    // State Transitions
    Task<RoutePlanVersionDto> SubmitRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> ApproveRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> ActivateRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
}
