namespace Contracts.IntegrationEvents;

public sealed record TripCompletedData(
    Guid TripId,
    string TripNo,
    Guid OrderId,
    string Status);

public sealed record TripCompletedEvent : IntegrationEvent<TripCompletedData>
{
    public override string EventType => "Planning.TripCompleted";
    public override string Source => "planning-service";
}
