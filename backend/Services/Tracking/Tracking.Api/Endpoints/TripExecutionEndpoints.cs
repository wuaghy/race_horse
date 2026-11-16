using BuildingBlocks.Api;
using System.Security.Claims;
using Tracking.Application.DTOs;
using Tracking.Application.Services;

namespace Tracking.Api.Endpoints;

public static class TripExecutionEndpoints
{
    public static void MapTripExecutionEndpoints(this WebApplication app)
    {
        var trips = app.MapGroup("/api/v1/trips")
            .WithTags("Trip Execution")
            .RequireAuthorization()
            .RequireCustomerTripOwnership();

        trips.MapPost("/", InitiateTripExecution)
            .RequireAuthorization("TrackingUpdate")
            .WithName("InitiateTripExecution")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}", GetTripExecution)
            .RequireAuthorization("TrackingRead")
            .WithName("GetTripExecution")
            .WithOpenApi();

        trips.MapGet("/", GetTripExecutions)
            .RequireAuthorization("TrackingRead")
            .WithName("ListTripExecutions")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/departure", RecordTripDeparture)
            .RequireAuthorization("TrackingUpdate")
            .WithName("RecordTripDeparture")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/arrival", RecordTripArrival)
            .RequireAuthorization("TrackingUpdate")
            .WithName("RecordTripArrival")
            .WithOpenApi();

        trips.MapPut("/{tripId:guid}/location", UpdateTripLocation)
            .RequireAuthorization("TrackingUpdate")
            .WithName("UpdateTripLocation")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/checkpoints", RecordCheckpointEvent)
            .RequireAuthorization("TrackingUpdate")
            .WithName("RecordCheckpointEvent")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}/checkpoints", GetCheckpointEvents)
            .RequireAuthorization("TrackingRead")
            .WithName("GetCheckpointEvents")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}/checkpoints/{checkpointId:guid}", GetCheckpointEvent)
            .RequireAuthorization("TrackingRead")
            .WithName("GetCheckpointEvent")
            .WithOpenApi();
    }

    private static async Task<IResult> InitiateTripExecution(
        InitiateTripExecutionInput input,
        ITripExecutionService service,
        Tracking.Api.Grpc.PlanningGrpcClient planning,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var trip = await planning.GetTripSummaryAsync(input.TripId, cancellationToken);
        if (trip is null)
            return Results.NotFound(ApiResponse<object>.Failure(40400, "Trip was not found in Planning service.", requestId: GetRequestId(context)));
        if (!string.Equals(trip.Status, "READY", StringComparison.OrdinalIgnoreCase))
            return Results.Conflict(ApiResponse<object>.Failure(40900, $"Trip is {trip.Status}; Logistics must confirm READY before execution can start.", requestId: GetRequestId(context)));

        var result = await service.InitiateTripExecutionAsync(input, cancellationToken);
        return Results.Created($"/api/v1/trips/{result.Id}",
            ApiResponse<TripExecutionDto>.Success(result, "Trip execution initiated.", GetRequestId(context)));
    }

    private static async Task<IResult> GetTripExecution(
        Guid tripId,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTripExecutionAsync(tripId, cancellationToken);
        if (result is null)
        {
            var error = new ApiError { Field = "tripId", Reason = "TRIP_EXECUTION_NOT_FOUND", Message = "Trip execution not found." };
            return Results.NotFound(ApiResponse<object>.Failure(40400, "Trip execution not found.", new List<ApiError> { error }, GetRequestId(context)));
        }
        return Results.Ok(ApiResponse<TripExecutionDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> GetTripExecutions(int? page, int? pageSize, string? search, string? status, string? sortDirection, ClaimsPrincipal user, Tracking.Api.Grpc.PlanningGrpcClient planning, ITripExecutionService service, HttpContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid>? customerTripIds = null;
        if (user.IsInRole("CUSTOMER"))
        {
            if (!Guid.TryParse(user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId)) return Results.Unauthorized();
            customerTripIds = await planning.GetCustomerTripIdsAsync(userId, cancellationToken);
        }
        var result = await service.GetTripExecutionsAsync(page ?? 0, pageSize ?? 20, search, status, sortDirection, cancellationToken, customerTripIds);
        return Results.Ok(ApiResponse<IReadOnlyList<TripExecutionDto>>.Success(result.Items, new PaginationMeta { Page = result.Page, PageSize = result.PageSize, TotalItems = result.TotalItems, TotalPages = result.TotalPages }, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> RecordTripDeparture(
        Guid tripId,
        RecordTripDepartureRequest request,
        ClaimsPrincipal user,
        ITripExecutionService service,
        Tracking.Api.Grpc.PlanningGrpcClient planning,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var actorUserId))
            return Results.Unauthorized();
        var readiness = await planning.ValidateTripDepartureAsync(tripId, cancellationToken);
        if (!readiness.Allowed)
            return Results.Conflict(ApiResponse<object>.Failure(40900,
                string.IsNullOrWhiteSpace(readiness.Status) ? "Trip is not ready for departure." : $"Trip is {readiness.Status}; departure is blocked.",
                readiness.Blockers.Select(blocker => new ApiError { Field = "blockers", Reason = "TRIP_DEPARTURE_BLOCKED", Message = blocker }).ToList(),
                GetRequestId(context)));
        var result = await service.RecordTripDepartureAsync(tripId, request.Latitude, request.Longitude, actorUserId, cancellationToken);
        return Results.Ok(ApiResponse<TripExecutionDto>.Success(result, "Trip departure recorded.", GetRequestId(context)));
    }

    private static async Task<IResult> RecordTripArrival(
        Guid tripId,
        RecordTripArrivalRequest request,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordTripArrivalAsync(tripId, request.Latitude, request.Longitude, cancellationToken);
        return Results.Ok(ApiResponse<TripExecutionDto>.Success(result, "Trip arrival recorded.", GetRequestId(context)));
    }

    private static async Task<IResult> UpdateTripLocation(
        Guid tripId,
        UpdateTripLocationInput input,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateTripLocationAsync(tripId, input, cancellationToken);
        return Results.Ok(ApiResponse<TripExecutionDto>.Success(result, "Trip location updated.", GetRequestId(context)));
    }

    private static async Task<IResult> RecordCheckpointEvent(
        Guid tripId,
        RecordCheckpointEventInput input,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordCheckpointEventAsync(tripId, input, cancellationToken);
        return Results.Created($"/api/v1/trips/{tripId}/checkpoints/{result.Id}",
            ApiResponse<CheckpointEventDto>.Success(result, "Checkpoint event recorded.", GetRequestId(context)));
    }

    private static async Task<IResult> GetCheckpointEvents(
        Guid tripId,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCheckpointEventsAsync(tripId, cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<CheckpointEventDto>>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> GetCheckpointEvent(
        Guid tripId,
        Guid checkpointId,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCheckpointEventAsync(tripId, checkpointId, cancellationToken);
        if (result is null)
        {
            var error = new ApiError { Field = "checkpointId", Reason = "CHECKPOINT_EVENT_NOT_FOUND", Message = "Checkpoint event not found." };
            return Results.NotFound(ApiResponse<object>.Failure(40400, "Checkpoint event not found.", new List<ApiError> { error }, GetRequestId(context)));
        }
        return Results.Ok(ApiResponse<CheckpointEventDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}

public sealed record RecordTripDepartureRequest(
    decimal Latitude,
    decimal Longitude
);

public sealed record RecordTripArrivalRequest(
    decimal Latitude,
    decimal Longitude
);
