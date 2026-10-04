namespace Compliance.Domain.Entities;

public class DocumentReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    public Document? Document { get; set; }
}
