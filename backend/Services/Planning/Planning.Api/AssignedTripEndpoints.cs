using System.Security.Claims;
using BuildingBlocks.Api;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;

namespace Planning.Api;

public static class AssignedTripEndpoints
{
    public static RouteGroupBuilder MapAssignedTripEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/my-trips", async (ClaimsPrincipal user, ITripPlanningService service, CancellationToken cancellationToken) =>
        {
            var userIdValue = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdValue, out var userId))
                return Results.Unauthorized();

            var trips = await service.GetAssignedTripsAsync(userId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<TripDto>>.Success(trips));
        }).WithName("ListMyAssignedTrips");

        return api;
    }
}
