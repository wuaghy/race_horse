using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Application.DTOs.Readiness;
using Planning.Application.Services;
using Planning.Domain;
using Xunit;

namespace Planning.Tests;

public class TripReadinessServiceTests
{
    [Fact]
    public async Task EvaluateReadiness_ListsAllBlockers_WhenPreconditionsMissing()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var readinessSvc = new TripReadinessService(db);

        // Just create a bare trip
        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));

        var result = await readinessSvc.EvaluateReadinessAsync(trip.Id);

        Assert.False(result.Ready);
        Assert.Contains("Route version chưa ACTIVE", result.Blockers);
        Assert.Contains("Chưa assign escort", result.Blockers);
        Assert.Contains("Chưa assign horse cho trip", result.Blockers);
        Assert.Contains("Compliance chưa ready", result.Blockers);
    }

    [Fact]
    public async Task ConfirmReady_Throws422_WhenBlockersRemain()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var readinessSvc = new TripReadinessService(db);

        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            readinessSvc.ConfirmReadyAsync(trip.Id, Guid.NewGuid()));

        Assert.Equal(422, ex.StatusCode);
        Assert.Equal(42200, ex.ErrorCode);
        Assert.NotNull(ex.Errors);
        Assert.True(ex.Errors.Count > 0);
    }

    [Fact]
    public async Task ConfirmReady_Succeeds_WhenAllPrerequisitesMet_AndEmitsOutboxEvent()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var readinessSvc = new TripReadinessService(db);

        // 1. Setup master data
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-12345", VehicleType = "TRUCK", HorseCapacity = 4, StallCapacity = 4, Status = "AVAILABLE" };
        var stall = new Stall { StallCode = "S01", Capacity = 2, Active = true };
        db.Countries.Add(country);
        db.Vehicles.Add(vehicle);
        db.Stalls.Add(stall);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        // 2. Setup trip and plan
        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await planningSvc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await planningSvc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        var leg = await planningSvc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(3)));

        // 3. Assign Vehicle to leg
        await planningSvc.AssignResourceAsync(leg.Id, new ResourceInput(vehicle.Id, null));

        // 4. Assign ESCORT staff to trip
        await planningSvc.AssignStaffAsync(trip.Id, new StaffInput(leg.Id, Guid.NewGuid(), PlanningConstants.StaffRole.Escort));

        // 5. Assign horse with stall
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));

        // 6. Submit, approve, activate route
        await planningSvc.SubmitRouteVersionAsync(version.Id, Guid.NewGuid());
        await planningSvc.ApproveRouteVersionAsync(version.Id, Guid.NewGuid());
        await planningSvc.ActivateRouteVersionAsync(version.Id, Guid.NewGuid());

        // Still blocked on compliance
        var preCheck = await readinessSvc.EvaluateReadinessAsync(trip.Id);
        Assert.False(preCheck.Ready);
        Assert.Single(preCheck.Blockers);
        Assert.Equal("Compliance chưa ready", preCheck.Blockers[0]);

        // 7. Mark compliance ready
        var compReadyTrip = await readinessSvc.SetComplianceReadyAsync(trip.Id);
        Assert.True(compReadyTrip.ComplianceReady);

        // 8. Now readiness must be true
        var finalCheck = await readinessSvc.EvaluateReadinessAsync(trip.Id);
        Assert.True(finalCheck.Ready);
        Assert.Empty(finalCheck.Blockers);

        // 9. Confirm ready command
        var actorUserId = Guid.NewGuid();
        var confirmed = await readinessSvc.ConfirmReadyAsync(trip.Id, actorUserId);

        Assert.Equal(PlanningConstants.TripStatus.Ready, confirmed.Status);

        // Verify database state: status is READY
        var tripInDb = await db.TransportTrips.FirstAsync(t => t.Id == trip.Id);
        Assert.Equal(PlanningConstants.TripStatus.Ready, tripInDb.Status);
        Assert.True(tripInDb.ComplianceReady);

        // Verify AuditLog
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == trip.Id && a.Action == "CONFIRM_READY");
        Assert.NotNull(audit);
        Assert.Equal(PlanningConstants.TripStatus.Ready, audit.NewState);
        Assert.Equal(actorUserId, audit.ActorUserId);

        // Verify Outbox message
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(o => o.AggregateId == trip.Id && o.EventType == "Planning.TripReady");
        Assert.NotNull(outbox);
        Assert.Equal("PENDING", outbox.Status);
    }

    [Fact]
    public async Task EvaluateReadiness_WhenGrpcComplianceReturnsBlockers_MergesSpecificBlockers()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var fakeClient = new FakeComplianceClient
        {
            Result = new TripComplianceVerificationDto(
                Guid.NewGuid(),
                false,
                2,
                0,
                2,
                0,
                new List<string> { "Coggins test missing for horse H-01", "Import permit expired" },
                new List<ComplianceCheckItemDto>(),
                DateTime.UtcNow
            )
        };
        var readinessSvc = new TripReadinessService(db, fakeClient);

        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));

        var result = await readinessSvc.EvaluateReadinessAsync(trip.Id);

        Assert.False(result.Ready);
        Assert.Contains("Coggins test missing for horse H-01", result.Blockers);
        Assert.Contains("Import permit expired", result.Blockers);
    }

    [Fact]
    public async Task EvaluateReadiness_WhenGrpcComplianceSucceeds_UpdatesLocalStateAndAllowsReady()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);

        // 1. Setup master data
        var country = new Country { IsoCode = "VN", Name = "Vietnam", Active = true };
        var vehicle = new Vehicle { VehicleCode = "V01", PlateNumber = "51H-12345", VehicleType = "TRUCK", HorseCapacity = 4, StallCapacity = 4, Status = "AVAILABLE" };
        var stall = new Stall { StallCode = "S01", Capacity = 2, Active = true };
        db.Countries.Add(country);
        db.Vehicles.Add(vehicle);
        db.Stalls.Add(stall);
        await db.SaveChangesAsync();

        var locA = new Location { Name = "Loc A", LocationType = "STABLE", CountryId = country.Id };
        var locB = new Location { Name = "Loc B", LocationType = "STABLE", CountryId = country.Id };
        db.Locations.AddRange(locA, locB);
        await db.SaveChangesAsync();

        // 2. Setup trip and plan
        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));
        var plan = await planningSvc.CreateRoutePlanAsync(trip.Id, Guid.NewGuid());
        var version = (await planningSvc.GetVersionsAsync(plan.Id)).Single();

        var now = DateTime.UtcNow.AddDays(1);
        var leg = await planningSvc.AddLegAsync(version.Id, new RouteLegInput(1, locA.Id, locB.Id, "ROAD", now, now.AddHours(3)));

        await planningSvc.AssignResourceAsync(leg.Id, new ResourceInput(vehicle.Id, null));
        await planningSvc.AssignStaffAsync(trip.Id, new StaffInput(leg.Id, Guid.NewGuid(), PlanningConstants.StaffRole.Escort));
        await planningSvc.AssignHorseAsync(trip.Id, new HorseInput(Guid.NewGuid(), leg.Id, stall.Id));

        await planningSvc.SubmitRouteVersionAsync(version.Id, Guid.NewGuid());
        await planningSvc.ApproveRouteVersionAsync(version.Id, Guid.NewGuid());
        await planningSvc.ActivateRouteVersionAsync(version.Id, Guid.NewGuid());

        var fakeClient = new FakeComplianceClient
        {
            Result = new TripComplianceVerificationDto(
                trip.Id,
                true,
                1,
                1,
                0,
                0,
                new List<string>(),
                new List<ComplianceCheckItemDto>(),
                DateTime.UtcNow
            )
        };
        var readinessSvc = new TripReadinessService(db, fakeClient);

        var result = await readinessSvc.EvaluateReadinessAsync(trip.Id);

        Assert.True(result.Ready);
        Assert.Empty(result.Blockers);

        var tripInDb = await db.TransportTrips.FirstAsync(t => t.Id == trip.Id);
        Assert.True(tripInDb.ComplianceReady);
    }

    [Fact]
    public async Task EvaluateReadiness_WhenGrpcFails_GracefullyDegradesToCachedLocalState()
    {
        using var db = TestDbContextFactory.Create();
        var planningSvc = new TripPlanningService(db);
        var trip = await planningSvc.CreateTripAsync(new CreateTripInput(Guid.NewGuid()));

        var fakeClient = new FakeComplianceClient
        {
            ThrowsException = new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "Service unavailable"))
        };
        var readinessSvc = new TripReadinessService(db, fakeClient);

        // Case A: ComplianceReady is false -> fallback reports blocker
        var result1 = await readinessSvc.EvaluateReadinessAsync(trip.Id);
        Assert.Contains("Compliance chưa ready", result1.Blockers);

        // Case B: ComplianceReady is true -> fallback permits compliance check
        await readinessSvc.SetComplianceReadyAsync(trip.Id);
        var result2 = await readinessSvc.EvaluateReadinessAsync(trip.Id);
        Assert.DoesNotContain("Compliance chưa ready", result2.Blockers);
    }

    private class FakeComplianceClient : IComplianceVerificationClient
    {
        public TripComplianceVerificationDto? Result { get; set; }
        public Exception? ThrowsException { get; set; }

        public Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(Guid tripId, CancellationToken cancellationToken = default)
        {
            if (ThrowsException != null) throw ThrowsException;
            return Task.FromResult(Result ?? new TripComplianceVerificationDto(tripId, false, 0, 0, 0, 0, new List<string>(), new List<ComplianceCheckItemDto>(), DateTime.UtcNow));
        }

        public Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
            Guid tripId, Guid originCountryId, Guid destinationCountryId,
            IReadOnlyList<Guid> horseIds, DateOnly? plannedDepartureDate = null,
            CancellationToken cancellationToken = default)
        {
            return VerifyTripComplianceAsync(tripId, cancellationToken);
        }
    }
}
