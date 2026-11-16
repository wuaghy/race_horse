using BuildingBlocks.Api;
using System.Security.Claims;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;

namespace Planning.Api;

public static class PlanningCommandEndpoints
{
    public static RouteGroupBuilder MapPlanningCommands(this RouteGroupBuilder api)
    {
        api.MapPost("/route-legs/templates", async (RouteTemplateInput input, ClaimsPrincipal user, ITripPlanningService service, CancellationToken ct) =>
        {
            var actor = GetActorId(user);
            if (actor is null) return Results.Unauthorized();
            var template = await service.CreateRouteTemplateAsync(input, actor.Value, ct);
            return Results.Ok(ApiResponse<RouteTemplateDto>.Success(template, "Route template saved"));
        }).RequireAuthorization("PlanningLogistics");
        // ==========================================
        // Trips (T10)
        // ==========================================
        api.MapPost("/dispatch-trips", async (CreateDispatchTripInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateDispatchTripAsync(input, ct);
            return Results.Created($"/api/v1/trips/{result.Trip.Id}", ApiResponse<DispatchTripDto>.Success(result, "Dispatch trip created from the selected day's orders."));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPost("/trips/{id:guid}/route-change-requests", async (
            Guid id,
            CreateRouteChangeRequestInput input,
            ClaimsPrincipal user,
            HttpContext context,
            ITripPlanningService svc,
            CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-ID"].FirstOrDefault(), out var parsed) ? parsed : (Guid?)null;
            var proposal = await svc.CreateRouteChangeProposalAsync(id, actorId.Value, input, correlationId, ct);
            return Results.Created($"/api/v1/route-plan-versions/{proposal.Id}", ApiResponse<RoutePlanVersionDto>.Success(proposal, "Emergency route change proposed as a new immutable version."));
        }).RequireAuthorization("PlanningLogistics");

        // ==========================================
        // Route Plans & Versions (T10)
        // ==========================================
        api.MapPost("/trips/{id:guid}/route-plans", async (Guid id, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var plan = await svc.CreateRoutePlanAsync(id, actorId.Value, ct);
            return Results.Created($"/api/v1/route-plans/{plan.Id}", ApiResponse<RoutePlanDto>.Success(plan, "Route plan created successfully"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPost("/route-plans/{id:guid}/versions", async (Guid id, CreateRoutePlanVersionInput input, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var version = await svc.CreateNewVersionAsync(id, actorId.Value, input.Reason, ct);
            return Results.Created($"/api/v1/route-plan-versions/{version.Id}", ApiResponse<RoutePlanVersionDto>.Success(version, "New route version created"));
        }).RequireAuthorization("PlanningLogistics");

        // ==========================================
        // Route Legs (T10)
        // ==========================================
        api.MapPost("/route-plan-versions/{id:guid}/legs", async (Guid id, RouteLegInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var leg = await svc.AddLegAsync(id, input, ct);
            return Results.Created($"/api/v1/route-legs/{leg.Id}", ApiResponse<RouteLegDto>.Success(leg, "Route leg added successfully"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPut("/route-legs/{id:guid}", async (Guid id, RouteLegInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RouteLegDto>.Success(await svc.UpdateLegAsync(id, input, ct), "Route leg updated successfully"))).RequireAuthorization("PlanningLogistics");

        api.MapPut("/route-legs/{id:guid}/shape", async (Guid id, RouteShapeInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<RouteLegDto>.Success(await svc.UpdateRouteShapeAsync(id, input, ct), "Route shape saved"))).RequireAuthorization("PlanningLogistics");

        api.MapDelete("/route-legs/{id:guid}", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
        {
            await svc.DeleteLegAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization("PlanningLogistics");

        // ==========================================
        // Checkpoints (T10)
        // ==========================================
        api.MapPost("/route-legs/{id:guid}/checkpoints", async (Guid id, CheckpointInput input, ITripPlanningService svc, CancellationToken ct) =>
        {
            var cp = await svc.AddCheckpointAsync(id, input, ct);
            return Results.Created($"/api/v1/checkpoints/{cp.Id}", ApiResponse<CheckpointDto>.Success(cp, "Checkpoint added successfully"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPut("/checkpoints/{id:guid}", async (Guid id, CheckpointInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<CheckpointDto>.Success(await svc.UpdateCheckpointAsync(id, input, ct), "Checkpoint updated successfully"))).RequireAuthorization("PlanningLogistics");

        api.MapDelete("/checkpoints/{id:guid}", async (Guid id, ITripPlanningService svc, CancellationToken ct) =>
        {
            await svc.DeleteCheckpointAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization("PlanningLogistics");

        // ==========================================
        // Assignments (T10)
        // ==========================================
        api.MapPost("/route-legs/{id:guid}/assignments/resource", async (Guid id, ResourceInput input, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var assignment = await svc.AssignResourceAsync(id, input with { AssignedByUserId = actorId.Value }, ct);
            return Results.Created("", ApiResponse<ResourceAssignmentDto>.Success(assignment, "Resource assigned successfully"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPost("/trips/{id:guid}/staff-assignments", async (Guid id, StaffInput input, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var canOverrideArea = user.IsInRole("ADMIN") || user.IsInRole("LOGISTICS_MANAGER");
            if (!string.IsNullOrWhiteSpace(input.OutsideAreaOverrideReason) && !canOverrideArea) return Results.Forbid();
            var assigned = await svc.AssignStaffAsync(id, input, ct, actorId.Value, canOverrideArea);
            return Results.Created("", ApiResponse<StaffAssignmentDto>.Success(assigned, "Staff assigned successfully"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPost("/trips/{id:guid}/horse-assignments", async (Guid id, HorseInput input, ITripPlanningService svc, CancellationToken ct) =>
            Results.Created("", ApiResponse<HorseAssignmentDto>.Success(await svc.AssignHorseAsync(id, input, ct), "Horse assigned successfully"))).RequireAuthorization("PlanningLogistics");

        // ==========================================
        // Route Version Lifecycle Transitions (T10)
        // ==========================================
        api.MapPost("/route-plan-versions/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            return Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.SubmitRouteVersionAsync(id, actorId.Value, ct), "Route version submitted for approval"));
        }).RequireAuthorization("PlanningLogistics");

        api.MapPost("/route-plan-versions/{id:guid}/approve", async (Guid id, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            return Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.ApproveRouteVersionAsync(id, actorId.Value, ct), "Route version approved"));
        }).RequireAuthorization("PlanningApproval");

        api.MapPost("/route-plan-versions/{id:guid}/reject", async (Guid id, RejectRouteChangeInput input, ClaimsPrincipal user, HttpContext context, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            var correlationId = Guid.TryParse(context.Request.Headers["X-Correlation-ID"].FirstOrDefault(), out var parsed) ? parsed : (Guid?)null;
            return Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.RejectRouteVersionAsync(id, actorId.Value, input.Reason, correlationId, ct), "Route change rejected."));
        }).RequireAuthorization("PlanningApproval");

        api.MapPost("/route-plan-versions/{id:guid}/activate", async (Guid id, ClaimsPrincipal user, ITripPlanningService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            return Results.Ok(ApiResponse<RoutePlanVersionDto>.Success(await svc.ActivateRouteVersionAsync(id, actorId.Value, ct), "Route version activated"));
        }).RequireAuthorization("PlanningApproval");

        // ==========================================
        // Readiness & Pre-departure (T11)
        // ==========================================
        api.MapPost("/trips/{id:guid}/confirm-ready", async (Guid id, ClaimsPrincipal user, ITripReadinessService svc, CancellationToken ct) =>
        {
            var actorId = GetActorId(user);
            if (actorId is null) return Results.Unauthorized();
            return Results.Ok(ApiResponse<TripDto>.Success(await svc.ConfirmReadyAsync(id, actorId.Value, ct), "Trip confirmed READY for departure"));
        }).RequireAuthorization("PlanningLogistics");

        return api;
    }

    private static Guid? GetActorId(ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var actorId) ? actorId : null;
    }
}
