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
            .WithTags("Compliance Readiness Engine");

        group.MapPost("/evaluate", async (
            [FromBody] EvaluateComplianceReadinessRequest request,
            IComplianceReadinessService service,
            CancellationToken ct) =>
        {
            var result = await service.EvaluateTripReadinessAsync(request, ct);
            return Results.Ok(ApiResponse<ComplianceReadinessResultDto>.Success(result));
        })
        .WithName("EvaluateComplianceReadiness")
        .WithSummary("Evaluates if a trip has satisfied all document compliance requirements for departure. If ready, emits Compliance.ComplianceReady event.");

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
