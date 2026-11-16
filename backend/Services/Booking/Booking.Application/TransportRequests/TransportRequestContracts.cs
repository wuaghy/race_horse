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
    int? VersionNo,
    string? DeliveryPriority = "STANDARD",
    bool RecipientIsRequester = true,
    string? RecipientName = null,
    string? RecipientPhone = null,
    string? RecipientEmail = null,
    decimal? CustomPickupLatitude = null,
    decimal? CustomPickupLongitude = null,
    string? CustomPickupLabel = null);

public sealed record TransportRequestListItem(
    Guid Id,
    string RequestNo,
    string Status,
    DateTimeOffset RequestedDepartureAt,
    DateTimeOffset CreatedAt,
    int HorseCount,
    int VersionNo,
    string HorseNames);

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
    string? OrderNo)
{
    public IReadOnlyList<TransportRequestHorse> Horses { get; init; } = [];
    public string DeliveryPriority { get; init; } = "STANDARD";
    public bool RecipientIsRequester { get; init; } = true;
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientEmail { get; init; }
    public decimal? CustomPickupLatitude { get; init; }
    public decimal? CustomPickupLongitude { get; init; }
    public string? CustomPickupLabel { get; init; }
}

public sealed record TransportRequestHorse(
    Guid Id,
    string HorseCode,
    string HorseName,
    string? Breed,
    string? Sex,
    DateOnly? DateOfBirth,
    string? Color,
    Guid? CountryOfOriginId,
    string? SpecialRequirements,
    string Status);

public sealed record OrderStatusSummary(Guid Id, string OrderNo, Guid RequestId, string Status, DateTimeOffset UpdatedAt);

public sealed record TransportRequestHistoryEntry(Guid Id, string Action, string? OldState, string? NewState, Guid ActorUserId, string? Reason, DateTimeOffset OccurredAt);

public sealed record BookingLeadTimeInfo(int MinimumLeadTimeDays, DateTimeOffset EarliestDepartureAt);

public sealed record TransportRequestDocumentChecklistItem(Guid HorseId, string DocumentTypeCode, string DocumentTypeName, bool Met, bool Uploaded, string Status, string? DocumentNo, DateOnly? ExpiryDate);

public sealed record TransportRequestDocumentChecklist(bool Ready, IReadOnlyList<TransportRequestDocumentChecklistItem> Items);

public sealed record CustomerHorseAvailability(Guid HorseId, bool Eligible, string HorseStatus, string? Blocker);

public sealed record ManagerDecisionInput(string? Reason);
public sealed record CustomerCancellationRequest(string? Reason);

public interface ITransportRequestService
{
    Task<OrderStatusSummary?> GetOrderStatusAsync(Guid orderId, Guid? customerIdentityUserId, CancellationToken cancellationToken);

    Task<OrderStatusSummary?> CancelOrderAsync(Guid orderId, Guid identityUserId, string? reason, Guid correlationId, CancellationToken cancellationToken);

    Task<PageResult<TransportRequestListItem>> GetCustomerRequestsAsync(Guid identityUserId, int page, int pageSize, string? search, string? status, string? sortBy, string? sortDirection, CancellationToken cancellationToken);

    Task<TransportRequestRecord?> GetCustomerRequestAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken);

    BookingLeadTimeInfo GetBookingLeadTime(Guid originLocationId, Guid destinationLocationId);

    Task<IReadOnlyList<CustomerHorseAvailability>> GetCustomerHorseAvailabilityAsync(Guid identityUserId, IReadOnlyList<Guid> horseIds, Guid? requestId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TransportRequestHistoryEntry>> GetCustomerRequestHistoryAsync(Guid identityUserId, Guid requestId, CancellationToken cancellationToken);


    Task<TransportRequestRecord?> GetManagerRequestAsync(Guid requestId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> CreateDraftAsync(Guid identityUserId, TransportRequestInput input, Guid? idempotencyKey, CancellationToken cancellationToken);

    Task<TransportRequestRecord> UpdateDraftAsync(Guid identityUserId, Guid requestId, TransportRequestInput input, CancellationToken cancellationToken);

    Task<TransportRequestRecord> SubmitAsync(Guid identityUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> CancelCustomerRequestAsync(Guid identityUserId, Guid requestId, string? reason, Guid correlationId, CancellationToken cancellationToken);

    Task<PageResult<TransportRequestListItem>> GetManagerQueueAsync(Guid managerUserId, int page, int pageSize, string? search, string? status, string? sortBy, string? sortDirection, CancellationToken cancellationToken);

    Task<TransportRequestRecord> StartReviewAsync(Guid managerUserId, Guid requestId, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> RequestInformationAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> RejectAsync(Guid managerUserId, Guid requestId, string reason, Guid correlationId, CancellationToken cancellationToken);

    Task<TransportRequestRecord> ApproveAsync(Guid managerUserId, Guid requestId, string? note, Guid correlationId, CancellationToken cancellationToken, string? managerName = null);
}
