using Compliance.Application.DTOs.Clearance;
using Compliance.Domain.Entities;
using Compliance.Infrastructure.Services;
using Xunit;

namespace Compliance.Tests;

public class ClearanceServiceTests
{
    private static CreateClearanceCaseRequest MakeRequest(Guid tripId, string authorityName, string? notes = null)
        => new(tripId, null, Guid.NewGuid(), null, authorityName, null, notes);

    [Fact]
    public async Task CreateClearanceCase_ReturnsNewCase()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var tripId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var result = await svc.CreateClearanceCaseAsync(
            userId, MakeRequest(tripId, "Vietnam Customs", "Export clearance"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(tripId, result.TripId);
        Assert.Equal("Vietnam Customs", result.AuthorityName);
        Assert.Equal(ClearanceStatus.Draft, result.Status);
    }

    [Fact]
    public async Task GetClearanceCases_FiltersByTripId()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var trip1 = Guid.NewGuid();
        var trip2 = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await svc.CreateClearanceCaseAsync(userId, MakeRequest(trip1, "Authority A"), CancellationToken.None);
        await svc.CreateClearanceCaseAsync(userId, MakeRequest(trip2, "Authority B"), CancellationToken.None);

        var results = await svc.GetClearanceCasesByTripAsync(trip1, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(trip1, results[0].TripId);
    }

    [Fact]
    public async Task GetClearanceCaseById_NotFound_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var result = await svc.GetClearanceCaseByIdAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task SubmitClearanceCase_ChangesStatusToSubmitted()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var userId = Guid.NewGuid();
        var created = await svc.CreateClearanceCaseAsync(
            userId, MakeRequest(Guid.NewGuid(), "Import Authority"), CancellationToken.None);

        var result = await svc.SubmitClearanceCaseAsync(created.Id, userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClearanceStatus.Submitted, result!.Status);
    }

    [Fact]
    public async Task ReviewClearanceCase_Approve_CreatesOutbox()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var userId = Guid.NewGuid();
        var created = await svc.CreateClearanceCaseAsync(
            userId, MakeRequest(Guid.NewGuid(), "Export Authority"), CancellationToken.None);

        await svc.SubmitClearanceCaseAsync(created.Id, userId, CancellationToken.None);

        var reviewerId = Guid.NewGuid();
        var result = await svc.ReviewClearanceCaseAsync(
            created.Id, reviewerId,
            new ReviewClearanceCaseRequest("APPROVED", "All good"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClearanceStatus.Approved, result!.Status);

        var outbox = db.OutboxMessages.ToList();
        Assert.Single(outbox);
        Assert.Equal("Compliance.ClearanceApproved", outbox[0].EventType);
    }

    [Fact]
    public async Task ReviewClearanceCase_Reject_SetsRejectedStatus()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new ClearanceService(db);

        var userId = Guid.NewGuid();
        var created = await svc.CreateClearanceCaseAsync(
            userId, MakeRequest(Guid.NewGuid(), "Customs"), CancellationToken.None);

        await svc.SubmitClearanceCaseAsync(created.Id, userId, CancellationToken.None);

        var result = await svc.ReviewClearanceCaseAsync(
            created.Id, Guid.NewGuid(),
            new ReviewClearanceCaseRequest("REJECTED", "Incomplete documents"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ClearanceStatus.Rejected, result!.Status);
    }
}
