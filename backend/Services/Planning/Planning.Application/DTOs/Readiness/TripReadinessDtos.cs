namespace Planning.Application.DTOs.Readiness;

public record TripReadinessResponse(
    Guid TripId,
    bool Ready,
    IReadOnlyList<string> Blockers
);

public record ConfirmReadyRequest(
    Guid ActorUserId
);
