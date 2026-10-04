namespace Compliance.Application.Abstractions;

public interface IMessageBus
{
    Task PublishAsync(string routingKey, string eventType, string payload, Guid correlationId, CancellationToken cancellationToken = default);
}
