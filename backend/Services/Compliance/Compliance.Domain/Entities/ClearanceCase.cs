namespace Compliance.Domain.Entities;

public class ClearanceCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CaseNo { get; set; } = string.Empty;
    public Guid TripId { get; set; }
    public Guid? RouteLegId { get; set; }
    public Guid CountryId { get; set; }
    public Guid? BorderPointId { get; set; }
    public string AuthorityName { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = ClearanceStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;

    public ICollection<ClearanceCaseDocument> ClearanceCaseDocuments { get; set; } = new List<ClearanceCaseDocument>();
}

public static class ClearanceStatus
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string NeedAdditionalInfo = "NEED_ADDITIONAL_INFO";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";

    public static readonly string[] All = [Draft, Submitted, UnderReview, NeedAdditionalInfo, Approved, Rejected, Completed];
}
