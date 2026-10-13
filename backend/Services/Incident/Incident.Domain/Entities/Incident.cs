namespace Incident.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IncidentNo { get; set; } = string.Empty;
    public Guid TripId { get; set; }
    public Guid? RouteLegId { get; set; }
    public Guid CheckpointId { get; set; }
    public Guid? HorseId { get; set; }
    public Guid ReportedByUserId { get; set; }

    /// <summary>
    /// Type: VEHICLE_BREAKDOWN, TRAFFIC_DELAY, WEATHER, HORSE_MEDICAL, BORDER_ISSUE, OTHER
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Severity: LOW, MEDIUM, HIGH, CRITICAL
    /// </summary>
    public string Severity { get; set; } = "MEDIUM";

    /// <summary>
    /// Status: OPEN, INVESTIGATING, RESOLVED, CANCELLED, CLOSED
    /// </summary>
    public string Status { get; set; } = "OPEN";

    public Guid? LocationId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;

    public ICollection<IncidentAttachment> Attachments { get; set; } = new List<IncidentAttachment>();
}
