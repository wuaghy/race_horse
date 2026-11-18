namespace Planning.Application.DTOs.Readiness;

public sealed record ComplianceCheckItemDto(
    string DocumentTypeCode,
    string DocumentTypeName,
    string Scope,
    string TargetId,
    string Status,
    bool IsMet,
    string Details
);

public sealed record TripComplianceVerificationDto(
    Guid TripId,
    bool IsCompliant,
    int TotalRequired,
    int TotalMet,
    int TotalMissing,
    int TotalExpired,
    IReadOnlyList<string> BlockerMessages,
    IReadOnlyList<ComplianceCheckItemDto> CheckItems,
    DateTime EvaluatedAt
);
