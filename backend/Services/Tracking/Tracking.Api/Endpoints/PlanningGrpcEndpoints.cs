using BuildingBlocks.Api;
using Tracking.Api.Grpc;

namespace Tracking.Api.Endpoints;

public static class PlanningGrpcEndpoints
{
    public static void MapPlanningGrpcEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/trips/{tripId:guid}/planning-summary", async (
            Guid tripId,
            PlanningGrpcClient planning,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var result = await planning.GetTripSummaryAsync(tripId, cancellationToken);
            if (result is null)
            {
                return Results.NotFound(ApiResponse<object>.Failure(
                    40400,
                    "Trip was not found in Planning service.",
                    requestId: GetRequestId(context)));
            }

            var response = new
            {
                tripId = Guid.Parse(result.TripId),
                result.TripNo,
                orderId = Guid.Parse(result.OrderId),
                result.Status,
                result.ComplianceReady,
                plannedDepartureAt = ParseDate(result.PlannedDepartureAt),
                plannedArrivalAt = ParseDate(result.PlannedArrivalAt),
                result.DelayMinutes,
                result.VersionNo,
                source = "planning-grpc"
            };
            return Results.Ok(ApiResponse<object>.Success(response, requestId: GetRequestId(context)));
        })
        .RequireAuthorization("TrackingRead")
        .WithTags("Planning gRPC")
        .WithName("GetPlanningTripSummaryViaGrpc")
        .WithOpenApi();
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}
