using BuildingBlocks.Api;
using Compliance.Application.DTOs.Readiness;
using Compliance.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compliance.Api.Endpoints;

public static class ReadinessEndpoints
{
    public static IEndpointRouteBuilder MapReadinessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance/readiness")
            .WithTags("Compliance Readiness Engine")
            .RequireAuthorization("ComplianceReadiness");

        group.MapPost("/evaluate", async (
            [FromBody] EvaluateDispatchTripReadinessRequest request,
            IComplianceReadinessService service,
            CancellationToken ct) =>
        {
            var result = await service.EvaluateDispatchTripReadinessAsync(request, ct);
            return Results.Ok(ApiResponse<DispatchComplianceReadinessResult>.Success(result));
        })
        .WithName("EvaluateComplianceReadiness")
        .WithSummary("Evaluates request and horse documents plus route-leg clearance requirements for dispatch readiness.");

        group.MapGet("/trip/{tripId:guid}", async (
            Guid tripId,
            [FromQuery] Guid originCountryId,
            [FromQuery] Guid destinationCountryId,
            [FromQuery] Guid[] horseIds,
            [FromQuery] DateOnly? departureDate,
            IComplianceReadinessService service,
            CancellationToken ct) =>
        {
            var request = new EvaluateComplianceReadinessRequest(tripId, originCountryId, destinationCountryId, horseIds, departureDate);
            var result = await service.EvaluateTripReadinessAsync(request, ct);
            return Results.Ok(ApiResponse<ComplianceReadinessResultDto>.Success(result));
        })
        .WithName("GetTripComplianceReadiness");

        return app;
    }
}
