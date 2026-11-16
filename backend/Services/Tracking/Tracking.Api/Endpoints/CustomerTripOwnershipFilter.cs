using System.Security.Claims;
using BuildingBlocks.Api;
using Tracking.Api.Grpc;

namespace Tracking.Api.Endpoints;

internal static class CustomerTripOwnershipFilter
{
    public static RouteGroupBuilder RequireCustomerTripOwnership(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.User;
            if (!user.IsInRole("CUSTOMER")) return await next(context);

            if (!Guid.TryParse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            if (!Guid.TryParse(context.HttpContext.Request.RouteValues["tripId"]?.ToString(), out var tripId))
                return await next(context); // The list endpoint applies the owner projection before pagination.

            Guid? horseId = Guid.TryParse(context.HttpContext.Request.RouteValues["horseId"]?.ToString(), out var parsedHorseId)
                ? parsedHorseId
                : null;
            var planning = context.HttpContext.RequestServices.GetRequiredService<PlanningGrpcClient>();
            if (!await planning.CustomerCanAccessTripAsync(tripId, userId, horseId, context.HttpContext.RequestAborted))
                return Results.NotFound(ApiResponse<object>.Failure(40400, "Trip was not found.", requestId: GetRequestId(context.HttpContext)));

            return await next(context);
        });
        return group;
    }

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}
