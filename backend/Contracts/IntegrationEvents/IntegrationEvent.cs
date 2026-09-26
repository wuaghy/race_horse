using System.Text.Json.Serialization;

namespace Contracts.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }
    string EventType { get; }
    int Version { get; }
    DateTime OccurredAt { get; }
    Guid CorrelationId { get; }
    string Source { get; }
}

public abstract record IntegrationEvent<TData> : IIntegrationEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; } = Guid.NewGuid();

    [JsonPropertyName("eventType")]
    public abstract string EventType { get; }

    [JsonPropertyName("version")]
    public int Version { get; init; } = 1;

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("correlationId")]
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    [JsonPropertyName("source")]
    public abstract string Source { get; }

    [JsonPropertyName("data")]
    public required TData Data { get; init; }
}
