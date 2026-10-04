namespace Contracts.IntegrationEvents;

public sealed record BookingRequestApprovedData(Guid RequestId, Guid CustomerId, string RequestNo, Guid OrderId);

public sealed record BookingRequestApprovedEvent : IntegrationEvent<BookingRequestApprovedData>
{
    public override string EventType => "Booking.RequestApproved";
    public override string Source => "booking-service";
}

public sealed record BookingOrderCreatedData(Guid OrderId, string OrderNo, Guid RequestId, Guid CustomerId);

public sealed record BookingOrderCreatedEvent : IntegrationEvent<BookingOrderCreatedData>
{
    public override string EventType => "Booking.OrderCreated";
    public override string Source => "booking-service";
}