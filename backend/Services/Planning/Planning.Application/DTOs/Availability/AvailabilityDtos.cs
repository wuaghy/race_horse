namespace Planning.Application.DTOs.Availability;

public record AvailabilityResponse(
    bool Available,
    DateTime From,
    DateTime To,
    string? Reason = null
);
