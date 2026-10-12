using BuildingBlocks.Api;
using Tracking.Application.DTOs;
using Tracking.Application.Services;

namespace Tracking.Api.Endpoints;

public static class GpsTrackingEndpoints
{
    public static void MapGpsTrackingEndpoints(this WebApplication app)
    {
        var gps = app.MapGroup("/api/v1/trips/{tripId:guid}/gps")
            .WithTags("GPS Tracking")
            .RequireAuthorization();

        gps.MapPost("/points", IngestGpsPoint)
            .WithName("IngestGpsPoint")
            .WithOpenApi();

        gps.MapGet("/points", GetGpsPoints)
            .WithName("GetGpsPoints")
            .WithOpenApi();

        gps.MapGet("/points/latest", GetLatestGpsPoint)
            .WithName("GetLatestGpsPoint")
            .WithOpenApi();

        gps.MapGet("/locations", GetLocationHistory)
            .WithName("GetLocationHistory")
            .WithOpenApi();

        gps.MapPost("/locations", RecordLocationHistory)
            .WithName("RecordLocationHistory")
            .WithOpenApi();

        var eta = app.MapGroup("/api/v1/trips/{tripId:guid}/eta")
            .WithTags("ETA Tracking")
            .RequireAuthorization();

        eta.MapPost("/calculate", CalculateEta)
            .WithName("CalculateEta")
            .WithOpenApi();

        eta.MapGet("/current", GetCurrentEta)
            .WithName("GetCurrentEta")
            .WithOpenApi();
    }

    private static async Task<IResult> IngestGpsPoint(
        Guid tripId,
        IngestGpsPointInput input,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.IngestGpsPointAsync(tripId, input, cancellationToken);
        return Results.Created($"/api/v1/trips/{tripId}/gps/points/{result.Id}",
            ApiResponse<GpsPointDto>.Success(result, "GPS point ingested.", GetRequestId(context)));
    }

    private static async Task<IResult> GetGpsPoints(
        Guid tripId,
        int? limit,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetGpsPointsAsync(tripId, limit, cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<GpsPointDto>>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> GetLatestGpsPoint(
        Guid tripId,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLatestGpsPointAsync(tripId, cancellationToken);
        return result is null
            ? Results.NotFound(ApiResponse<object>.Failure(40400, "No GPS data found.", requestId: GetRequestId(context)))
            : Results.Ok(ApiResponse<GpsPointDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> GetLocationHistory(
        Guid tripId,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLocationHistoryAsync(tripId, cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<LocationHistoryDto>>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> RecordLocationHistory(
        Guid tripId,
        RecordLocationHistoryRequest request,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.RecordLocationHistoryAsync(
            tripId,
            request.Latitude,
            request.Longitude,
            request.LocationId,
            request.LocationName,
            cancellationToken);
        return Results.Created($"/api/v1/trips/{tripId}/gps/locations/{result.Id}",
            ApiResponse<LocationHistoryDto>.Success(result, "Location recorded.", GetRequestId(context)));
    }

    private static async Task<IResult> CalculateEta(
        Guid tripId,
        EtaCalculationInput input,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.CalculateEtaAsync(tripId, input, cancellationToken);
        return Results.Ok(ApiResponse<EstimatedTimeOfArrivalDto>.Success(result, "ETA calculated.", GetRequestId(context)));
    }

    private static async Task<IResult> GetCurrentEta(
        Guid tripId,
        IGpsTrackingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetCurrentEtaAsync(tripId, cancellationToken);
        return result is null
            ? Results.NotFound(ApiResponse<object>.Failure(40400, "ETA not calculated.", requestId: GetRequestId(context)))
            : Results.Ok(ApiResponse<EstimatedTimeOfArrivalDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}

public sealed record RecordLocationHistoryRequest(
    decimal Latitude,
    decimal Longitude,
    Guid? LocationId = null,
    string? LocationName = null
);
