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
