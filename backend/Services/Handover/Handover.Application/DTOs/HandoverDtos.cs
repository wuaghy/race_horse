namespace Handover.Application.DTOs;

public sealed record HandoverHorseDto(
    Guid Id,
    Guid HandoverId,
    Guid HorseId,
    string Status,
    string? Condition,
    string? IssueNote,
    string? EvidenceImageUrl,
    DateTime? AcceptedAt);

public sealed record HandoverDto(
    Guid Id,
    string HandoverNo,
    Guid TripId,
    string Status,
    string? ReceiverName,
    string? ReceiverPhone,
    string? ReceiverEmail,
    Guid? HandoverLocationId,
    DateTime? ScheduledAt,
    DateTime? ActualAt,
    string? Notes,
    string? SignatureUrl,
    Guid CreatedByUserId,
    Guid? CompletedByUserId,
    int VersionNo,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool CanComplete,
    IReadOnlyList<string> CompletionBlockers,
    IReadOnlyList<HandoverHorseDto> Horses);

public sealed record HandoverCompletionCheckDto(
    Guid HandoverId,
    Guid TripId,
    bool ReadyToComplete,
    IReadOnlyList<string> Blockers);

public sealed record HandoverFilter(
    Guid? TripId,
    string? Status,
    int Page,
    int PageSize,
    Guid? CreatedByUserId = null);

public sealed record HandoverListResult(
    IReadOnlyList<HandoverDto> Items,
    long TotalItems,
    int Page,
    int PageSize);

public sealed record CreateHandoverHorseItem(
    Guid HorseId,
    string? Condition = null,
    string? IssueNote = null);

public sealed record CreateHandoverRequest(
    Guid TripId,
    string? ReceiverName = null,
    string? ReceiverPhone = null,
    string? ReceiverEmail = null,
    Guid? HandoverLocationId = null,
    DateTime? ScheduledAt = null,
    string? Notes = null,
    IReadOnlyList<CreateHandoverHorseItem>? Horses = null);

public sealed record UpdateHandoverRequest(
    string? ReceiverName,
    string? ReceiverPhone,
    string? ReceiverEmail,
    Guid? HandoverLocationId,
    DateTime? ScheduledAt,
    string? Notes,
    int? ExpectedVersionNo = null);

public sealed record UpsertHandoverHorseRequest(
    Guid HorseId,
    string? Condition = null,
    string? IssueNote = null,
    int? ExpectedVersionNo = null);

public sealed record InspectHandoverHorseRequest(
    string Condition,
    string? IssueNote = null,
    int? ExpectedVersionNo = null);

public sealed record SubmitHandoverRequest(
    string? Notes = null,
    int? ExpectedVersionNo = null);

public sealed record AcceptHandoverHorseRequest(
    string? Condition = null,
    string? Note = null,
    int? ExpectedVersionNo = null);

public sealed record DisputeHandoverHorseRequest(
    string IssueNote,
    string? Condition = null,
    int? ExpectedVersionNo = null);

public sealed record ResolveHandoverHorseDisputeRequest(
    string ResolutionNote,
    string? Condition = null,
    int? ExpectedVersionNo = null);

public sealed record CompleteHandoverRequest(
    DateTime? ActualAt = null,
    string? Notes = null,
    int? ExpectedVersionNo = null);
