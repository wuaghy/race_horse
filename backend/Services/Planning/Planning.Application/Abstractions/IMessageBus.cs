namespace Planning.Application.Abstractions;

public interface IMessageBus
{
    Task PublishAsync(string routingKey, string eventType, string payload, Guid correlationId, CancellationToken ct = default);
}
