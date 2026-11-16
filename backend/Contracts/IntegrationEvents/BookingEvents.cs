namespace Contracts.IntegrationEvents;

public sealed record BookingRequestApprovedData(
    Guid RequestId,
    Guid CustomerId,
    string RequestNo,
    Guid OrderId,
    Guid CustomerUserId,
    string? ReceiverName = null,
    string? ReceiverPhone = null,
    string? ReceiverEmail = null,
    string? CustomerEmail = null,
    IReadOnlyList<string>? HorseNames = null,
    string? ApprovedBy = null,
    DateTime? ApprovedAt = null,
    string? OrderNo = null,
    DateTime? RequestedDepartureAt = null,
    string? ApprovalNote = null);

public sealed record BookingRequestApprovedEvent : IntegrationEvent<BookingRequestApprovedData>
{
    public override string EventType => "Booking.RequestApproved";
    public override string Source => "booking-service";
}

public sealed record BookingOrderCreatedData(
    Guid OrderId,
    string OrderNo,
    Guid RequestId,
    Guid CustomerId,
    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    DateTime? RequestedDepartureAt = null,
    DateTime? RequestedArrivalAt = null,
    string? PreferredTransportMode = null,
    decimal? CustomPickupLatitude = null,
    decimal? CustomPickupLongitude = null,
    string? CustomPickupLabel = null,
    IReadOnlyList<Guid>? HorseIds = null,
    string? ReceiverName = null,
    string? ReceiverPhone = null,
    string? ReceiverEmail = null,
    string? RequestNo = null,
    string? CustomerName = null,
    IReadOnlyDictionary<Guid, string>? HorseNames = null,
    string PaymentStatus = "PAID",
    Guid? CustomerUserId = null);

public sealed record BookingOrderCreatedEvent : IntegrationEvent<BookingOrderCreatedData>
{
    public override string EventType => "Booking.OrderCreated";
    public override string Source => "booking-service";
}

public sealed record BookingOrderPaymentCompletedData(
    Guid OrderId,
    DateTime PaidAt,
    Guid? RequestId = null,
    Guid? CustomerUserId = null,
    string? CustomerEmail = null,
    string? RequestNo = null,
    string? OrderNo = null,
    string PaymentMethod = "PAYOS",
    string? PaymentReference = null);

public sealed record BookingOrderPaymentCompletedEvent : IntegrationEvent<BookingOrderPaymentCompletedData>
{
    public override string EventType => "Booking.OrderPaymentCompleted";
    public override string Source => "booking-service";
}

public sealed record BookingOrderCancelledData(Guid OrderId, Guid RequestId, Guid CustomerUserId, string? Reason);

public sealed record BookingOrderCancelledEvent : IntegrationEvent<BookingOrderCancelledData>
{
    public override string EventType => "Booking.OrderCancelled";
    public override string Source => "booking-service";
}

public sealed record BookingRequestDecisionData(Guid RequestId, Guid RecipientUserId, string RequestNo, string Status, string? Reason, string? RecipientEmail = null);

public sealed record BookingRequestDecisionEvent : IntegrationEvent<BookingRequestDecisionData>
{
    public override string EventType => "Booking.RequestDecisionChanged";
    public override string Source => "booking-service";
}
