namespace Contracts.IntegrationEvents;

public sealed record HandoverCompletedData(
    Guid HandoverId,
    string HandoverNo,
    Guid TripId,
    Guid? HandoverLocationId,
    DateTime ActualAt,
    Guid CompletedByUserId,
    IReadOnlyList<Guid> AcceptedHorseIds);

public sealed record HandoverCompletedEvent : IntegrationEvent<HandoverCompletedData>
{
    public override string EventType => "Handover.HandoverCompleted";
    public override string Source => "handover-service";
}
