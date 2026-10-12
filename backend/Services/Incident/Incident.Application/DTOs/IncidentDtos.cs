namespace Incident.Application.DTOs;

public record CreateIncidentRequest(
    Guid TripId,
    Guid? RouteLegId,
    Guid? CheckpointId,
    Guid? HorseId,
    string Type,
    string Severity,
    string Title,
    string Description,
    DateTime OccurredAt,
    decimal? Latitude = null,
    decimal? Longitude = null);

public record UpdateIncidentStatusRequest(
    string NewStatus,
    string? Reason = null);

public record ResolveIncidentRequest(
    string ResolutionNote);

public record IncidentFilterDto(
    Guid? TripId = null,
    Guid? HorseId = null,
    string? Type = null,
    string? Severity = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 20);

public record IncidentSummaryDto(
    Guid Id,
    string IncidentNo,
    Guid TripId,
    Guid? HorseId,
    string Type,
    string Severity,
    string Status,
    string Title,
    DateTime OccurredAt,
    DateTime? ResolvedAt,
    int AttachmentCount);

public record IncidentDto(
    Guid Id,
    string IncidentNo,
    Guid TripId,
    Guid? RouteLegId,
    Guid? CheckpointId,
    Guid? HorseId,
    Guid ReportedByUserId,
    string Type,
    string Severity,
    string Status,
    Guid? LocationId,
    decimal? Latitude,
    decimal? Longitude,
    string Title,
    string Description,
    DateTime OccurredAt,
    DateTime? ResolvedAt,
    string? ResolutionNote,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo,
    IReadOnlyList<IncidentAttachmentDto> Attachments);

public record IncidentAttachmentDto(
    Guid Id,
    Guid IncidentId,
    string FileName,
    string FileUrl,
    string? MimeType,
    Guid UploadedByUserId,
    DateTime UploadedAt);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
