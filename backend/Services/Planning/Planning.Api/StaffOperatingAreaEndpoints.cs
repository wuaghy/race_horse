using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain;

namespace Planning.Api;

public static class StaffOperatingAreaEndpoints
{
    public static RouteGroupBuilder MapStaffOperatingAreaEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/staff/{userId:guid}/operating-areas").RequireAuthorization("PlanningRead");

        group.MapGet("", async (Guid userId, IPlanningDbContext db, CancellationToken ct) =>
        {
            var areas = await (from area in db.StaffOperatingAreas.AsNoTracking()
                               join location in db.Locations.AsNoTracking() on area.LocationId equals location.Id
                               join country in db.Countries.AsNoTracking() on location.CountryId equals country.Id
                               where area.UserId == userId
                               orderby country.Name, location.City, location.Name
                               select new StaffOperatingAreaDto(area.Id, area.UserId, area.LocationId, location.Name,
                                   location.City ?? string.Empty, country.Name, location.Latitude ?? 0, location.Longitude ?? 0,
                                   area.RadiusKm, area.UpdatedAt))
                .ToListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StaffOperatingAreaDto>>.Success(areas));
        }).WithName("GetStaffOperatingAreas");

        group.MapPut("", async (Guid userId, ReplaceStaffOperatingAreasInput input, IPlanningDbContext db, CancellationToken ct) =>
        {
            if (userId == Guid.Empty || input.Areas is null || input.Areas.Count > 20 ||
                input.Areas.Any(area => area.LocationId == Guid.Empty || area.RadiusKm <= 0 || area.RadiusKm > 1000) ||
                input.Areas.Select(area => area.LocationId).Distinct().Count() != input.Areas.Count)
                throw new BusinessRuleException("Choose up to 20 unique operating areas with a radius between 0 and 1,000 km.", "INVALID_STAFF_OPERATING_AREAS");

            var locationIds = input.Areas.Select(area => area.LocationId).ToArray();
            var usableLocations = locationIds.Length == 0 ? new List<Guid>() : await db.Locations.AsNoTracking()
                .Where(location => locationIds.Contains(location.Id) && location.Active && location.Latitude.HasValue && location.Longitude.HasValue)
                .Select(location => location.Id)
                .ToListAsync(ct);
            if (usableLocations.Count != locationIds.Length)
                throw new BusinessRuleException("Every operating area must use an active location with map coordinates.", "STAFF_OPERATING_AREA_LOCATION_INVALID", "areas");

            var existing = await db.StaffOperatingAreas.Where(area => area.UserId == userId).ToListAsync(ct);
            db.StaffOperatingAreas.RemoveRange(existing);
            foreach (var area in input.Areas)
                db.StaffOperatingAreas.Add(new StaffOperatingArea
                {
                    UserId = userId,
                    LocationId = area.LocationId,
                    RadiusKm = area.RadiusKm,
                    UpdatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync(ct);

            var saved = await (from area in db.StaffOperatingAreas.AsNoTracking()
                               join location in db.Locations.AsNoTracking() on area.LocationId equals location.Id
                               join country in db.Countries.AsNoTracking() on location.CountryId equals country.Id
                               where area.UserId == userId
                               orderby country.Name, location.City, location.Name
                               select new StaffOperatingAreaDto(area.Id, area.UserId, area.LocationId, location.Name,
                                   location.City ?? string.Empty, country.Name, location.Latitude ?? 0, location.Longitude ?? 0,
                                   area.RadiusKm, area.UpdatedAt))
                .ToListAsync(ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StaffOperatingAreaDto>>.Success(saved, "Staff operating areas updated."));
        }).RequireAuthorization("PlanningDataAdmin").WithName("ReplaceStaffOperatingAreas");

        api.MapGet("/route-legs/{legId:guid}/nearest-drivers", GetNearestDrivers)
            .RequireAuthorization("PlanningRead")
            .WithName("GetNearestEligibleDrivers");

        return api;
    }

    private static async Task<IResult> GetNearestDrivers(Guid legId, IPlanningDbContext db, CancellationToken ct)
    {
        var leg = await db.RouteLegs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == legId, ct)
            ?? throw new NotFoundException("RouteLeg", legId);
        var version = await db.RoutePlanVersions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == leg.RoutePlanVersionId, ct)
            ?? throw new NotFoundException("RoutePlanVersion", leg.RoutePlanVersionId);
        if (version.Status is not (PlanningConstants.RouteVersionStatus.Draft or PlanningConstants.RouteVersionStatus.Active))
            throw new BusinessRuleException("Choose a draft or active route leg before looking for a driver.", "ROUTE_VERSION_NOT_ASSIGNABLE");
        if (!string.Equals(leg.TransportMode, PlanningConstants.TransportMode.Road, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Nearest-driver assignment is available for road legs only.", "DRIVER_ASSIGNMENT_REQUIRES_ROAD");
        if (!leg.PlannedDepartureAt.HasValue || !leg.PlannedArrivalAt.HasValue || leg.PlannedArrivalAt.Value <= leg.PlannedDepartureAt.Value)
            throw new BusinessRuleException("Set a valid departure and arrival time before looking for a driver.", "STAFF_SCHEDULE_REQUIRED");

        var routePlan = await db.RoutePlans.AsNoTracking().FirstAsync(item => item.Id == version.RoutePlanId, ct);
        var trip = await db.TransportTrips.AsNoTracking().FirstAsync(item => item.Id == routePlan.TripId, ct);
        var origin = await db.Locations.AsNoTracking().FirstOrDefaultAsync(item => item.Id == leg.OriginLocationId, ct)
            ?? throw new BusinessRuleException("The route origin is unavailable.", "ROUTE_ORIGIN_UNAVAILABLE");
        var hasFirstLegCustomPickup = leg.SequenceNo == 1 && trip.CustomPickupLatitude.HasValue && trip.CustomPickupLongitude.HasValue;
        var originLatitude = hasFirstLegCustomPickup ? trip.CustomPickupLatitude : origin.Latitude;
        var originLongitude = hasFirstLegCustomPickup ? trip.CustomPickupLongitude : origin.Longitude;
        if (!originLatitude.HasValue || !originLongitude.HasValue)
            throw new BusinessRuleException("Add map coordinates to the route pickup before using nearest-driver assignment.", "ROUTE_ORIGIN_COORDINATES_REQUIRED");

        var routeCountryIds = await db.Locations.AsNoTracking()
            .Where(location => location.Id == leg.OriginLocationId || location.Id == leg.DestinationLocationId ||
                db.Checkpoints.Any(checkpoint => checkpoint.RouteLegId == leg.Id && checkpoint.LocationId == location.Id))
            .Select(location => location.CountryId).Distinct().ToListAsync(ct);
        if (routeCountryIds.Count == 0)
            throw new BusinessRuleException("The route leg has no country information.", "ROUTE_COUNTRIES_REQUIRED");

        var validUntil = DateOnly.FromDateTime(leg.PlannedArrivalAt.Value);
        var authorizations = await db.StaffRouteCountryAuthorizations.AsNoTracking()
            .Where(authorization => routeCountryIds.Contains(authorization.CountryId) &&
                (!authorization.ValidUntil.HasValue || authorization.ValidUntil.Value >= validUntil))
            .Select(authorization => new { authorization.UserId, authorization.CountryId })
            .ToListAsync(ct);
        var requiredCountries = routeCountryIds.ToHashSet();
        var authorizedUserIds = authorizations.GroupBy(authorization => authorization.UserId)
            .Where(group => requiredCountries.IsSubsetOf(group.Select(authorization => authorization.CountryId)))
            .Select(group => group.Key).ToArray();

        var areas = await (from area in db.StaffOperatingAreas.AsNoTracking()
                           join location in db.Locations.AsNoTracking() on area.LocationId equals location.Id
                           where authorizedUserIds.Contains(area.UserId) && location.Active &&
                               location.Latitude.HasValue && location.Longitude.HasValue
                           select new { area.UserId, area.RadiusKm, area.UpdatedAt, Latitude = location.Latitude!.Value,
                               Longitude = location.Longitude!.Value, AreaName = location.City ?? location.Name })
            .ToListAsync(ct);
        var matchingAreas = areas.Select(area => new
            {
                area.UserId,
                area.AreaName,
                area.UpdatedAt,
                DistanceKm = DistanceKm((double)originLatitude.Value, (double)originLongitude.Value,
                    (double)area.Latitude, (double)area.Longitude),
                area.RadiusKm
            })
            .Where(area => area.DistanceKm <= (double)area.RadiusKm)
            .GroupBy(area => area.UserId)
            .Select(group => group.OrderBy(area => area.DistanceKm).First())
            .ToArray();

        if (matchingAreas.Length == 0)
            return Results.Ok(ApiResponse<IReadOnlyList<NearestDriverDto>>.Success(Array.Empty<NearestDriverDto>(),
                "No driver operating area covers this pickup with valid route-country authorization."));

        var userIds = matchingAreas.Select(area => area.UserId).Distinct().ToArray();
        var conflicts = await db.StaffAssignments.AsNoTracking()
            .Where(assignment => userIds.Contains(assignment.UserId) && assignment.ReleasedAt == null &&
                assignment.RouteLegId.HasValue && assignment.RouteLegId != legId)
            .Join(db.RouteLegs.AsNoTracking(), assignment => assignment.RouteLegId!.Value, otherLeg => otherLeg.Id,
                (assignment, otherLeg) => new { assignment, otherLeg })
            .Join(db.RoutePlanVersions.AsNoTracking(), row => row.otherLeg.RoutePlanVersionId, otherVersion => otherVersion.Id,
                (row, otherVersion) => new { row.assignment, row.otherLeg, otherVersion.Status })
            .Where(row => row.Status == PlanningConstants.RouteVersionStatus.Active || row.Status == PlanningConstants.RouteVersionStatus.Draft ||
                row.Status == PlanningConstants.RouteVersionStatus.PendingApproval || row.Status == PlanningConstants.RouteVersionStatus.Approved)
            .Where(row => row.otherLeg.PlannedDepartureAt.HasValue && row.otherLeg.PlannedArrivalAt.HasValue &&
                row.otherLeg.PlannedDepartureAt.Value < leg.PlannedArrivalAt.Value && leg.PlannedDepartureAt.Value < row.otherLeg.PlannedArrivalAt.Value)
            .Where(row => !db.TransportTrips.Any(otherTrip => otherTrip.Id == row.assignment.TripId &&
                (otherTrip.Status == PlanningConstants.TripStatus.Completed || otherTrip.Status == PlanningConstants.TripStatus.Cancelled)))
            .Select(row => row.assignment.UserId).Distinct().ToListAsync(ct);
        var alreadyOnLeg = await db.StaffAssignments.AsNoTracking()
            .Where(assignment => assignment.RouteLegId == legId && assignment.ReleasedAt == null)
            .Select(assignment => assignment.UserId).ToListAsync(ct);
        var unavailableIds = conflicts.Concat(alreadyOnLeg).ToHashSet();

        var candidates = matchingAreas.Where(area => !unavailableIds.Contains(area.UserId))
            .Select(area => new NearestDriverDto(area.UserId, Math.Round(area.DistanceKm, 2), area.AreaName, area.UpdatedAt))
            .OrderBy(candidate => candidate.DistanceKm)
            .ToArray();
        return Results.Ok(ApiResponse<IReadOnlyList<NearestDriverDto>>.Success(candidates));
    }

    private static double DistanceKm(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        const double earthRadiusKm = 6371.0088;
        var latDelta = DegreesToRadians(latitudeB - latitudeA);
        var lonDelta = DegreesToRadians(longitudeB - longitudeA);
        var a = Math.Pow(Math.Sin(latDelta / 2), 2) + Math.Cos(DegreesToRadians(latitudeA)) *
            Math.Cos(DegreesToRadians(latitudeB)) * Math.Pow(Math.Sin(lonDelta / 2), 2);
        a = Math.Clamp(a, 0d, 1d);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
