namespace Compliance.Application.DTOs.Readiness;

public sealed record ComplianceReadinessCheckItem(
    string RuleCode,
    string RuleTitle,
    string DocumentTypeCode,
    string DocumentTypeName,
    string TargetScope, // HORSE or TRIP
    Guid? TargetEntityId, // HorseId or TripId
    bool IsMet,
    string Status, // APPROVED, MISSING, EXPIRED, UNDER_REVIEW
    string? DocumentNo,
    DateOnly? ExpiryDate,
    string? Notes);

public sealed record ComplianceReadinessResultDto(
    Guid TripId,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    bool IsReady,
    int TotalRequirements,
    int MetRequirements,
    int MissingRequirements,
    int ExpiredRequirements,
    int PendingReviewRequirements,
    IReadOnlyList<ComplianceReadinessCheckItem> Items,
    DateTime EvaluatedAt);

public sealed record EvaluateComplianceReadinessRequest(
    Guid TripId,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    IReadOnlyList<Guid> HorseIds,
    DateOnly? PlannedDepartureDate = null);

public sealed record ComplianceRouteLegContext(
    Guid RouteLegId,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    DateTime PlannedDepartureAt,
    DateTime? PlannedArrivalAt);

public sealed record EvaluateDispatchTripReadinessRequest(
    Guid TripId,
    IReadOnlyList<Guid> RequestIds,
    IReadOnlyList<Guid> HorseIds,
    IReadOnlyList<ComplianceRouteLegContext> RouteLegs);

public sealed record DispatchComplianceRequirementResult(
    Guid RouteLegId,
    string RuleCode,
    string DocumentTypeCode,
    string TargetScope,
    Guid? TargetEntityId,
    bool IsMet,
    string Status,
    string? Notes);

public sealed record DispatchClearanceRequirementResult(
    Guid RouteLegId,
    Guid CountryId,
    string ClearanceType,
    Guid? CaseId,
    bool IsRequired,
    bool IsMet,
    string Status,
    string? Notes);

public sealed record DispatchComplianceReadinessResult(
    Guid TripId,
    bool IsReady,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<DispatchComplianceRequirementResult> Requirements,
    IReadOnlyList<DispatchClearanceRequirementResult> Clearances,
    DateTime EvaluatedAt);
