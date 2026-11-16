using System.Text.Json;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Domain;

namespace Planning.Application.Services;

public class TripReadinessService(IPlanningDbContext db, ITripComplianceEvaluator? complianceEvaluator = null) : ITripReadinessService
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
            trip.VersionNo,
            trip.CustomPickupLatitude,
            trip.CustomPickupLongitude,
            trip.CustomPickupLabel,
            trip.OriginLocationId,
            trip.DestinationLocationId
        );
    }

    public async Task<TripReadinessResponse> EvaluateReadinessAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        var blockers = new List<string>();
        var activeLegs = new List<RouteLeg>();

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
            activeLegs = legs;

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

                var flightIds = await db.TripLegAssignments.AsNoTracking()
                    .Where(assignment => legs.Select(leg => leg.Id).Contains(assignment.RouteLegId) && assignment.FlightBookingId.HasValue)
                    .Select(assignment => assignment.FlightBookingId!.Value).ToListAsync(ct);
                if (flightIds.Count > 0 && await db.FlightBookings.AsNoTracking()
                    .AnyAsync(flight => flightIds.Contains(flight.Id) && flight.ConfirmationStatus != "CONFIRMED", ct))
                    blockers.Add("One or more route legs use a flight booking that has not been manually confirmed.");
            }
        }

        // 3. Driver and escort assignments must belong to the active route version.
        var activeLegIds = activeLegs.Select(leg => leg.Id).ToHashSet();
        var assignedDriverLegIds = activeLegIds.Count == 0 ? new HashSet<Guid>() : (await db.StaffAssignments.AsNoTracking()
            .Where(assignment => assignment.TripId == tripId && assignment.Role == PlanningConstants.StaffRole.Driver &&
                assignment.ReleasedAt == null && assignment.RouteLegId.HasValue && activeLegIds.Contains(assignment.RouteLegId.Value))
            .Select(assignment => assignment.RouteLegId!.Value)
            .Distinct().ToListAsync(ct)).ToHashSet();
        foreach (var roadLeg in activeLegs.Where(leg => string.Equals(leg.TransportMode, PlanningConstants.TransportMode.Road, StringComparison.OrdinalIgnoreCase)))
            if (!assignedDriverLegIds.Contains(roadLeg.Id)) blockers.Add($"Driver not assigned to road leg {roadLeg.SequenceNo}.");

        var assignedEscortLegIds = activeLegIds.Count == 0 ? new HashSet<Guid>() : (await db.StaffAssignments.AsNoTracking()
            .Where(assignment => assignment.TripId == tripId && assignment.Role == PlanningConstants.StaffRole.Escort &&
                assignment.ReleasedAt == null && assignment.RouteLegId.HasValue && activeLegIds.Contains(assignment.RouteLegId.Value))
            .Select(assignment => assignment.RouteLegId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
        var roadLegs = activeLegs.Where(leg => string.Equals(leg.TransportMode, PlanningConstants.TransportMode.Road, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (roadLegs.Length > 0)
        {
            foreach (var roadLeg in roadLegs.Where(leg => !assignedEscortLegIds.Contains(leg.Id)))
                blockers.Add($"Escort not assigned to road leg {roadLeg.SequenceNo}.");
        }
        else if (assignedEscortLegIds.Count == 0)
        {
            blockers.Add("Chưa assign escort");
        }

        // 4. Horse stall assignment check
        var horseAssignments = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId && activeLegIds.Contains(h.RouteLegId))
            .ToListAsync(ct);

        if (horseAssignments.Count == 0)
        {
            blockers.Add("Chưa assign horse cho trip");
        }

        var requestedHorseIds = (await db.TripOrders.AsNoTracking().Where(order => order.TripId == tripId)
            .Select(order => order.HorseIdsJson).ToListAsync(ct))
            .SelectMany(value => { try { return JsonSerializer.Deserialize<Guid[]>(value) ?? []; } catch (JsonException) { return []; } })
            .Distinct().ToHashSet();
        var assignedHorseLegPairs = horseAssignments.Select(assignment => (assignment.HorseId, assignment.RouteLegId)).ToHashSet();
        var missingHorseLegPairs = (from horseId in requestedHorseIds
                                    from leg in activeLegs
                                    where !assignedHorseLegPairs.Contains((horseId, leg.Id))
                                    select (HorseId: horseId, LegId: leg.Id, leg.SequenceNo)).ToArray();
        if (missingHorseLegPairs.Length > 0)
            blockers.Add($"Assign each requested horse to every active route leg it travels; {missingHorseLegPairs.Length} horse/leg assignment(s) are missing.");

        foreach (var h in horseAssignments.Where(h => h.StallId is null))
        {
            blockers.Add($"Horse {h.HorseId} chưa có stall");
        }

        // 5. Compliance is re-evaluated from the Compliance service. The persisted flag is only a projection.
        if (complianceEvaluator is null)
        {
            if (await db.TransportTrips.AsNoTracking().AnyAsync(member => (member.Id == tripId || member.MergedIntoTripId == tripId) && !member.ComplianceReady, ct))
                blockers.Add("Compliance chưa ready for one or more included orders");
        }
        else
        {
            var compliance = await complianceEvaluator.EvaluateAsync(tripId, ct);
            var trackedTrip = await db.TransportTrips.FirstOrDefaultAsync(member => member.Id == tripId, ct);
            if (trackedTrip is not null && trackedTrip.ComplianceReady != compliance.IsReady)
            {
                trackedTrip.ComplianceReady = compliance.IsReady;
                trackedTrip.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            if (!compliance.IsReady)
                blockers.AddRange(compliance.Blockers.Count > 0 ? compliance.Blockers : ["Compliance requirements are not met."]);
        }

        var unresolvedSevereIncidents = await db.TripIncidentFlags.AsNoTracking()
            .CountAsync(incident => incident.TripId == tripId &&
                (incident.Severity == "HIGH" || incident.Severity == "CRITICAL") &&
                (incident.Status == "OPEN" || incident.Status == "INVESTIGATING"), ct);
        if (unresolvedSevereIncidents > 0)
            blockers.Add($"Resolve {unresolvedSevereIncidents} open high-severity incident(s) before marking the trip ready.");

        return new TripReadinessResponse(tripId, blockers.Count == 0, blockers);
    }

    public async Task<TripDto> ConfirmReadyAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default)
    {
        await using var transaction = await db.BeginTransactionAsync(ct);
        var trip = await db.GetTripForUpdateAsync(tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);
        if (trip.CancellationPending)
            throw new BusinessRuleException("This order is being cancelled. Trip readiness is temporarily locked.", "ORDER_CANCELLATION_PENDING", "status");

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

        if (trip.Status == PlanningConstants.TripStatus.Ready)
        {
            if (transaction is not null) await transaction.CommitAsync(ct);
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
        if (transaction is not null) await transaction.CommitAsync(ct);
        return MapTrip(trip);
    }

    private static TripDto MapTrip(TransportTrip t) =>
        new(t.Id, t.TripNo, t.OrderId, t.Status, t.PlannedDepartureAt, t.PlannedArrivalAt, t.ComplianceReady, t.CreatedAt, t.UpdatedAt, t.VersionNo);
}
