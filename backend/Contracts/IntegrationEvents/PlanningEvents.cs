namespace Contracts.IntegrationEvents;

public sealed record RouteChangeAppliedData(
    Guid TripId,
    Guid RoutePlanId,
    Guid RoutePlanVersionId,
    int VersionNo,
    int? DelayEstimateMinutes,
    decimal AdditionalCost,
    string? CurrencyCode,
    string? Reason);

public sealed record RouteChangeAppliedEvent : IntegrationEvent<RouteChangeAppliedData>
{
    public override string EventType => "Planning.RouteChangeApplied";
    public override string Source => "planning-service";
}
