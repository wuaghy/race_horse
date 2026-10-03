using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;
using Planning.Domain;
using Xunit;

namespace Planning.Tests;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task VehicleAvailability_ReturnsFalse_WhenAssignedToOverlappingLeg()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var availSvc = new AvailabilityService(db);

        // Setup master data
        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-111", VehicleType = "TRUCK", HorseCapacity = 4, StallCapacity = 4, Status = "AVAILABLE" };
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        db.Vehicles.Add(vehicle);
        db.Countries.Add(country);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        // Create Trip and Plan
        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await planningSvc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await planningSvc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        var leg = await planningSvc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(4)));

        // Before assignment, vehicle is available
        var checkBefore = await availSvc.CheckVehicleAvailabilityAsync(vehicle.Id, now.AddHours(1), now.AddHours(3));
        Assert.True(checkBefore.Available);

        // Assign vehicle to leg
        await planningSvc.AssignResourceAsync(leg.Id, new ResourceInput(vehicle.Id, null));

        // After assignment, overlapping window is NOT available
        var checkAfter = await availSvc.CheckVehicleAvailabilityAsync(vehicle.Id, now.AddHours(1), now.AddHours(3));
        Assert.False(checkAfter.Available);
        Assert.Contains("already assigned", checkAfter.Reason);

        // Non-overlapping window is available
        var checkNonOverlap = await availSvc.CheckVehicleAvailabilityAsync(vehicle.Id, now.AddHours(5), now.AddHours(7));
        Assert.True(checkNonOverlap.Available);
    }

    [Fact]
    public async Task StallAvailability_ReturnsFalse_WhenCapacityExceeded()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var availSvc = new AvailabilityService(db);

        var stall = new Stall { StallCode = "STL-01", Capacity = 2, Active = true };
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        db.Stalls.Add(stall);
        db.Countries.Add(country);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await planningSvc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await planningSvc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        var leg = await planningSvc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(4)));

        // Assign 1st horse: still available (1/2)
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));
        var check1 = await availSvc.CheckStallAvailabilityAsync(stall.Id, now, now.AddHours(4));
        Assert.True(check1.Available);

        // Assign 2nd horse: capacity reached (2/2)
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));
        var check2 = await availSvc.CheckStallAvailabilityAsync(stall.Id, now, now.AddHours(4));
        Assert.False(check2.Available);
        Assert.Contains("capacity reached", check2.Reason);
    }
}
