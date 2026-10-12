using BuildingBlocks.Api;
using Tracking.Application.DTOs;
using Tracking.Application.Services;

namespace Tracking.Api.Endpoints;

public static class TripExecutionEndpoints
{
    public static void MapTripExecutionEndpoints(this WebApplication app)
    {
        var trips = app.MapGroup("/api/v1/trips")
            .WithTags("Trip Execution")
            .RequireAuthorization();

        trips.MapPost("/", InitiateTripExecution)
            .WithName("InitiateTripExecution")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}", GetTripExecution)
            .WithName("GetTripExecution")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/departure", RecordTripDeparture)
            .WithName("RecordTripDeparture")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/arrival", RecordTripArrival)
            .WithName("RecordTripArrival")
            .WithOpenApi();

        trips.MapPut("/{tripId:guid}/location", UpdateTripLocation)
            .WithName("UpdateTripLocation")
            .WithOpenApi();

        trips.MapPost("/{tripId:guid}/checkpoints", RecordCheckpointEvent)
            .WithName("RecordCheckpointEvent")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}/checkpoints", GetCheckpointEvents)
            .WithName("GetCheckpointEvents")
            .WithOpenApi();

        trips.MapGet("/{tripId:guid}/checkpoints/{checkpointId:guid}", GetCheckpointEvent)
            .WithName("GetCheckpointEvent")
            .WithOpenApi();
    }

    private static async Task<IResult> InitiateTripExecution(
        InitiateTripExecutionInput input,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
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

    private static async Task<IResult> RecordTripDeparture(
        Guid tripId,
        RecordTripDepartureRequest request,
        ITripExecutionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordTripDepartureAsync(tripId, request.Latitude, request.Longitude, cancellationToken);
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
