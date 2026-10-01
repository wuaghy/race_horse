using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;
using Planning.Domain;
using Xunit;

namespace Planning.Tests;

public class ConcurrencyAndCapacityTests
{
    [Fact]
    public async Task AssignHorse_ExceedsVehicleHorseCapacity_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);

        // Vehicle with capacity of 1 horse
        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-111", VehicleType = "TRUCK", HorseCapacity = 1, StallCapacity = 2, Status = "AVAILABLE" };
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var stall = new Stall { StallCode = "S01", Capacity = 5, Active = true };
        db.Vehicles.Add(vehicle);
        db.Countries.Add(country);
        db.Stalls.Add(stall);
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

        // Assign vehicle with HorseCapacity = 1 to leg
        await planningSvc.AssignResourceAsync(leg.Id, new ResourceInput(vehicle.Id, null));

        // 1st horse: fits (1/1)
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));

        // 2nd horse: exceeds vehicle HorseCapacity -> must reject
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id)));

        Assert.Equal("VEHICLE_CAPACITY_EXCEEDED", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task AssignVehicle_WhenLegAlreadyHasMoreHorsesThanCapacity_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);

        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-111", VehicleType = "TRUCK", HorseCapacity = 1, StallCapacity = 2, Status = "AVAILABLE" };
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var stall = new Stall { StallCode = "S01", Capacity = 5, Active = true };
        db.Vehicles.Add(vehicle);
        db.Countries.Add(country);
        db.Stalls.Add(stall);
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

        // Assign 2 horses to leg first
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));

        // Now try assigning a vehicle with capacity = 1 -> must reject
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            planningSvc.AssignResourceAsync(leg.Id, new ResourceInput(vehicle.Id, null)));

        Assert.Equal("VEHICLE_CAPACITY_EXCEEDED", ex.Errors?[0].Reason);
    }
}
