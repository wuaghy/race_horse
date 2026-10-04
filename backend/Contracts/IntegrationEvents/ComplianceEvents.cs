namespace Contracts.IntegrationEvents;

public sealed record ComplianceComplianceReadyData(
    Guid TripId,
    Guid? ClearanceCaseId,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    DateTime EvaluatedAt,
    string? Notes = null);

public sealed record ComplianceComplianceReadyEvent : IntegrationEvent<ComplianceComplianceReadyData>
{
    public override string EventType => "Compliance.ComplianceReady";
    public override string Source => "compliance-service";
}

public sealed record ComplianceClearanceApprovedData(
    Guid ClearanceCaseId,
    Guid TripId,
    Guid CountryId,
    string AuthorityName,
    DateTime ApprovedAt);

public sealed record ComplianceClearanceApprovedEvent : IntegrationEvent<ComplianceClearanceApprovedData>
{
    public override string EventType => "Compliance.ClearanceApproved";
    public override string Source => "compliance-service";
}

public sealed record ComplianceDocumentApprovedData(
    Guid DocumentId,
    string DocumentNo,
    Guid? HorseId,
    Guid? RequestId,
    Guid? TripId,
    DateTime ApprovedAt);

public sealed record ComplianceDocumentApprovedEvent : IntegrationEvent<ComplianceDocumentApprovedData>
{
    public override string EventType => "Compliance.DocumentApproved";
    public override string Source => "compliance-service";
}
