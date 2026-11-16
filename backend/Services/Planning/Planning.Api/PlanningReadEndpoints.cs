using BuildingBlocks.Api;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Application.Services;

namespace Planning.Api;

public static class PlanningReadEndpoints
{
    public static RouteGroupBuilder MapPlanningReadEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/route-legs/templates", async (Guid? originLocationId, Guid? destinationLocationId, string? mode, decimal? pickupLatitude, decimal? pickupLongitude, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<RouteTemplateDto>>.Success(await service.GetRouteTemplatesAsync(originLocationId, destinationLocationId, mode, pickupLatitude, pickupLongitude, ct))));
        api.MapGet("/dispatch-calendar", async (int year, int month, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<DispatchCalendarDayDto>>.Success(await service.GetDispatchCalendarAsync(year, month, ct))));

        api.MapGet("/dispatch-orders", async (DateOnly? date, string? search, ITripPlanningService service, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(search))
                return Results.Ok(ApiResponse<IReadOnlyList<TripOrderDto>>.Success(await service.SearchDispatchOrdersAsync(search, ct)));
            if (!date.HasValue)
                return Results.BadRequest(ApiResponse<object>.Failure(40000, "A departure date or search term is required."));
            return Results.Ok(ApiResponse<IReadOnlyList<TripOrderDto>>.Success(await service.GetDispatchOrdersAsync(date.Value, ct)));
        });

        api.MapGet("/trips", async ([AsParameters] PaginationFilter filter, string? status, string? sortBy, string? sortDirection, DateOnly? date, ITripPlanningService service, CancellationToken ct) =>
        {
            var result = await service.GetTripsAsync(filter, status, sortBy, sortDirection, ct, date);
            return Results.Ok(ApiResponse<IReadOnlyList<TripDto>>.Success(
                result.Items,
                new PaginationMeta { Page = result.Page, PageSize = result.PageSize, TotalItems = result.TotalItems, TotalPages = result.TotalPages }));
        }).WithName("ListTrips");

        api.MapGet("/trips/{id:guid}", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripDto>.Success(await service.GetTripByIdAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/orders", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<TripOrderDto>>.Success(await service.GetTripOrdersAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/route-plan", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<RoutePlanDto>.Success(await service.GetRoutePlanByTripAsync(id, ct))));

        api.MapGet("/route-plans/{id:guid}/versions", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<RoutePlanVersionDto>>.Success(await service.GetVersionsAsync(id, ct))));

        api.MapGet("/route-plan-versions/{id:guid}/legs", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<RouteLegDto>>.Success(await service.GetLegsAsync(id, ct))));

        api.MapGet("/route-legs/{id:guid}/checkpoints", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<CheckpointDto>>.Success(await service.GetCheckpointsAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/assignments", async (Guid id, ITripPlanningService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripAssignmentsSummaryDto>.Success(await service.GetTripAssignmentsAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/readiness", async (Guid id, ITripReadinessService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripReadinessResponse>.Success(await service.EvaluateReadinessAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/blockers", async (Guid id, ITripReadinessService service, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<string>>.Success((await service.EvaluateReadinessAsync(id, ct)).Blockers)));

        return api;
    }
}
