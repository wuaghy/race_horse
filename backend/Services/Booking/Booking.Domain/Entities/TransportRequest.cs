namespace Booking.Domain.Entities;

public sealed class TransportRequest
{
    public Guid Id { get; set; }
    public string RequestNo { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedManagerUserId { get; set; }
    public Guid? OriginLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public DateTimeOffset RequestedDepartureAt { get; set; }
    public DateTimeOffset? RequestedArrivalAt { get; set; }
    public string? PreferredTransportMode { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string? ReviewReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;
    public Guid? OrderId { get; set; }
    public string? OrderNo { get; set; }

    public void ValidateForSubmission()
    {
        if (OriginLocationId == Guid.Empty)
        {
            throw new InvalidOperationException("Origin location is required.");
        }

        if (DestinationLocationId == Guid.Empty)
        {
            throw new InvalidOperationException("Destination location is required.");
        }

        if (RequestedDepartureAt == default)
        {
            throw new InvalidOperationException("Requested departure date/time is required.");
        }
    }

    public void Submit(Guid correlationId)
    {
        ValidateForSubmission();
        Status = "SUBMITTED";
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }

    public void StartReview(Guid managerUserId, Guid correlationId)
    {
        if (Status != "SUBMITTED")
        {
            throw new InvalidOperationException("Only submitted requests can be reviewed.");
        }

        AssignedManagerUserId = managerUserId;
        Status = "UNDER_REVIEW";
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }

    public void RequestInformation(string reason, Guid correlationId)
    {
        if (Status != "UNDER_REVIEW")
        {
            throw new InvalidOperationException("Can only request information for requests under review.");
        }

        Status = "INFO_REQUESTED";
        ReviewReason = reason;
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }

    public void Approve(string? note, Guid correlationId)
    {
        if (Status != "UNDER_REVIEW")
        {
            throw new InvalidOperationException("Can only approve requests under review.");
        }

        Status = "APPROVED";
        if (!string.IsNullOrWhiteSpace(note))
        {
            ReviewReason = note;
        }
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }

    public void Reject(string reason, Guid correlationId)
    {
        if (Status != "UNDER_REVIEW")
        {
            throw new InvalidOperationException("Can only reject requests under review.");
        }

        Status = "REJECTED";
        ReviewReason = reason;
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }
}
