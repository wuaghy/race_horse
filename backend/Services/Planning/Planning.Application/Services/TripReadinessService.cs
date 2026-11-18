using System.Text.Json;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Domain;

namespace Planning.Application.Services;

public class TripReadinessService(
    IPlanningDbContext db,
    IComplianceVerificationClient? complianceClient = null,
    ILogger<TripReadinessService>? logger = null,
    Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null) : ITripReadinessService
{
    public async Task<TripDto> SetComplianceReadyAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        trip.ComplianceReady = true;
        trip.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return MapTrip(trip);
    }

    public async Task<ComplianceTripQuery> BuildComplianceQueryAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        Guid originCountryId = Guid.Empty;
        Guid destinationCountryId = Guid.Empty;

        var plan = await db.RoutePlans.AsNoTracking().FirstOrDefaultAsync(p => p.TripId == tripId, ct);
        if (plan?.ActiveVersionId != null)
        {
            var legs = await db.RouteLegs.AsNoTracking()
                .Where(l => l.RoutePlanVersionId == plan.ActiveVersionId.Value)
                .OrderBy(l => l.SequenceNo)
                .ToListAsync(ct);

            if (legs.Count > 0)
            {
                var locIds = new[] { legs[0].OriginLocationId, legs[^1].DestinationLocationId };
                var locs = await db.Locations.AsNoTracking()
                    .Where(l => locIds.Contains(l.Id))
                    .ToListAsync(ct);

                originCountryId = locs.FirstOrDefault(l => l.Id == legs[0].OriginLocationId)?.CountryId ?? Guid.Empty;
                destinationCountryId = locs.FirstOrDefault(l => l.Id == legs[^1].DestinationLocationId)?.CountryId ?? Guid.Empty;
            }
        }

        var horseIds = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId)
            .Select(h => h.HorseId)
            .Distinct()
            .ToListAsync(ct);

        DateOnly? departureDate = trip.PlannedDepartureAt.HasValue
            ? DateOnly.FromDateTime(trip.PlannedDepartureAt.Value)
            : null;

        return new ComplianceTripQuery(tripId, originCountryId, destinationCountryId, horseIds, departureDate);
    }

    public async Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(Guid tripId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        var query = await BuildComplianceQueryAsync(tripId, ct);

        // Fast-reject locally if trip lacks active route or assigned horses without dispatching blank GUIDs over gRPC wire (SPEC-002 L21)
        var localBlockers = new List<string>();
        if (query.OriginCountryId == Guid.Empty || query.DestinationCountryId == Guid.Empty)
        {
            localBlockers.Add("Trip has no active route plan or undefined route origin/destination.");
        }
        if (query.HorseIds.Count == 0)
        {
            localBlockers.Add("Trip has no horses assigned.");
        }

        if (localBlockers.Count > 0 || complianceClient == null)
        {
            return new TripComplianceVerificationDto(
                tripId,
                IsCompliant: false,
                TotalRequired: 0,
                TotalMet: 0,
                TotalMissing: 0,
                TotalExpired: 0,
                BlockerMessages: localBlockers,
                CheckItems: Array.Empty<ComplianceCheckItemDto>(),
                EvaluatedAt: DateTime.UtcNow
            );
        }

        return await complianceClient.VerifyTripComplianceAsync(query, ct);
    }

    public async Task<TripReadinessResponse> EvaluateReadinessAsync(Guid tripId, CancellationToken ct = default)
    {
        var (readiness, _, _) = await EvaluateReadinessInternalAsync(tripId, ct);
        return readiness;
    }

    private async Task<(TripReadinessResponse Readiness, bool ComplianceViolation, TripComplianceVerificationDto? ComplianceResult)> EvaluateReadinessInternalAsync(Guid tripId, CancellationToken ct = default)
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

        List<RouteLeg> legs = [];
        if (activeVersion is null)
        {
            blockers.Add("Route version chưa ACTIVE");
        }
        else
        {
            // 2. Resource assignment check for all legs in active version
            legs = await db.RouteLegs.AsNoTracking()
                .Where(l => l.RoutePlanVersionId == activeVersion.Id)
                .OrderBy(l => l.SequenceNo)
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

        // 5. Compliance check (pure read-only via gRPC pre-flight gate with graceful degradation)
        bool complianceViolation = false;
        TripComplianceVerificationDto? complianceResult = null;

        if (activeVersion != null && legs.Count > 0 && horseAssignments.Count > 0)
        {
            var query = await BuildComplianceQueryAsync(tripId, ct);

            if (query.OriginCountryId != Guid.Empty && query.DestinationCountryId != Guid.Empty && query.HorseIds.Count > 0)
            {
                bool verifiedViaGrpc = false;

                if (complianceClient != null)
                {
                    try
                    {
                        complianceResult = await complianceClient.VerifyTripComplianceAsync(query, ct);
                        verifiedViaGrpc = true;

                        if (!complianceResult.IsCompliant)
                        {
                            complianceViolation = true;
                            if (complianceResult.BlockerMessages.Count > 0)
                            {
                                blockers.AddRange(complianceResult.BlockerMessages);
                            }
                            else
                            {
                                blockers.Add("Compliance chưa ready");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "gRPC compliance verification failed for TripId {TripId}. Falling back to cached local state.", tripId);
                    }
                }

                if (!verifiedViaGrpc)
                {
                    if (!trip.ComplianceReady)
                    {
                        blockers.Add("Compliance chưa ready");
                    }
                }
            }
            else
            {
                if (!trip.ComplianceReady)
                {
                    blockers.Add("Compliance chưa ready");
                }
            }
        }
        else
        {
            // Route version, legs, or horses missing: do not query Compliance with blank data
            if (!trip.ComplianceReady)
            {
                blockers.Add("Compliance chưa ready");
            }
        }

        return (new TripReadinessResponse(tripId, blockers.Count == 0, blockers), complianceViolation, complianceResult);
    }

    public async Task<TripDto> ConfirmReadyAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default)
    {
        var (readiness, complianceViolation, complianceResult) = await EvaluateReadinessInternalAsync(tripId, ct);
        if (!readiness.Ready)
        {
            // Rigorous cache invalidation: if gRPC reported compliance violation, invalidate local ComplianceReady flag
            if (complianceViolation)
            {
                var unreadyTrip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct);
                if (unreadyTrip != null && unreadyTrip.ComplianceReady)
                {
                    unreadyTrip.ComplianceReady = false;
                    unreadyTrip.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }

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
        trip.ComplianceReady = true;
        trip.UpdatedAt = DateTime.UtcNow;
        trip.VersionNo++;

        // Audit Log with compliance telemetry snapshot in Payload
        var complianceSummary = complianceResult != null
            ? $"gRPC verified: Compliant={complianceResult.IsCompliant}, Met={complianceResult.TotalMet}/{complianceResult.TotalRequired}, Missing={complianceResult.TotalMissing}"
            : (trip.ComplianceReady ? "Compliance verified via cached state." : "Compliance verified.");

        var auditPayload = JsonSerializer.Serialize(new
        {
            complianceVerified = complianceResult != null,
            isCompliant = complianceResult?.IsCompliant ?? trip.ComplianceReady,
            totalRequired = complianceResult?.TotalRequired ?? 0,
            totalMet = complianceResult?.TotalMet ?? 0,
            totalMissing = complianceResult?.TotalMissing ?? 0,
            totalExpired = complianceResult?.TotalExpired ?? 0,
            evaluatedAt = complianceResult?.EvaluatedAt ?? DateTime.UtcNow,
            summary = complianceSummary
        });

        var correlationId = Guid.TryParse(httpContextAccessor?.HttpContext?.Items["X-Correlation-ID"]?.ToString(), out var parsedCid)
            ? parsedCid
            : Guid.NewGuid();

        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "TransportTrip",
            EntityId = tripId,
            Action = "CONFIRM_READY",
            OldState = oldState,
            NewState = PlanningConstants.TripStatus.Ready,
            ActorUserId = actorUserId,
            Reason = complianceSummary,
            Payload = auditPayload,
            CorrelationId = correlationId,
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
            CorrelationId = correlationId,
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        });

        await db.SaveChangesAsync(ct);
        return MapTrip(trip);
    }

    private static TripDto MapTrip(TransportTrip t) =>
        new(t.Id, t.TripNo, t.OrderId, t.Status, t.PlannedDepartureAt, t.PlannedArrivalAt, t.ComplianceReady, t.CreatedAt, t.UpdatedAt, t.VersionNo);
}
