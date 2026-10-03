using BuildingBlocks.Api;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Application.Services;

namespace Planning.Api;

public static class PlanningCommandEndpoints
{
    public static RouteGroupBuilder MapPlanningCommands(this RouteGroupBuilder api)
    {
        // ==========================================
        // Trips (T10)
        // ==========================================
        api.MapPost("/trips", async (CreateTripInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var trip = await svc.CreateTripAsync(input, ct);
            return Results.Ok(ApiResponse<TripDto>.Success(trip, "Trip created or existing retrieved"));
        });

        api.MapGet("/trips/{id:guid}", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripDto>.Success(await svc.GetTripByIdAsync(id, ct))));

        // ==========================================
        // Route Plans & Versions (T10)
        // ==========================================
        api.MapPost("/trips/{id:guid}/route-plans", async (Guid id, ActorInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var plan = await svc.CreateRoutePlanAsync(id, input.ActorUserId, ct);
            return Results.Created($"/api/v1/route-plans/{plan.Id}", ApiResponse<RoutePlanDto>.Success(plan, "Route plan created successfully"));
        });

        api.MapGet("/route-plans/{id:guid}/versions", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<RoutePlanVersionDto>>.Success(await svc.GetVersionsAsync(id, ct))));

        api.MapPost("/route-plans/{id:guid}/versions", async (Guid id, CreateRoutePlanVersionInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var version = await svc.CreateNewVersionAsync(id, input.ActorUserId, input.Reason, ct);
            return Results.Created($"/api/v1/route-plan-versions/{version.Id}", ApiResponse<RoutePlanVersionDto>.Success(version, "New route version created"));
        });

        // ==========================================
        // Route Legs (T10)
        // ==========================================
        api.MapGet("/route-plan-versions/{id:guid}/legs", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<RouteLegDto>>.Success(await svc.GetLegsAsync(id, ct))));

        api.MapPost("/route-plan-versions/{id:guid}/legs", async (Guid id, RouteLegInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var leg = await svc.AddLegAsync(id, input, ct);
            return Results.Created($"/api/v1/route-legs/{leg.Id}", ApiResponse<RouteLegDto>.Success(leg, "Route leg added successfully"));
        });

        api.MapPut("/route-legs/{id:guid}", async (Guid id, RouteLegInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RouteLegDto>.Success(await svc.UpdateLegAsync(id, input, ct), "Route leg updated successfully")));

        api.MapDelete("/route-legs/{id:guid}", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
        {
            await svc.DeleteLegAsync(id, ct);
            return Results.NoContent();
        });

        // ==========================================
        // Checkpoints (T10)
        // ==========================================
        api.MapGet("/route-legs/{id:guid}/checkpoints", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<CheckpointDto>>.Success(await svc.GetCheckpointsAsync(id, ct))));

        api.MapPost("/route-legs/{id:guid}/checkpoints", async (Guid id, CheckpointInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var cp = await svc.AddCheckpointAsync(id, input, ct);
            return Results.Created($"/api/v1/checkpoints/{cp.Id}", ApiResponse<CheckpointDto>.Success(cp, "Checkpoint added successfully"));
        });

        api.MapPut("/checkpoints/{id:guid}", async (Guid id, CheckpointInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<CheckpointDto>.Success(await svc.UpdateCheckpointAsync(id, input, ct), "Checkpoint updated successfully")));

        api.MapDelete("/checkpoints/{id:guid}", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
        {
            await svc.DeleteCheckpointAsync(id, ct);
            return Results.NoContent();
        });

        // ==========================================
        // Assignments (T10)
        // ==========================================
        api.MapPost("/route-legs/{id:guid}/assignments/resource", async (Guid id, ResourceInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Created("", ApiResponse<ResourceAssignmentDto>.Success(await svc.AssignResourceAsync(id, input, ct), "Resource assigned successfully")));

        api.MapPost("/trips/{id:guid}/staff-assignments", async (Guid id, StaffInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Created("", ApiResponse<StaffAssignmentDto>.Success(await svc.AssignStaffAsync(id, input, ct), "Staff assigned successfully")));

        api.MapPost("/trips/{id:guid}/horse-assignments", async (Guid id, HorseInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Created("", ApiResponse<HorseAssignmentDto>.Success(await svc.AssignHorseAsync(id, input, ct), "Horse assigned successfully")));

        api.MapGet("/trips/{id:guid}/assignments", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripAssignmentsSummaryDto>.Success(await svc.GetTripAssignmentsAsync(id, ct))));

        // ==========================================
        // Route Version Lifecycle Transitions (T10)
        // ==========================================
        api.MapPost("/route-plan-versions/{id:guid}/submit", async (Guid id, ActorInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.SubmitRouteVersionAsync(id, input.ActorUserId, ct), "Route version submitted for approval")));

        api.MapPost("/route-plan-versions/{id:guid}/approve", async (Guid id, ActorInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.ApproveRouteVersionAsync(id, input.ActorUserId, ct), "Route version approved")));

        api.MapPost("/route-plan-versions/{id:guid}/activate", async (Guid id, ActorInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.ActivateRouteVersionAsync(id, input.ActorUserId, ct), "Route version activated")));

        // ==========================================
        // Readiness & Pre-departure (T11)
        // ==========================================
        api.MapPost("/trips/{id:guid}/compliance-ready", async (Guid id, ITripReadinessService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripDto>.Success(await svc.SetComplianceReadyAsync(id, ct), "Trip marked compliance ready")));

        api.MapGet("/trips/{id:guid}/readiness", async (Guid id, ITripReadinessService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripReadinessResponse>.Success(await svc.EvaluateReadinessAsync(id, ct))));

        api.MapGet("/trips/{id:guid}/blockers", async (Guid id, ITripReadinessService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<IReadOnlyList<string>>.Success((await svc.EvaluateReadinessAsync(id, ct)).Blockers)));

        api.MapPost("/trips/{id:guid}/confirm-ready", async (Guid id, ConfirmReadyRequest input, ITripReadinessService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<TripDto>.Success(await svc.ConfirmReadyAsync(id, input.ActorUserId, ct), "Trip confirmed READY for departure")));

        return api;
    }
}
