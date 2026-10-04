namespace Compliance.Domain.Entities;

public class ClearanceCaseDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClearanceCaseId { get; set; }
    public Guid DocumentId { get; set; }
    public bool Required { get; set; } = true;
    public DateTime? SubmittedAt { get; set; }
    public string Status { get; set; } = DocumentStatus.Uploaded;

    public ClearanceCase? ClearanceCase { get; set; }
    public Document? Document { get; set; }
}
