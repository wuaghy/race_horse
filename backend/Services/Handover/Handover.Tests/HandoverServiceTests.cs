using BuildingBlocks.Exceptions;
using Handover.Application.Abstractions;
using Handover.Application.DTOs;
using Handover.Domain.Entities;
using Handover.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Handover.Tests;

public class HandoverServiceTests
{
    [Fact]
    public async Task Create_DuplicateTrip_Throws422()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new HandoverService(db);
        var tripId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var created = await svc.CreateAsync(actorId, new CreateHandoverRequest(tripId, "Receiver A"), Guid.NewGuid());
        Assert.Equal(HandoverStatuses.Draft, created.Status);
        Assert.Equal(1, created.VersionNo);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateAsync(actorId, new CreateHandoverRequest(tripId, "Receiver B"), Guid.NewGuid()));

        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public async Task SubmitForAcceptance_RequiresHorsesAndReceiverName()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new HandoverService(db);
        var actorId = Guid.NewGuid();

        // Created without horses and without receiverName
        var handover = await svc.CreateAsync(actorId, new CreateHandoverRequest(Guid.NewGuid()), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.SubmitForAcceptanceAsync(handover.Id, actorId, new SubmitHandoverRequest(), null));

        // Add horse, still missing receiverName
        await svc.UpsertHorseAsync(handover.Id, actorId, new UpsertHandoverHorseRequest(Guid.NewGuid(), "Healthy"), null);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.SubmitForAcceptanceAsync(handover.Id, actorId, new SubmitHandoverRequest(), null));

        // Update receiverName, then submit succeeds
        await svc.UpdateDraftAsync(handover.Id, actorId, new UpdateHandoverRequest("Receiver Name", "+84-909", "r@example.com", null, null, "Notes"), null);
        var submitted = await svc.SubmitForAcceptanceAsync(handover.Id, actorId, new SubmitHandoverRequest("Ready for customer acceptance"), null);

        Assert.Equal(HandoverStatuses.PendingAcceptance, submitted.Status);
    }

    [Fact]
    public async Task Complete_BlocksWhenHorsesPendingOrDisputedOrSignatureMissing()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new HandoverService(db);
        var driverId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var horse1 = Guid.NewGuid();
        var horse2 = Guid.NewGuid();

        // 1. Driver creates handover with 2 horses
        var handover = await svc.CreateAsync(driverId, new CreateHandoverRequest(
            Guid.NewGuid(),
            ReceiverName: "Customer Stable",
            Horses:
            [
                new CreateHandoverHorseItem(horse1, "Stable condition"),
                new CreateHandoverHorseItem(horse2, "Minor scratch on left leg")
            ]), null);

        // Cannot complete while DRAFT
        var draftCompleteEx = await Assert.ThrowsAsync<AppException>(() =>
            svc.CompleteAsync(handover.Id, managerId, new CompleteHandoverRequest(), null));
        Assert.Equal(422, draftCompleteEx.StatusCode);

        // 2. Submit for acceptance -> PENDING_ACCEPTANCE
        await svc.SubmitForAcceptanceAsync(handover.Id, driverId, new SubmitHandoverRequest(), null);

        // 3. Customer accepts horse1, disputes horse2
        var afterAccept1 = await svc.AcceptHorseAsync(handover.Id, horse1, customerId, new AcceptHandoverHorseRequest("Good", "Accepted horse 1"), null);
        Assert.Equal(HandoverStatuses.PendingAcceptance, afterAccept1.Status);

        var afterDispute2 = await svc.DisputeHorseAsync(handover.Id, horse2, customerId, new DisputeHandoverHorseRequest("Leg scratch needs vet confirmation"), null);
        Assert.Equal(HandoverStatuses.Disputed, afterDispute2.Status);

        // Upload signature while horse2 is still DISPUTED -> completion still blocked!
        await svc.UploadSignatureAsync(handover.Id, customerId, new StoredHandoverFile("sig.png", "sig.png", "image/png", 128), null, null);

        var checkWhileDisputed = await svc.EvaluateCompletionAsync(handover.Id);
        Assert.False(checkWhileDisputed.ReadyToComplete);
        Assert.Contains(checkWhileDisputed.Blockers, b => b.Contains("DISPUTED"));

        var disputedCompleteEx = await Assert.ThrowsAsync<AppException>(() =>
            svc.CompleteAsync(handover.Id, managerId, new CompleteHandoverRequest(), null));
        Assert.Equal(422, disputedCompleteEx.StatusCode);

        // 4. Manager formally resolves horse2 dispute -> status becomes ACCEPTED
        var afterResolve = await svc.ResolveHorseDisputeAsync(handover.Id, horse2, managerId, new ResolveHandoverHorseDisputeRequest("Vet examined and cleared; customer agreed."), null);
        Assert.Equal(HandoverStatuses.Accepted, afterResolve.Status);
        Assert.True(afterResolve.CanComplete);
        Assert.Empty(afterResolve.CompletionBlockers);

        // 5. Complete handover -> status becomes COMPLETED, AuditLog + OutboxMessage written
        var completed = await svc.CompleteAsync(handover.Id, managerId, new CompleteHandoverRequest(Notes: "All horses handed over."), Guid.NewGuid());
        Assert.Equal(HandoverStatuses.Completed, completed.Status);
        Assert.Equal(managerId, completed.CompletedByUserId);
        Assert.NotNull(completed.ActualAt);

        var outbox = await db.OutboxMessages.SingleOrDefaultAsync(x => x.AggregateId == handover.Id && x.EventType == "Handover.HandoverCompleted");
        Assert.NotNull(outbox);
        Assert.Equal("PENDING", outbox.Status);

        var audits = await db.AuditLogs.Where(x => x.EntityId == handover.Id).ToListAsync();
        Assert.Contains(audits, a => a.Action == "CREATED");
        Assert.Contains(audits, a => a.Action == "SUBMITTED_FOR_ACCEPTANCE");
        Assert.Contains(audits, a => a.Action == "HORSE_ACCEPTED");
        Assert.Contains(audits, a => a.Action == "HORSE_DISPUTED");
        Assert.Contains(audits, a => a.Action == "DISPUTE_RESOLVED");
        Assert.Contains(audits, a => a.Action == "COMPLETED" && a.NewState == HandoverStatuses.Completed);
    }

    [Fact]
    public async Task ExpectedVersionMismatch_Throws409ConcurrencyException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new HandoverService(db);
        var actorId = Guid.NewGuid();

        var handover = await svc.CreateAsync(actorId, new CreateHandoverRequest(
            Guid.NewGuid(),
            ReceiverName: "Receiver",
            Horses: [new CreateHandoverHorseItem(Guid.NewGuid(), "Healthy")]), null);

        Assert.Equal(1, handover.VersionNo);

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            svc.SubmitForAcceptanceAsync(handover.Id, actorId, new SubmitHandoverRequest(ExpectedVersionNo: 99), null));

        Assert.Equal(409, ex.StatusCode);
    }
}
