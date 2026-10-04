namespace Compliance.Application.DTOs.Clearance;

public sealed record ClearanceCaseDocumentDto(
    Guid Id,
    Guid ClearanceCaseId,
    Guid DocumentId,
    string? DocumentNo,
    string? DocumentTypeName,
    bool Required,
    DateTime? SubmittedAt,
    string Status);

public sealed record ClearanceCaseDto(
    Guid Id,
    string CaseNo,
    Guid TripId,
    Guid? RouteLegId,
    Guid CountryId,
    Guid? BorderPointId,
    string AuthorityName,
    string? ReferenceNumber,
    string Status,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    DateTime? CompletedAt,
    string? RejectionReason,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo,
    IReadOnlyList<ClearanceCaseDocumentDto> Documents);

public sealed record CreateClearanceCaseRequest(
    Guid TripId,
    Guid? RouteLegId,
    Guid CountryId,
    Guid? BorderPointId,
    string AuthorityName,
    string? ReferenceNumber = null,
    string? Notes = null,
    IReadOnlyList<Guid>? DocumentIds = null);

public sealed record ReviewClearanceCaseRequest(
    string Status, // APPROVED, REJECTED, NEED_ADDITIONAL_INFO, COMPLETED
    string? Reason = null,
    string? ReferenceNumber = null,
    string? Notes = null);
