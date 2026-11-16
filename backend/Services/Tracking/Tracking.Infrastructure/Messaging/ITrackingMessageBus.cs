namespace Tracking.Infrastructure.Messaging;

public interface ITrackingMessageBus
{
    Task PublishAsync(Guid eventId, string eventType, string payload, Guid correlationId, DateTime occurredAt, CancellationToken cancellationToken = default);
}
