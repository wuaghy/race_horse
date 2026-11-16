using System.Security.Claims;
using BuildingBlocks.Api;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;

namespace Planning.Api;

public static class AssignedTripEndpoints
{
    public static RouteGroupBuilder MapAssignedTripEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/my-trips", async ([AsParameters] PaginationFilter filter, string? status, string? sortBy, string? sortDirection, ClaimsPrincipal user, ITripPlanningService service, CancellationToken cancellationToken) =>
        {
            var userIdValue = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
                return Results.Unauthorized();

            var result = await service.GetAssignedTripsAsync(userId, filter, status, sortBy, sortDirection, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TripDto>>.Success(
                result.Items,
                new PaginationMeta { Page = result.Page, PageSize = result.PageSize, TotalItems = result.TotalItems, TotalPages = result.TotalPages }));
        }).WithName("ListMyAssignedTrips");

        api.MapGet("/my-trips/{tripId:guid}/orders", async (Guid tripId, ClaimsPrincipal user, ITripPlanningService service, CancellationToken cancellationToken) =>
        {
            var userIdValue = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
                return Results.Unauthorized();

            var orders = await service.GetAssignedTripOrdersAsync(tripId, userId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TripOrderDto>>.Success(orders));
        }).WithName("GetMyAssignedTripOrders");

        return api;
    }
}
