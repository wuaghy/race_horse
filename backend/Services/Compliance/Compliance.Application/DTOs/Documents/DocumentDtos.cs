namespace Compliance.Application.DTOs.Documents;

public sealed record DocumentReviewDto(
    Guid Id,
    Guid DocumentId,
    Guid ReviewerUserId,
    string Status,
    string? Comment,
    DateTime ReviewedAt);

public sealed record DocumentDto(
    Guid Id,
    string DocumentNo,
    Guid DocumentTypeId,
    string? DocumentTypeCode,
    string? DocumentTypeName,
    string? DocumentScope,
    Guid? HorseId,
    Guid? RequestId,
    Guid? TripId,
    Guid UploadedByUserId,
    string FileName,
    string FileUrl,
    long? FileSize,
    string? MimeType,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string? IssuingAuthority,
    string Status,
    string? CurrentReviewNote,
    DateTime UploadedAt,
    DateTime UpdatedAt,
    int VersionNo,
    IReadOnlyList<DocumentReviewDto> Reviews);

public sealed record UploadDocumentRequest(
    Guid DocumentTypeId,
    Guid? HorseId,
    Guid? RequestId,
    Guid? TripId,
    string FileName,
    string FileUrl,
    long? FileSize = null,
    string? MimeType = null,
    DateOnly? IssueDate = null,
    DateOnly? ExpiryDate = null,
    string? IssuingAuthority = null);

public sealed record ReviewDocumentRequest(
    string Status, // APPROVED, REJECTED, NEED_ADDITIONAL_INFO
    string? Comment = null);

public sealed record DocumentFilter(
    Guid? HorseId = null,
    Guid? RequestId = null,
    Guid? TripId = null,
    Guid? DocumentTypeId = null,
    string? Status = null);
