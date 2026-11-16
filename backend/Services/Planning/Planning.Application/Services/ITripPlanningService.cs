using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Common;

namespace Planning.Application.Services;

public interface ITripPlanningService
{
    // Trips
    Task<TripDto> CreateTripAsync(CreateTripInput input, CancellationToken ct = default);
    Task RegisterOrderForDispatchAsync(CreateTripInput input, CancellationToken ct = default);
    Task<IReadOnlyList<DispatchCalendarDayDto>> GetDispatchCalendarAsync(int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<TripOrderDto>> GetDispatchOrdersAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<TripOrderDto>> SearchDispatchOrdersAsync(string search, CancellationToken ct = default);
    Task<DispatchTripDto> CreateDispatchTripAsync(CreateDispatchTripInput input, CancellationToken ct = default);
    Task<TripDto> GetTripByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<TripDto>> GetTripsAsync(PaginationFilter filter, string? status = null, string? sortBy = null, string? sortDirection = null, CancellationToken ct = default, DateOnly? date = null);
    Task<IReadOnlyList<TripOrderDto>> GetTripOrdersAsync(Guid tripId, CancellationToken ct = default);
    Task<PagedResult<TripDto>> GetAssignedTripsAsync(Guid userId, PaginationFilter filter, string? status = null, string? sortBy = null, string? sortDirection = null, CancellationToken ct = default);
    Task<IReadOnlyList<TripOrderDto>> GetAssignedTripOrdersAsync(Guid tripId, Guid userId, CancellationToken ct = default);
    Task<RoutePlanDto> GetRoutePlanByTripAsync(Guid tripId, CancellationToken ct = default);

    // Route Plans & Versions
    Task<RoutePlanDto> CreateRoutePlanAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<RoutePlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> CreateNewVersionAsync(Guid planId, Guid actorUserId, string? reason = null, CancellationToken ct = default);
    Task<RoutePlanVersionDto> CreateRouteChangeProposalAsync(Guid tripId, Guid actorUserId, CreateRouteChangeRequestInput input, Guid? correlationId = null, CancellationToken ct = default);

    // Route Legs (editable only when version is DRAFT)
    Task<IReadOnlyList<RouteLegDto>> GetLegsAsync(Guid versionId, CancellationToken ct = default);
    Task<RouteLegDto> AddLegAsync(Guid versionId, RouteLegInput input, CancellationToken ct = default);
    Task<RouteLegDto> UpdateLegAsync(Guid legId, RouteLegInput input, CancellationToken ct = default);
    Task<RouteLegDto> UpdateRouteShapeAsync(Guid legId, RouteShapeInput input, CancellationToken ct = default);
    Task<IReadOnlyList<RouteTemplateDto>> GetRouteTemplatesAsync(Guid? originLocationId, Guid? destinationLocationId, string? mode, decimal? pickupLatitude, decimal? pickupLongitude, CancellationToken ct = default);
    Task<RouteTemplateDto> CreateRouteTemplateAsync(RouteTemplateInput input, Guid actorUserId, CancellationToken ct = default);
    Task DeleteLegAsync(Guid legId, CancellationToken ct = default);

    // Checkpoints (editable only when version is DRAFT)
    Task<IReadOnlyList<CheckpointDto>> GetCheckpointsAsync(Guid legId, CancellationToken ct = default);
    Task<CheckpointDto> AddCheckpointAsync(Guid legId, CheckpointInput input, CancellationToken ct = default);
    Task<CheckpointDto> UpdateCheckpointAsync(Guid checkpointId, CheckpointInput input, CancellationToken ct = default);
    Task DeleteCheckpointAsync(Guid checkpointId, CancellationToken ct = default);

    // Resource, Staff & Horse Assignments
    Task<ResourceAssignmentDto> AssignResourceAsync(Guid legId, ResourceInput input, CancellationToken ct = default);
    Task<StaffAssignmentDto> AssignStaffAsync(Guid tripId, StaffInput input, CancellationToken ct = default, Guid? actorUserId = null, bool canOverrideOperatingArea = false);
    Task<HorseAssignmentDto> AssignHorseAsync(Guid tripId, HorseInput input, CancellationToken ct = default);
    Task<TripAssignmentsSummaryDto> GetTripAssignmentsAsync(Guid tripId, CancellationToken ct = default);

    // State Transitions
    Task<RoutePlanVersionDto> SubmitRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> ApproveRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
    Task<RoutePlanVersionDto> RejectRouteVersionAsync(Guid versionId, Guid actorUserId, string reason, Guid? correlationId = null, CancellationToken ct = default);
    Task<RoutePlanVersionDto> ActivateRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default);
}
