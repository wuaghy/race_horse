using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;
using Planning.Domain;
using Planning.Infrastructure.Persistence;
using Xunit;

namespace Planning.Tests;

public class TripPlanningServiceTests
{
    [Fact]
    public async Task CreateTrip_IsIdempotent_AndCreatesOutboxMessage()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var orderId = Guid.NewGuid();
        var trip1 = await svc.CreateTripAsync(new CreateTripInput(orderId));
        var trip2 = await svc.CreateTripAsync(new CreateTripInput(orderId));

        Assert.Equal(trip1.Id, trip2.Id);
        Assert.Equal(trip1.TripNo, trip2.TripNo);

        var outbox = await db.OutboxMessages.ToListAsync();
        Assert.Single(outbox);
        Assert.Equal("Planning.TripCreated", outbox[0].EventType);
    }

    [Fact]
    public async Task ActiveRoute_CannotBeModified_RequiresNewVersion()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var (trip, plan, version, locA, locB) = await SetupActiveRouteTripAsync(db, svc);

        // Try adding a leg to ACTIVE version -> must fail
        var now = DateTime.UtcNow.AddDays(2);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.AddLegAsync(version.Id, new RouteLegInput(2, locA.Id, locB.Id, "ROAD", now, now.AddHours(2))));

        Assert.Equal("ROUTE_VERSION_IMMUTABLE", ex.Errors?[0].Reason);

        // Create new version -> works and has status DRAFT
        var newVersion = await svc.CreateNewVersionAsync(plan.Id, Guid.NewGuid(), "Emergency detour");
        Assert.Equal(PlanningConstants.RouteVersionStatus.Draft, newVersion.Status);
        Assert.Equal(2, newVersion.VersionNo);
        Assert.NotEqual(version.Id, newVersion.Id);

        // Can add leg to new DRAFT version
        var newLeg = await svc.AddLegAsync(newVersion.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(3)));
        Assert.NotNull(newLeg);
    }

    [Fact]
    public async Task VehicleAssignment_RejectsOverlappingDoubleBooking()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-12345", VehicleType = "TRUCK", HorseCapacity = 4, StallCapacity = 4, Status = "AVAILABLE" };
        db.Countries.Add(country);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip1 = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan1 = await svc.CreateRoutePlanAsync(trip1.Id, Guid.NewGuid());
        var version1 = (await svc.GetVersionsAsync(plan1.Id)).Single();

        var trip2 = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan2 = await svc.CreateRoutePlanAsync(trip2.Id, Guid.NewGuid());
        var version2 = (await svc.GetVersionsAsync(plan2.Id)).Single();

        var from = DateTime.UtcNow.AddDays(1);
        var to = from.AddHours(4);

        var leg1 = await svc.AddLegAsync(version1.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", from, to));
        var leg2 = await svc.AddLegAsync(version2.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", from.AddHours(1), to.AddHours(2)));

        // Assign to leg 1: OK
        await svc.AssignResourceAsync(leg1.Id, new ResourceInput(vehicle.Id, null));

        // Assign to leg 2 (overlapping time): Must reject
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.AssignResourceAsync(leg2.Id, new ResourceInput(vehicle.Id, null)));

        Assert.Equal("VEHICLE_DOUBLE_BOOKED", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task StaffAssignment_RejectsOverlappingDoubleBooking()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        db.Countries.Add(country);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await svc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await svc.GetVersionsAsync(plan.Id)).Single();

        var from = DateTime.UtcNow.AddDays(1);
        var to = from.AddHours(4);

        var leg1 = await svc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", from, to));
        var leg2 = await svc.AddLegAsync(version.Id, new RouteLegInput(2, locB.Id, locA.Id, "ROAD", from.AddHours(2), to.AddHours(3)));

        var staffUserId = Guid.NewGuid();

        // Assign staff to leg 1: OK
        await svc.AssignStaffAsync(trip.Id, new StaffInput(leg1.Id, staffUserId, "DRIVER"));

        // Assign same staff to leg 2 (overlapping time): Must reject
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.AssignStaffAsync(trip.Id, new StaffInput(leg2.Id, staffUserId, "DRIVER")));

        Assert.Equal("STAFF_DOUBLE_BOOKED", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task StallAssignment_RejectsWhenCapacityExceeded()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var stall = new Stall { StallCode = "S-1", Capacity = 1, Active = true };
        db.Countries.Add(country);
        db.Stalls.Add(stall);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await svc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await svc.GetVersionsAsync(plan.Id)).Single();

        var from = DateTime.UtcNow.AddDays(1);
        var to = from.AddHours(4);
        var leg = await svc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", from, to));

        // 1st horse: OK
        await svc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));

        // 2nd horse: exceeds capacity of 1
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id)));

        Assert.Equal("STALL_CAPACITY_EXCEEDED", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task RouteVersion_FullLifecycleTransitions_AndOutboxPublished()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new TripPlanningService(db);

        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        db.Countries.Add(country);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await svc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await svc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        await svc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(3)));

        // Submit: DRAFT -> PENDING_APPROVAL
        var submitted = await svc.SubmitRouteVersionAsync(version.Id, Guid.NewGuid());
        Assert.Equal(PlanningConstants.RouteVersionStatus.PendingApproval, submitted.Status);

        // Approve: PENDING_APPROVAL -> APPROVED
        var approved = await svc.ApproveRouteVersionAsync(version.Id, Guid.NewGuid());
        Assert.Equal(PlanningConstants.RouteVersionStatus.Approved, approved.Status);

        // Activate: APPROVED -> ACTIVE
        var activated = await svc.ActivateRouteVersionAsync(version.Id, Guid.NewGuid());
        Assert.Equal(PlanningConstants.RouteVersionStatus.Active, activated.Status);

        var planInDb = await db.RoutePlans.FirstAsync(p => p.Id == plan.Id);
        Assert.Equal(version.Id, planInDb.ActiveVersionId);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == version.Id && a.Action == "ACTIVATE");
        Assert.NotNull(audit);

        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "Planning.RouteActivated");
        Assert.NotNull(outbox);
    }

    private static async Task<(TripDto Trip, RoutePlanDto Plan, RoutePlanVersionDto Version, Location LocA, Location LocB)>
        SetupActiveRouteTripAsync(PlanningDbContext db, TripPlanningService svc)
    {
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        db.Countries.Add(country);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        var trip = await svc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await svc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await svc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        await svc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(3)));

        await svc.SubmitRouteVersionAsync(version.Id, Guid.NewGuid());
        await svc.ApproveRouteVersionAsync(version.Id, Guid.NewGuid());
        var active = await svc.ActivateRouteVersionAsync(version.Id, Guid.NewGuid());

        return (trip, plan, active, locA, locB);
    }
}
