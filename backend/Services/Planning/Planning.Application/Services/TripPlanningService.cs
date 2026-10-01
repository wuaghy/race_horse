using System.Text.Json;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain;

namespace Planning.Application.Services;

public class TripPlanningService(IPlanningDbContext db) : ITripPlanningService
{
    // ==========================================
    // Trips
    // ==========================================
    public async Task<TripDto> CreateTripAsync(CreateTripInput input, CancellationToken ct = default)
    {
        var existing = await db.TransportTrips.FirstOrDefaultAsync(t => t.OrderId == input.OrderId, ct);
        if (existing is not null)
        {
            return MapTrip(existing);
        }

        var count = await db.TransportTrips.CountAsync(ct);
        var tripNo = $"TRP-{DateTime.UtcNow:yyyyMMdd}-{count + 1:000}";

        var trip = new TransportTrip
        {
            TripNo = tripNo,
            OrderId = input.OrderId,
            Status = PlanningConstants.TripStatus.Planned,
            ComplianceReady = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        db.TransportTrips.Add(trip);

        var payload = JsonSerializer.Serialize(new
        {
            tripId = trip.Id,
            orderId = trip.OrderId,
            tripNo = trip.TripNo
        });

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "Planning.TripCreated",
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

    public async Task<TripDto> GetTripByIdAsync(Guid id, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Trip", id);
        return MapTrip(trip);
    }

    // ==========================================
    // Route Plans & Versions
    // ==========================================
    public async Task<RoutePlanDto> CreateRoutePlanAsync(Guid tripId, Guid actorUserId, CancellationToken ct = default)
    {
        var trip = await db.TransportTrips.FirstOrDefaultAsync(t => t.Id == tripId, ct)
            ?? throw new NotFoundException("Trip", tripId);

        if (await db.RoutePlans.AnyAsync(p => p.TripId == tripId, ct))
            throw new BusinessRuleException("Trip already has a route plan.", "ROUTE_PLAN_ALREADY_EXISTS");

        var plan = new RoutePlan
        {
            TripId = tripId,
            ActiveVersionId = null,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        db.RoutePlans.Add(plan);

        var version = new RoutePlanVersion
        {
            RoutePlanId = plan.Id,
            VersionNo = 1,
            Status = PlanningConstants.RouteVersionStatus.Draft,
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.RoutePlanVersions.Add(version);
        await db.SaveChangesAsync(ct);

        return MapPlan(plan);
    }

    public async Task<IReadOnlyList<RoutePlanVersionDto>> GetVersionsAsync(Guid planId, CancellationToken ct = default)
    {
        if (!await db.RoutePlans.AnyAsync(p => p.Id == planId, ct))
            throw new NotFoundException("RoutePlan", planId);

        return await db.RoutePlanVersions.AsNoTracking()
            .Where(v => v.RoutePlanId == planId)
            .OrderBy(v => v.VersionNo)
            .Select(v => MapVersion(v))
            .ToListAsync(ct);
    }

    public async Task<RoutePlanVersionDto> CreateNewVersionAsync(Guid planId, Guid actorUserId, string? reason = null, CancellationToken ct = default)
    {
        var plan = await db.RoutePlans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new NotFoundException("RoutePlan", planId);

        var latestVersionNo = await db.RoutePlanVersions
            .Where(v => v.RoutePlanId == planId)
            .MaxAsync(v => (int?)v.VersionNo, ct) ?? 0;

        var newVersion = new RoutePlanVersion
        {
            RoutePlanId = planId,
            VersionNo = latestVersionNo + 1,
            Status = PlanningConstants.RouteVersionStatus.Draft,
            Reason = reason?.Trim(),
            CreatedByUserId = actorUserId,
            CreatedAt = DateTime.UtcNow
        };

        db.RoutePlanVersions.Add(newVersion);
        await db.SaveChangesAsync(ct);

        return MapVersion(newVersion);
    }

    // ==========================================
    // Route Legs
    // ==========================================
    public async Task<IReadOnlyList<RouteLegDto>> GetLegsAsync(Guid versionId, CancellationToken ct = default)
    {
        if (!await db.RoutePlanVersions.AnyAsync(v => v.Id == versionId, ct))
            throw new NotFoundException("RoutePlanVersion", versionId);

        return await db.RouteLegs.AsNoTracking()
            .Where(l => l.RoutePlanVersionId == versionId)
            .OrderBy(l => l.SequenceNo)
            .Select(l => MapLeg(l))
            .ToListAsync(ct);
    }

    public async Task<RouteLegDto> AddLegAsync(Guid versionId, RouteLegInput input, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        AssertVersionIsDraft(version);
        await ValidateLegLocationsAndTimesAsync(input, ct);

        if (await db.RouteLegs.AnyAsync(l => l.RoutePlanVersionId == versionId && l.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Route leg sequence '{input.SequenceNo}' already exists in this version.", "DUPLICATE_SEQUENCE_NO", "sequenceNo");

        var leg = new RouteLeg
        {
            RoutePlanVersionId = versionId,
            SequenceNo = input.SequenceNo,
            OriginLocationId = input.OriginLocationId,
            DestinationLocationId = input.DestinationLocationId,
            TransportMode = input.Mode.Trim().ToUpperInvariant(),
            PlannedDepartureAt = input.DepartureAt,
            PlannedArrivalAt = input.ArrivalAt,
            DistanceKm = input.DistanceKm,
            Status = "PLANNED"
        };

        db.RouteLegs.Add(leg);
        await db.SaveChangesAsync(ct);

        return MapLeg(leg);
    }

    public async Task<RouteLegDto> UpdateLegAsync(Guid legId, RouteLegInput input, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);
        await ValidateLegLocationsAndTimesAsync(input, ct);

        if (await db.RouteLegs.AnyAsync(l => l.Id != legId && l.RoutePlanVersionId == leg.RoutePlanVersionId && l.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Route leg sequence '{input.SequenceNo}' already exists in this version.", "DUPLICATE_SEQUENCE_NO", "sequenceNo");

        leg.SequenceNo = input.SequenceNo;
        leg.OriginLocationId = input.OriginLocationId;
        leg.DestinationLocationId = input.DestinationLocationId;
        leg.TransportMode = input.Mode.Trim().ToUpperInvariant();
        leg.PlannedDepartureAt = input.DepartureAt;
        leg.PlannedArrivalAt = input.ArrivalAt;
        leg.DistanceKm = input.DistanceKm;

        await db.SaveChangesAsync(ct);
        return MapLeg(leg);
    }

    public async Task DeleteLegAsync(Guid legId, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (await db.Checkpoints.AnyAsync(c => c.RouteLegId == legId, ct) ||
            await db.TripLegAssignments.AnyAsync(a => a.RouteLegId == legId, ct) ||
            await db.HorseLegAssignments.AnyAsync(h => h.RouteLegId == legId, ct))
        {
            throw new BusinessRuleException("Cannot delete route leg because it has dependent checkpoints or assignments.", "LEG_IN_USE");
        }

        db.RouteLegs.Remove(leg);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Checkpoints
    // ==========================================
    public async Task<IReadOnlyList<CheckpointDto>> GetCheckpointsAsync(Guid legId, CancellationToken ct = default)
    {
        if (!await db.RouteLegs.AnyAsync(l => l.Id == legId, ct))
            throw new NotFoundException("RouteLeg", legId);

        return await db.Checkpoints.AsNoTracking()
            .Where(c => c.RouteLegId == legId)
            .OrderBy(c => c.SequenceNo)
            .Select(c => MapCheckpoint(c))
            .ToListAsync(ct);
    }

    public async Task<CheckpointDto> AddCheckpointAsync(Guid legId, CheckpointInput input, CancellationToken ct = default)
    {
        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (!await db.Locations.AnyAsync(loc => loc.Id == input.LocationId, ct))
            throw new NotFoundException("Location", input.LocationId);

        if (input.DepartureAt < input.ArrivalAt)
            throw new BusinessRuleException("Checkpoint departure time cannot be earlier than arrival time.", "INVALID_CHECKPOINT_TIMES");

        if (await db.Checkpoints.AnyAsync(c => c.RouteLegId == legId && c.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Checkpoint sequence '{input.SequenceNo}' already exists in this leg.", "DUPLICATE_CHECKPOINT_SEQUENCE");

        var checkpoint = new Checkpoint
        {
            RouteLegId = legId,
            SequenceNo = input.SequenceNo,
            LocationId = input.LocationId,
            Type = input.Type.Trim().ToUpperInvariant(),
            PlannedArrivalAt = input.ArrivalAt,
            PlannedDepartureAt = input.DepartureAt,
            Status = "PLANNED"
        };

        db.Checkpoints.Add(checkpoint);
        await db.SaveChangesAsync(ct);

        return MapCheckpoint(checkpoint);
    }

    public async Task<CheckpointDto> UpdateCheckpointAsync(Guid checkpointId, CheckpointInput input, CancellationToken ct = default)
    {
        var checkpoint = await db.Checkpoints.FirstOrDefaultAsync(c => c.Id == checkpointId, ct)
            ?? throw new NotFoundException("Checkpoint", checkpointId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == checkpoint.RouteLegId, ct)
            ?? throw new NotFoundException("RouteLeg", checkpoint.RouteLegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (!await db.Locations.AnyAsync(loc => loc.Id == input.LocationId, ct))
            throw new NotFoundException("Location", input.LocationId);

        if (input.DepartureAt < input.ArrivalAt)
            throw new BusinessRuleException("Checkpoint departure time cannot be earlier than arrival time.", "INVALID_CHECKPOINT_TIMES");

        if (await db.Checkpoints.AnyAsync(c => c.Id != checkpointId && c.RouteLegId == checkpoint.RouteLegId && c.SequenceNo == input.SequenceNo, ct))
            throw new BusinessRuleException($"Checkpoint sequence '{input.SequenceNo}' already exists in this leg.", "DUPLICATE_CHECKPOINT_SEQUENCE");

        checkpoint.SequenceNo = input.SequenceNo;
        checkpoint.LocationId = input.LocationId;
        checkpoint.Type = input.Type.Trim().ToUpperInvariant();
        checkpoint.PlannedArrivalAt = input.ArrivalAt;
        checkpoint.PlannedDepartureAt = input.DepartureAt;

        await db.SaveChangesAsync(ct);
        return MapCheckpoint(checkpoint);
    }

    public async Task DeleteCheckpointAsync(Guid checkpointId, CancellationToken ct = default)
    {
        var checkpoint = await db.Checkpoints.FirstOrDefaultAsync(c => c.Id == checkpointId, ct)
            ?? throw new NotFoundException("Checkpoint", checkpointId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == checkpoint.RouteLegId, ct)
            ?? throw new NotFoundException("RouteLeg", checkpoint.RouteLegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        db.Checkpoints.Remove(checkpoint);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Assignments (Resource, Staff, Horse)
    // ==========================================
    public async Task<ResourceAssignmentDto> AssignResourceAsync(Guid legId, ResourceInput input, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if ((input.VehicleId is null) == (input.FlightBookingId is null))
            throw new BusinessRuleException("Must provide exactly one of vehicleId or flightBookingId.", "INVALID_RESOURCE_EXCLUSIVE");

        if (input.VehicleId.HasValue)
        {
            if (leg.TransportMode != PlanningConstants.TransportMode.Road)
                throw new BusinessRuleException("Vehicle assignment is only permitted for ROAD transport mode.", "MODE_MISMATCH");

            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == input.VehicleId.Value, ct)
                ?? throw new NotFoundException("Vehicle", input.VehicleId.Value);

            if (vehicle.Status != PlanningConstants.VehicleStatus.Available)
                throw new BusinessRuleException($"Vehicle is currently '{vehicle.Status}' and cannot be assigned.", "VEHICLE_NOT_AVAILABLE");

            // Check if current horses on leg exceed vehicle capacity
            var currentHorses = await db.HorseLegAssignments.CountAsync(h => h.RouteLegId == legId, ct);
            if (currentHorses > vehicle.HorseCapacity)
                throw new BusinessRuleException($"Cannot assign vehicle with capacity {vehicle.HorseCapacity} because leg already has {currentHorses} horses assigned.", "VEHICLE_CAPACITY_EXCEEDED");

            // Overlap check
            if (leg.PlannedDepartureAt.HasValue && leg.PlannedArrivalAt.HasValue)
            {
                var dep = leg.PlannedDepartureAt.Value;
                var arr = leg.PlannedArrivalAt.Value;

                var isConflict = await db.TripLegAssignments
                    .Where(a => a.VehicleId == vehicle.Id && a.RouteLegId != legId)
                    .Join(
                        db.RouteLegs,
                        a => a.RouteLegId,
                        l2 => l2.Id,
                        (a, l2) => new { l2.PlannedDepartureAt, l2.PlannedArrivalAt }
                    )
                    .AnyAsync(l2 => l2.PlannedDepartureAt.HasValue && l2.PlannedArrivalAt.HasValue &&
                                   l2.PlannedDepartureAt.Value < arr && dep < l2.PlannedArrivalAt.Value, ct);

                if (isConflict)
                    throw new BusinessRuleException("Vehicle is already assigned to another route leg during this period.", "VEHICLE_DOUBLE_BOOKED");
            }
        }
        else if (input.FlightBookingId.HasValue)
        {
            if (leg.TransportMode != PlanningConstants.TransportMode.Air)
                throw new BusinessRuleException("Flight booking assignment is only permitted for AIR transport mode.", "MODE_MISMATCH");

            var flight = await db.FlightBookings.FirstOrDefaultAsync(f => f.Id == input.FlightBookingId.Value, ct)
                ?? throw new NotFoundException("FlightBooking", input.FlightBookingId.Value);

            if (flight.OriginAirportId != leg.OriginLocationId || flight.DestinationAirportId != leg.DestinationLocationId)
                throw new BusinessRuleException("Flight booking origin/destination must match the route leg airports.", "AIRPORT_MISMATCH");
        }

        // Replace any existing assignment on this leg
        var existing = await db.TripLegAssignments.Where(a => a.RouteLegId == legId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            db.TripLegAssignments.RemoveRange(existing);
        }

        var assignment = new TripLegAssignment
        {
            RouteLegId = legId,
            VehicleId = input.VehicleId,
            FlightBookingId = input.FlightBookingId,
            AssignedByUserId = input.AssignedByUserId ?? Guid.Empty,
            AssignedAt = DateTime.UtcNow
        };

        db.TripLegAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new ResourceAssignmentDto(assignment.Id, assignment.RouteLegId, assignment.VehicleId, assignment.FlightBookingId, assignment.AssignedByUserId, assignment.AssignedAt);
    }

    public async Task<StaffAssignmentDto> AssignStaffAsync(Guid tripId, StaffInput input, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        if (!await db.TransportTrips.AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == input.LegId, ct)
            ?? throw new NotFoundException("RouteLeg", input.LegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        var role = input.Role.Trim().ToUpperInvariant();
        if (role is not (PlanningConstants.StaffRole.Driver or PlanningConstants.StaffRole.Escort))
            throw new BusinessRuleException("Staff role must be DRIVER or ESCORT.", "INVALID_STAFF_ROLE");

        // Staff overlap check
        if (leg.PlannedDepartureAt.HasValue && leg.PlannedArrivalAt.HasValue)
        {
            var dep = leg.PlannedDepartureAt.Value;
            var arr = leg.PlannedArrivalAt.Value;

            var isConflict = await db.StaffAssignments
                .Where(s => s.UserId == input.UserId && s.RouteLegId.HasValue && s.RouteLegId.Value != input.LegId)
                .Join(
                    db.RouteLegs,
                    s => s.RouteLegId!.Value,
                    l2 => l2.Id,
                    (s, l2) => new { l2.PlannedDepartureAt, l2.PlannedArrivalAt }
                )
                .AnyAsync(l2 => l2.PlannedDepartureAt.HasValue && l2.PlannedArrivalAt.HasValue &&
                               l2.PlannedDepartureAt.Value < arr && dep < l2.PlannedArrivalAt.Value, ct);

            if (isConflict)
                throw new BusinessRuleException("Staff member is already assigned to a conflicting route leg.", "STAFF_DOUBLE_BOOKED");
        }

        var assignment = new StaffAssignment
        {
            TripId = tripId,
            RouteLegId = input.LegId,
            UserId = input.UserId,
            Role = role,
            AssignedAt = DateTime.UtcNow
        };

        db.StaffAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new StaffAssignmentDto(assignment.Id, assignment.TripId, assignment.RouteLegId, assignment.UserId, assignment.Role, assignment.AssignedAt, assignment.ReleasedAt);
    }

    public async Task<HorseAssignmentDto> AssignHorseAsync(Guid tripId, HorseInput input, CancellationToken ct = default)
    {
        await using var tx = await db.BeginTransactionAsync(ct);

        if (!await db.TransportTrips.AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        var leg = await db.RouteLegs.FirstOrDefaultAsync(l => l.Id == input.LegId, ct)
            ?? throw new NotFoundException("RouteLeg", input.LegId);

        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);

        AssertVersionIsDraft(version);

        if (await db.HorseLegAssignments.AnyAsync(h => h.RouteLegId == input.LegId && h.HorseId == input.HorseId, ct))
            throw new BusinessRuleException("Horse is already assigned to this route leg.", "HORSE_ALREADY_ASSIGNED");

        // Check vehicle horse capacity on this leg if vehicle is assigned
        var vehicleAssignment = await db.TripLegAssignments.FirstOrDefaultAsync(a => a.RouteLegId == input.LegId && a.VehicleId != null, ct);
        if (vehicleAssignment?.VehicleId != null)
        {
            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleAssignment.VehicleId.Value, ct);
            if (vehicle is not null)
            {
                var assignedHorseCount = await db.HorseLegAssignments.CountAsync(h => h.RouteLegId == input.LegId, ct);
                if (assignedHorseCount + 1 > vehicle.HorseCapacity)
                {
                    throw new BusinessRuleException(
                        $"Vehicle horse capacity ({vehicle.HorseCapacity}) exceeded for route leg {leg.SequenceNo}.",
                        "VEHICLE_CAPACITY_EXCEEDED"
                    );
                }
            }
        }

        if (input.StallId.HasValue)
        {
            var stall = await db.Stalls.FirstOrDefaultAsync(s => s.Id == input.StallId.Value, ct)
                ?? throw new NotFoundException("Stall", input.StallId.Value);

            if (!stall.Active)
                throw new BusinessRuleException("Stall is inactive and cannot be assigned.", "STALL_INACTIVE");

            // Check stall capacity overlap
            if (leg.PlannedDepartureAt.HasValue && leg.PlannedArrivalAt.HasValue)
            {
                var dep = leg.PlannedDepartureAt.Value;
                var arr = leg.PlannedArrivalAt.Value;

                var bookedCount = await db.HorseLegAssignments
                    .Where(h => h.StallId == stall.Id)
                    .Join(
                        db.RouteLegs,
                        h => h.RouteLegId,
                        l2 => l2.Id,
                        (h, l2) => new { l2.PlannedDepartureAt, l2.PlannedArrivalAt }
                    )
                    .CountAsync(l2 => l2.PlannedDepartureAt.HasValue && l2.PlannedArrivalAt.HasValue &&
                                     l2.PlannedDepartureAt.Value < arr && dep < l2.PlannedArrivalAt.Value, ct);

                if (bookedCount >= stall.Capacity)
                    throw new BusinessRuleException($"Stall capacity ({stall.Capacity}) is full for this interval.", "STALL_CAPACITY_EXCEEDED");
            }
        }

        var assignment = new HorseLegAssignment
        {
            TripId = tripId,
            HorseId = input.HorseId,
            RouteLegId = input.LegId,
            StallId = input.StallId
        };

        db.HorseLegAssignments.Add(assignment);
        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new HorseAssignmentDto(assignment.Id, assignment.TripId, assignment.HorseId, assignment.RouteLegId, assignment.StallId, assignment.BoardedAt, assignment.UnboardedAt);
    }

    public async Task<TripAssignmentsSummaryDto> GetTripAssignmentsAsync(Guid tripId, CancellationToken ct = default)
    {
        if (!await db.TransportTrips.AnyAsync(t => t.Id == tripId, ct))
            throw new NotFoundException("Trip", tripId);

        var resources = await db.TripLegAssignments.AsNoTracking()
            .Join(
                db.RouteLegs.AsNoTracking(),
                a => a.RouteLegId,
                l => l.Id,
                (a, l) => new { a, l }
            )
            .Join(
                db.RoutePlanVersions.AsNoTracking(),
                al => al.l.RoutePlanVersionId,
                v => v.Id,
                (al, v) => new { al.a, al.l, v }
            )
            .Join(
                db.RoutePlans.AsNoTracking(),
                alv => alv.v.RoutePlanId,
                p => p.Id,
                (alv, p) => new { alv.a, p.TripId }
            )
            .Where(x => x.TripId == tripId)
            .Select(x => new ResourceAssignmentDto(x.a.Id, x.a.RouteLegId, x.a.VehicleId, x.a.FlightBookingId, x.a.AssignedByUserId, x.a.AssignedAt))
            .ToListAsync(ct);

        var staff = await db.StaffAssignments.AsNoTracking()
            .Where(s => s.TripId == tripId)
            .Select(s => new StaffAssignmentDto(s.Id, s.TripId, s.RouteLegId, s.UserId, s.Role, s.AssignedAt, s.ReleasedAt))
            .ToListAsync(ct);

        var horses = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId)
            .Select(h => new HorseAssignmentDto(h.Id, h.TripId, h.HorseId, h.RouteLegId, h.StallId, h.BoardedAt, h.UnboardedAt))
            .ToListAsync(ct);

        return new TripAssignmentsSummaryDto(resources, staff, horses);
    }

    // ==========================================
    // State Transitions
    // ==========================================
    public async Task<RoutePlanVersionDto> SubmitRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.Draft)
            throw new BusinessRuleException($"Route plan version must be in DRAFT to submit. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        if (!await db.RouteLegs.AnyAsync(l => l.RoutePlanVersionId == versionId, ct))
            throw new BusinessRuleException("Cannot submit a route plan version with no route legs.", "NO_ROUTE_LEGS");

        version.Status = PlanningConstants.RouteVersionStatus.PendingApproval;
        await db.SaveChangesAsync(ct);

        return MapVersion(version);
    }

    public async Task<RoutePlanVersionDto> ApproveRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.PendingApproval)
            throw new BusinessRuleException($"Route plan version must be in PENDING_APPROVAL to approve. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        version.Status = PlanningConstants.RouteVersionStatus.Approved;
        version.ApprovedByUserId = actorUserId;
        version.ApprovedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    public async Task<RoutePlanVersionDto> ActivateRouteVersionAsync(Guid versionId, Guid actorUserId, CancellationToken ct = default)
    {
        var version = await db.RoutePlanVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", versionId);

        if (version.Status != PlanningConstants.RouteVersionStatus.Approved)
            throw new BusinessRuleException($"Only an APPROVED route version can be activated. Current status: {version.Status}", "INVALID_VERSION_TRANSITION");

        if (!await db.RouteLegs.AnyAsync(l => l.RoutePlanVersionId == versionId, ct))
            throw new BusinessRuleException("Route version contains no route legs and cannot be activated.", "NO_ROUTE_LEGS");

        var plan = await db.RoutePlans.FirstOrDefaultAsync(p => p.Id == version.RoutePlanId, ct)
            ?? throw new NotFoundException("RoutePlan", version.RoutePlanId);

        // Archive previous active versions
        var activeVersions = await db.RoutePlanVersions
            .Where(v => v.RoutePlanId == plan.Id && v.Status == PlanningConstants.RouteVersionStatus.Active)
            .ToListAsync(ct);

        foreach (var av in activeVersions)
        {
            av.Status = PlanningConstants.RouteVersionStatus.Archived;
        }

        version.Status = PlanningConstants.RouteVersionStatus.Active;
        plan.ActiveVersionId = version.Id;
        plan.UpdatedAt = DateTime.UtcNow;
        plan.VersionNo++;

        // Audit Log
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = "RoutePlanVersion",
            EntityId = version.Id,
            Action = "ACTIVATE",
            OldState = PlanningConstants.RouteVersionStatus.Approved,
            NewState = PlanningConstants.RouteVersionStatus.Active,
            ActorUserId = actorUserId,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        });

        // Outbox Event
        var payload = JsonSerializer.Serialize(new
        {
            tripId = plan.TripId,
            routePlanId = plan.Id,
            routePlanVersionId = version.Id,
            versionNo = version.VersionNo
        });

        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "Planning.RouteActivated",
            AggregateType = "RoutePlanVersion",
            AggregateId = version.Id,
            Payload = payload,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            Status = "PENDING"
        });

        await db.SaveChangesAsync(ct);
        return MapVersion(version);
    }

    // ==========================================
    // Helpers & Mappers
    // ==========================================
    private static void AssertVersionIsDraft(RoutePlanVersion version)
    {
        if (version.Status != PlanningConstants.RouteVersionStatus.Draft)
            throw new BusinessRuleException("Only DRAFT route versions can be edited; create a new version after activation.", "ROUTE_VERSION_IMMUTABLE");
    }

    private async Task ValidateLegLocationsAndTimesAsync(RouteLegInput input, CancellationToken ct)
    {
        if (input.ArrivalAt <= input.DepartureAt)
            throw new BusinessRuleException("Route leg arrival time must be after departure time.", "INVALID_LEG_TIMES", "arrivalAt");

        if (input.OriginLocationId == input.DestinationLocationId)
            throw new BusinessRuleException("Route leg origin and destination locations must be distinct.", "SAME_LOCATION", "destinationLocationId");

        var mode = input.Mode.Trim().ToUpperInvariant();
        if (mode is not (PlanningConstants.TransportMode.Road or PlanningConstants.TransportMode.Air))
            throw new BusinessRuleException("Transport mode must be ROAD or AIR.", "INVALID_TRANSPORT_MODE", "mode");

        if (!await db.Locations.AnyAsync(l => l.Id == input.OriginLocationId, ct))
            throw new NotFoundException("Origin Location", input.OriginLocationId);

        if (!await db.Locations.AnyAsync(l => l.Id == input.DestinationLocationId, ct))
            throw new NotFoundException("Destination Location", input.DestinationLocationId);
    }

    private static TripDto MapTrip(TransportTrip t) =>
        new(t.Id, t.TripNo, t.OrderId, t.Status, t.PlannedDepartureAt, t.PlannedArrivalAt, t.ComplianceReady, t.CreatedAt, t.UpdatedAt, t.VersionNo);

    private static RoutePlanDto MapPlan(RoutePlan p) =>
        new(p.Id, p.TripId, p.ActiveVersionId, p.CreatedByUserId, p.CreatedAt, p.UpdatedAt, p.VersionNo);

    private static RoutePlanVersionDto MapVersion(RoutePlanVersion v) =>
        new(v.Id, v.RoutePlanId, v.VersionNo, v.Status, v.Reason, v.TotalDistanceKm, v.EstimatedDurationMinutes, v.PlannedDepartureAt, v.PlannedArrivalAt, v.AdditionalCost, v.CurrencyCode, v.CreatedByUserId, v.ApprovedByUserId, v.ApprovedAt, v.CreatedAt);

    private static RouteLegDto MapLeg(RouteLeg l) =>
        new(l.Id, l.RoutePlanVersionId, l.SequenceNo, l.OriginLocationId, l.DestinationLocationId, l.TransportMode, l.PlannedDepartureAt, l.PlannedArrivalAt, l.DistanceKm, l.Status);

    private static CheckpointDto MapCheckpoint(Checkpoint c) =>
        new(c.Id, c.RouteLegId, c.SequenceNo, c.LocationId, c.Type, c.PlannedArrivalAt, c.PlannedDepartureAt, c.Status);
}
