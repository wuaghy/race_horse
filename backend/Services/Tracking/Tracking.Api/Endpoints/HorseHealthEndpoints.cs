using BuildingBlocks.Api;
using Tracking.Application.DTOs;
using Tracking.Application.Services;

namespace Tracking.Api.Endpoints;

public static class HorseHealthEndpoints
{
    public static void MapHorseHealthEndpoints(this WebApplication app)
    {
        var health = app.MapGroup("/api/v1/trips/{tripId:guid}/horses/{horseId:guid}/health")
            .WithTags("Horse Health")
            .RequireAuthorization();

        health.MapPost("/logs", RecordHealthLog)
            .RequireAuthorization("TrackingUpdate")
            .WithName("RecordHealthLog")
            .WithOpenApi();

        health.MapGet("/logs", GetHealthLogs)
            .RequireAuthorization("TrackingRead")
            .WithName("GetHealthLogs")
            .WithOpenApi();

        health.MapGet("/logs/{logId:guid}", GetHealthLog)
            .RequireAuthorization("TrackingRead")
            .WithName("GetHealthLog")
            .WithOpenApi();

        health.MapPost("/alerts", CreateHealthAlert)
            .RequireAuthorization("TrackingUpdate")
            .WithName("CreateHealthAlert")
            .WithOpenApi();

        health.MapGet("/alerts", GetHealthAlerts)
            .RequireAuthorization("TrackingRead")
            .WithName("GetHealthAlerts")
            .WithOpenApi();

        health.MapPost("/alerts/{alertId:guid}/acknowledge", AcknowledgeAlert)
            .RequireAuthorization("TrackingUpdate")
            .WithName("AcknowledgeAlert")
            .WithOpenApi();

        health.MapGet("/status", GetHealthStatus)
            .RequireAuthorization("TrackingRead")
            .WithName("GetHealthStatus")
            .WithOpenApi();
    }

    private static async Task<IResult> RecordHealthLog(
        Guid tripId,
        Guid horseId,
        RecordHealthLogInput input,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var internalInput = input with { HorseId = horseId };
        var result = await service.RecordHealthLogAsync(tripId, internalInput, cancellationToken);
        return Results.Created($"/api/v1/trips/{tripId}/horses/{horseId}/health/logs/{result.Id}",
            ApiResponse<HorseHealthLogDto>.Success(result, "Health log recorded.", GetRequestId(context)));
    }

    private static async Task<IResult> GetHealthLogs(
        Guid tripId,
        Guid horseId,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHealthLogsAsync(tripId, horseId, cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<HorseHealthLogDto>>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> GetHealthLog(
        Guid tripId,
        Guid horseId,
        Guid logId,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHealthLogAsync(tripId, logId, cancellationToken);
        return result is null
            ? Results.NotFound(ApiResponse<object>.Failure(40400, "Health log not found.", requestId: GetRequestId(context)))
            : Results.Ok(ApiResponse<HorseHealthLogDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> CreateHealthAlert(
        Guid tripId,
        Guid horseId,
        CreateHealthAlertInput input,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var internalInput = input with { HorseId = horseId };
        var result = await service.CreateHealthAlertAsync(tripId, internalInput, cancellationToken);
        return Results.Created($"/api/v1/trips/{tripId}/horses/{horseId}/health/alerts/{result.Id}",
            ApiResponse<HealthAlertDto>.Success(result, "Health alert created.", GetRequestId(context)));
    }

    private static async Task<IResult> GetHealthAlerts(
        Guid tripId,
        Guid horseId,
        bool? unacknowledgedOnly,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHealthAlertsAsync(tripId, horseId, unacknowledgedOnly, cancellationToken);
        return Results.Ok(ApiResponse<IReadOnlyList<HealthAlertDto>>.Success(result, requestId: GetRequestId(context)));
    }

    private static async Task<IResult> AcknowledgeAlert(
        Guid tripId,
        Guid horseId,
        Guid alertId,
        AcknowledgeAlertInput input,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.AcknowledgeAlertAsync(tripId, alertId, input, cancellationToken);
        return Results.Ok(ApiResponse<HealthAlertDto>.Success(result, "Alert acknowledged.", GetRequestId(context)));
    }

    private static async Task<IResult> GetHealthStatus(
        Guid tripId,
        Guid horseId,
        IHorseHealthService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.GetHealthStatusAsync(tripId, horseId, cancellationToken);
        return Results.Ok(ApiResponse<HorseHealthStatusDto>.Success(result, requestId: GetRequestId(context)));
    }

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}
