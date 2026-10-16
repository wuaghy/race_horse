namespace Handover.Domain.Entities;

public static class HandoverStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingAcceptance = "PENDING_ACCEPTANCE";
    public const string Accepted = "ACCEPTED";
    public const string Disputed = "DISPUTED";
    public const string Completed = "COMPLETED";

    public static bool IsValid(string value) =>
        value is Draft or PendingAcceptance or Accepted or Disputed or Completed;
}

public static class HandoverHorseStatuses
{
    public const string Pending = "PENDING";
    public const string Accepted = "ACCEPTED";
    public const string Disputed = "DISPUTED";

    public static bool IsValid(string value) =>
        value is Pending or Accepted or Disputed;
}

public sealed class HandoverRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HandoverNo { get; set; } = null!;
    public Guid TripId { get; set; }
    public string Status { get; set; } = HandoverStatuses.Draft;
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public string? ReceiverEmail { get; set; }
    public Guid? HandoverLocationId { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ActualAt { get; set; }
    public string? Notes { get; set; }
    public string? SignatureUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
    public List<HandoverHorse> Horses { get; set; } = [];
}

public sealed class HandoverHorse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HandoverId { get; set; }
    public HandoverRecord Handover { get; set; } = null!;
    public Guid HorseId { get; set; }
    public string Status { get; set; } = HandoverHorseStatuses.Pending;
    public string? Condition { get; set; }
    public string? IssueNote { get; set; }
    public string? EvidenceImageUrl { get; set; }
    public DateTime? AcceptedAt { get; set; }
}

public sealed class TripCostItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = "PENDING";
    public DateTime? IncurredAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TripRevenueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public string Category { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public string Status { get; set; } = "PENDING";
    public DateTime? RecognizedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HandoverOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = null!;
    public string AggregateType { get; set; } = "HandoverRecord";
    public Guid AggregateId { get; set; }
    public string Payload { get; set; } = null!;
    public Guid CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "PENDING";
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class HandoverAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = "HandoverRecord";
    public Guid EntityId { get; set; }
    public string Action { get; set; } = null!;
    public string? OldState { get; set; }
    public string? NewState { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
