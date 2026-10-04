namespace Compliance.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentNo { get; set; } = string.Empty;
    public Guid DocumentTypeId { get; set; }
    public Guid? HorseId { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? TripId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string Status { get; set; } = DocumentStatus.Uploaded;
    public string? CurrentReviewNote { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;

    public DocumentType? DocumentType { get; set; }
    public ICollection<DocumentReview> Reviews { get; set; } = new List<DocumentReview>();
    public ICollection<ClearanceCaseDocument> ClearanceCaseDocuments { get; set; } = new List<ClearanceCaseDocument>();

    public bool IsExpired(DateOnly? asOfDate = null)
    {
        var checkDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return ExpiryDate.HasValue && ExpiryDate.Value < checkDate;
    }
}

public static class DocumentStatus
{
    public const string Uploaded = "UPLOADED";
    public const string UnderReview = "UNDER_REVIEW";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string NeedAdditionalInfo = "NEED_ADDITIONAL_INFO";

    public static readonly string[] All = [Uploaded, UnderReview, Approved, Rejected, NeedAdditionalInfo];
}
