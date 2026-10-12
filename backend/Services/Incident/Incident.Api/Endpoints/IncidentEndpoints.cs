using System.Security.Claims;
using Incident.Application.DTOs;
using Incident.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Incident.Api.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/incidents")
            .WithTags("Incidents");

        group.MapPost("/", async (
            [FromBody] CreateIncidentRequest request,
            IIncidentService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.CreateIncidentAsync(request, userId, ct);
            return Results.Created($"/api/v1/incidents/{result.Id}", result);
        })
        .WithName("CreateIncident")
        .WithSummary("Report a new transport incident");

        group.MapGet("/", async (
            [AsParameters] IncidentFilterDto filter,
            IIncidentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetIncidentsAsync(filter, ct);
            return Results.Ok(result);
        })
        .WithName("GetIncidents")
        .WithSummary("List and filter incidents");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IIncidentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetIncidentByIdAsync(id, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("GetIncidentById")
        .WithSummary("Get incident details by ID");

        group.MapGet("/trips/{tripId:guid}", async (
            Guid tripId,
            IIncidentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetIncidentsByTripIdAsync(tripId, ct);
            return Results.Ok(result);
        })
        .WithName("GetIncidentsByTripId")
        .WithSummary("Get all incidents for a specific transport trip");

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] UpdateIncidentStatusRequest request,
            IIncidentService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.UpdateStatusAsync(id, request, userId, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateIncidentStatus")
        .WithSummary("Update incident status");

        group.MapPost("/{id:guid}/resolve", async (
            Guid id,
            [FromBody] ResolveIncidentRequest request,
            IIncidentService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var result = await service.ResolveIncidentAsync(id, request, userId, ct);
            return Results.Ok(result);
        })
        .WithName("ResolveIncident")
        .WithSummary("Resolve incident with resolution notes");

        group.MapPost("/{id:guid}/attachments", async (
            Guid id,
            IFormFile file,
            IIncidentService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
                return Results.BadRequest("A valid file must be provided.");

            var userId = GetUserId(user);
            using var stream = file.OpenReadStream();
            var result = await service.AddAttachmentAsync(
                id,
                file.FileName,
                file.ContentType,
                stream,
                userId,
                ct);

            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithName("UploadIncidentAttachment")
        .WithSummary("Upload photo or document attachment for an incident");

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? user.FindFirstValue("userId");

        if (Guid.TryParse(sub, out var parsed))
        {
            return parsed;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
