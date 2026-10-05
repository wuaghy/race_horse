using System.Text.Json;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Domain;

namespace Planning.Application.Services;

public class TripReadinessService(IPlanningDbContext db) : ITripReadinessService
{
    public async Task<TripDto> SetComplianceReadyAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        trip.ComplianceReady = true;
        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new TripDto(
            trip.Id,
            trip.TripNo,
            trip.OrderId,
            trip.Status,
            trip.PlannedDepartureAt,
            trip.PlannedArrivalAt,
            trip.ComplianceReady,
            trip.CreatedAt,
            trip.UpdatedAt,
            trip.VersionNo
        );
    }

    public async Task<TripReadinessResponse> EvaluateReadinessAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        var blockers = new List<string>();

        // 1. Active route version check
        var plan = await db.RoutePlans.AsNoTracking().FirstOrDefaultAsync(p => p.TripId == tripId, ct);
        RoutePlanVersion? activeVersion = null;

        if (plan?.ActiveVersionId != null)
        {
            activeVersion = await db.RoutePlanVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == plan.ActiveVersionId && v.Status == PlanningConstants.RouteVersionStatus.Active, ct);
        }

        if (activeVersion is null)
        {
            blockers.Add("Route version chưa ACTIVE");
        }
        else
        {
            // 2. Resource assignment check for all legs in active version
            var legs = await db.RouteLegs.AsNoTracking()
                .Where(l => l.RoutePlanVersionId == activeVersion.Id)
                .ToListAsync(ct);

            if (legs.Count == 0)
            {
                blockers.Add("Route version chưa có route leg nào");
            }
            else
            {
                var assignedLegIds = await db.TripLegAssignments.AsNoTracking()
                    .Where(a => legs.Select(l => l.Id).Contains(a.RouteLegId))
                    .Select(a => a.RouteLegId)
                    .Distinct()
                    .ToListAsync(ct);

                if (legs.Any(l => !assignedLegIds.Contains(l.Id)))
                {
                    blockers.Add("Chưa assign vehicle hoặc flight");
                }
            }
        }

        // 3. Escort staff assignment check
        var hasEscort = await db.StaffAssignments.AsNoTracking()
            .AnyAsync(s => s.TripId == tripId && s.Role == PlanningConstants.StaffRole.Escort, ct);

        if (!hasEscort)
        {
            blockers.Add("Chưa assign escort");
        }

        // 4. Horse stall assignment check
        var horseAssignments = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId)
            .ToListAsync(ct);

        if (horseAssignments.Count == 0)
        {
            blockers.Add("Chưa assign horse cho trip");
        }

        foreach (var h in horseAssignments.Where(h => h.StallId is null))
        {
            blockers.Add($"Horse {h.HorseId} chưa có stall");
        }

        // 5. Compliance check
        if (!trip.ComplianceReady)
        {
            blockers.Add("Compliance chưa ready");
        }

        return new TripReadinessResponse(tripId, blockers.Count == 0, blockers);
    }

    public async Task<TripDto> ConfirmReadyAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default)
    {
        var readiness = await EvaluateReadinessAsync(tripId, ct);
        if (!readiness.Ready)
        {
            var errors = readiness.Blockers
                .Select(b => new ApiError { Field = "blockers", Reason = "TRIP_NOT_READY", Message = b })
                .ToList();

            throw new AppException(
                "Trip is not ready. Unresolved readiness blockers remain.",
                statusCode: 422,
                errorCode: 42200,
                errors: errors
            );
        }

        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        if (trip.Status == PlanningConstants.TripStatus.Ready)
        {
            return MapTrip(trip);
        }

        var oldState = trip.Status;
        trip.Status = PlanningConstants.TripStatus.Ready;
        trip.UpdatedAt = DateTime.UtcNow;
        trip.VersionNo++;

        // Audit Log
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "TransportTrip",
            EntityId = tripId,
            Action = "CONFIRM_READY",
            OldState = oldState,
            NewState = PlanningConstants.TripStatus.Ready,
            ActorUserId = actorUserId,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        });

        // Outbox Event
        var payload = JsonSerializer.Serialize(new
        {
            tripId = trip.Id,
            tripNo = trip.TripNo,
            orderId = trip.OrderId,
            status = trip.Status
        });

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "Planning.TripReady",
            AggregateType = "TransportTrip",
            AggregateId = trip.Id,
            Payload = payload,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        });

        await db.SaveChangesAsync(ct);
        return MapTrip(trip);
    }

    private static TripDto MapTrip(TransportTrip t) =>
        new(t.Id, t.TripNo, t.OrderId, t.Status, t.PlannedDepartureAt, t.PlannedArrivalAt, t.ComplianceReady, t.CreatedAt, t.UpdatedAt, t.VersionNo);
}
