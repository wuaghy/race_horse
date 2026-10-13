namespace Notification.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientUserId { get; set; }

    /// <summary>
    /// Type: REQUEST_APPROVED, DOCUMENT_NEED_INFO, TRIP_DEPARTED, HEALTH_ALERT, INCIDENT, ROUTE_CHANGED, HANDOVER_READY
    /// </summary>
    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// ReferenceType: TransportRequest, Trip, Incident, etc.
    /// </summary>
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    /// <summary>
    /// Status: PENDING, SENT, READ, FAILED
    /// </summary>
    public string Status { get; set; } = "SENT";

    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
