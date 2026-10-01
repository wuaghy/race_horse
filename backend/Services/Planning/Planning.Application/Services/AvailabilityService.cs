using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Availability;
using Planning.Domain;

namespace Planning.Application.Services;

public class AvailabilityService(IPlanningDbContext db) : IAvailabilityService
{
    public async Task<AvailabilityResponse> CheckVehicleAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (to <= from)
            throw new BusinessRuleException("End time must be after start time.", "INVALID_TIME_RANGE", "to");

        var vehicle = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new NotFoundException("Vehicle", vehicleId);

        if (vehicle.Status != PlanningConstants.VehicleStatus.Available)
        {
            return new AvailabilityResponse(false, from, to, $"Vehicle status is '{vehicle.Status}'.");
        }

        // Check overlapping assignments
        var isBooked = await db.TripLegAssignments.AsNoTracking()
            .Where(a => a.VehicleId == vehicleId)
            .Join(
                db.RouteLegs.AsNoTracking(),
                assignment => assignment.RouteLegId,
                leg => leg.Id,
                (assignment, leg) => new { leg.PlannedDepartureAt, leg.PlannedArrivalAt }
            )
            .AnyAsync(l => l.PlannedDepartureAt.HasValue && l.PlannedArrivalAt.HasValue &&
                           l.PlannedDepartureAt.Value < to && from < l.PlannedArrivalAt.Value, ct);

        return isBooked
            ? new AvailabilityResponse(false, from, to, "Vehicle is already assigned to a route leg during this interval.")
            : new AvailabilityResponse(true, from, to);
    }

    public async Task<AvailabilityResponse> CheckStallAvailabilityAsync(Guid stallId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (to <= from)
            throw new BusinessRuleException("End time must be after start time.", "INVALID_TIME_RANGE", "to");

        var stall = await db.Stalls.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stallId, ct)
            ?? throw new NotFoundException("Stall", stallId);

        if (!stall.Active)
        {
            return new AvailabilityResponse(false, from, to, "Stall is inactive.");
        }

        // Count overlapping horse assignments
        var bookedCount = await db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.StallId == stallId)
            .Join(
                db.RouteLegs.AsNoTracking(),
                h => h.RouteLegId,
                leg => leg.Id,
                (h, leg) => new { leg.PlannedDepartureAt, leg.PlannedArrivalAt }
            )
            .CountAsync(l => l.PlannedDepartureAt.HasValue && l.PlannedArrivalAt.HasValue &&
                             l.PlannedDepartureAt.Value < to && from < l.PlannedArrivalAt.Value, ct);

        var available = bookedCount < stall.Capacity;
        var reason = available ? null : $"Stall capacity reached ({bookedCount}/{stall.Capacity}) for this interval.";

        return new AvailabilityResponse(available, from, to, reason);
    }

    public async Task<AvailabilityResponse> CheckFlightAvailabilityAsync(Guid flightBookingId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (to <= from)
            throw new BusinessRuleException("End time must be after start time.", "INVALID_TIME_RANGE", "to");

        var flight = await db.FlightBookings.AsNoTracking().FirstOrDefaultAsync(f => f.Id == flightBookingId, ct)
            ?? throw new NotFoundException("FlightBooking", flightBookingId);

        // A flight is available for a window if its scheduled flight interval overlaps or falls within the requested window
        var overlaps = flight.ScheduledDepartureAt < to && from < flight.ScheduledArrivalAt;

        return overlaps
            ? new AvailabilityResponse(true, from, to)
            : new AvailabilityResponse(false, from, to, "Flight departure/arrival times do not match the requested interval.");
    }
}
