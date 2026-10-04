using System.Security.Claims;
using BuildingBlocks.Api;
using Compliance.Application.DTOs.Clearance;
using Compliance.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compliance.Api.Endpoints;

public static class ClearanceEndpoints
{
    public static IEndpointRouteBuilder MapClearanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance/clearance")
            .WithTags("Compliance Clearance Cases");

        group.MapGet("/cases/trip/{tripId:guid}", async (
            Guid tripId,
            IClearanceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetClearanceCasesByTripAsync(tripId, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<ClearanceCaseDto>>.Success(result));
        })
        .WithName("GetClearanceCasesByTrip");

        group.MapGet("/cases/{id:guid}", async (
            Guid id,
            IClearanceService service,
            CancellationToken ct) =>
        {
            var result = await service.GetClearanceCaseByIdAsync(id, ct);
            return result is not null
                ? Results.Ok(ApiResponse<ClearanceCaseDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Clearance case not found."));
        })
        .WithName("GetClearanceCaseById");

        group.MapPost("/cases", async (
            [FromBody] CreateClearanceCaseRequest request,
            ClaimsPrincipal user,
            IClearanceService service,
            CancellationToken ct) =>
        {
            var userId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.CreateClearanceCaseAsync(userId, request, ct);
            return Results.Created($"/api/v1/compliance/clearance/cases/{result.Id}", ApiResponse<ClearanceCaseDto>.Success(result));
        })
        .WithName("CreateClearanceCase");

        group.MapPost("/cases/{id:guid}/submit", async (
            Guid id,
            ClaimsPrincipal user,
            IClearanceService service,
            CancellationToken ct) =>
        {
            var userId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.SubmitClearanceCaseAsync(id, userId, ct);
            return result is not null
                ? Results.Ok(ApiResponse<ClearanceCaseDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Clearance case not found."));
        })
        .WithName("SubmitClearanceCase");

        group.MapPost("/cases/{id:guid}/review", async (
            Guid id,
            [FromBody] ReviewClearanceCaseRequest request,
            ClaimsPrincipal user,
            IClearanceService service,
            CancellationToken ct) =>
        {
            var reviewerId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000002");
            var result = await service.ReviewClearanceCaseAsync(id, reviewerId, request, ct);
            return result is not null
                ? Results.Ok(ApiResponse<ClearanceCaseDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Clearance case not found."));
        })
        .WithName("ReviewClearanceCase");

        return app;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("sub")
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("userId")
            ?? principal.FindFirst("id");

        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
