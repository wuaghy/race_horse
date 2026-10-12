namespace Contracts.IntegrationEvents;

public sealed record IncidentCreatedData(
    Guid IncidentId,
    string IncidentNo,
    Guid TripId,
    Guid? RouteLegId,
    Guid? CheckpointId,
    Guid? HorseId,
    string Type,
    string Severity,
    string Title,
    string Description,
    DateTime OccurredAt,
    Guid ReportedByUserId,
    decimal? Latitude = null,
    decimal? Longitude = null);

public sealed record IncidentCreatedEvent : IntegrationEvent<IncidentCreatedData>
{
    public override string EventType => "Incident.IncidentCreated";
    public override string Source => "incident-service";
}

public sealed record IncidentResolvedData(
    Guid IncidentId,
    string IncidentNo,
    Guid TripId,
    DateTime ResolvedAt,
    string? ResolutionNote,
    Guid ResolvedByUserId);

public sealed record IncidentResolvedEvent : IntegrationEvent<IncidentResolvedData>
{
    public override string EventType => "Incident.IncidentResolved";
    public override string Source => "incident-service";
}

public sealed record IncidentEmergencyRouteChangeRequestedData(
    Guid IncidentId,
    string IncidentNo,
    Guid TripId,
    string Reason,
    decimal? Latitude,
    decimal? Longitude,
    DateTime RequestedAt,
    Guid RequestedByUserId);

public sealed record IncidentEmergencyRouteChangeRequestedEvent : IntegrationEvent<IncidentEmergencyRouteChangeRequestedData>
{
    public override string EventType => "Incident.EmergencyRouteChangeRequested";
    public override string Source => "incident-service";
}
