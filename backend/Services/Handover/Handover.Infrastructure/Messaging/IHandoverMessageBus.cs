namespace Handover.Infrastructure.Messaging;

public interface IHandoverMessageBus
{
    Task PublishAsync(Guid eventId, string eventType, string payload, Guid correlationId, DateTime occurredAt, CancellationToken cancellationToken = default);
}
