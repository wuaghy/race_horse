using Booking.Application.Customers;

namespace Booking.Application.TransportRequests;

public sealed record TransportRequestInput(
    Guid OriginLocationId,
    Guid DestinationLocationId,
    DateTimeOffset RequestedDepartureAt,
    DateTimeOffset? RequestedArrivalAt,
    string? PreferredTransportMode,
    string? SpecialRequirements,
    string? Notes,
    IReadOnlyList<Guid> HorseIds,
    int? VersionNo);

public sealed record TransportRequestListItem(
    Guid Id,
    string RequestNo,
    string Status,
    DateTimeOffset RequestedDepartureAt,
    DateTimeOffset CreatedAt,
    int HorseCount,
    int VersionNo);

public sealed record TransportRequestRecord(
    Guid Id,
    string RequestNo,
    Guid CustomerId,
    Guid CreatedByUserId,
    Guid? AssignedManagerUserId,
    Guid? OriginLocationId,
    Guid? DestinationLocationId,
    DateTimeOffset RequestedDepartureAt,
    DateTimeOffset? RequestedArrivalAt,
    string? PreferredTransportMode,
    string? SpecialRequirements,
    string? Notes,
    string Status,
    string? ReviewReason,
    int VersionNo,
    IReadOnlyList<Guid> HorseIds,
    Guid? OrderId,
    string? OrderNo);

public sealed record ManagerDecisionInput(string? Reason);

public interface ITransportRequestService
{
    Task<PageResult<TransportRequestListItem>> GetCustomerRequestsAsync(Guid identityUserId, int page, int pageSize, string? search, CancellationToken cancellationToken);

    Task<TransportRequestRecord?> GetCustomerRequestAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken);

    Task<TransportRequestRecord?> GetManagerRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> CreateDraftAsync(Guid identityUserId, TransportRequestInput input, CancellationToken cancellationToken);

    Task<TransportRequestRecord> UpdateDraftAsync(Guid identityUserId, Guid requestId, TransportRequestInput input, CancellationToken cancellationToken);

    Task<TransportRequestRecord> SubmitAsync(Guid identityUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken);

    Task<PageResult<TransportRequestListItem>> GetManagerQueueAsync(Guid managerUserId, int page, int pageSize, CancellationToken cancellationToken);

    Task<TransportRequestRecord> StartReviewAsync(Guid managerUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> RequestInformationAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> RejectAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> ApproveAsync(Guid managerUserId, Guid requestId, string? note, Guid correlationId, CancellationToken cancellationToken);
}
