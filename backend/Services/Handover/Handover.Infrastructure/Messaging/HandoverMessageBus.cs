using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Handover.Infrastructure.Messaging;

public sealed class HandoverMessageBus(IOptions<RabbitMqOptions> options, ILogger<HandoverMessageBus> logger) : IHandoverMessageBus, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(Guid eventId, string eventType, string payload, Guid correlationId, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ disabled; leaving outbox event {EventId} pending for {EventType}.", eventId, eventType);
            throw new InvalidOperationException("RabbitMQ publishing is disabled; outbox message was not delivered.");
        }

        var channel = await GetChannelAsync(cancellationToken);
        using var dataDocument = JsonDocument.Parse(payload);
        var envelope = JsonSerializer.SerializeToUtf8Bytes(new
        {
            eventId,
            eventType,
            version = 1,
            occurredAt,
            correlationId,
            source = "handover-service",
            data = dataDocument.RootElement
        });

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = correlationId.ToString(),
            Type = eventType,
            MessageId = eventId.ToString(),
            Timestamp = new AmqpTimestamp(new DateTimeOffset(occurredAt).ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(_options.ExchangeName, eventType, mandatory: false, basicProperties: properties, body: envelope, cancellationToken: cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true }) return _channel;
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    AutomaticRecoveryEnabled = true
                };
                _connection = await factory.CreateConnectionAsync(cancellationToken);
            }

            _channel = await _connection!.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) { await _channel.CloseAsync(); _channel.Dispose(); }
        if (_connection is not null) { await _connection.CloseAsync(); _connection.Dispose(); }
        _connectionLock.Dispose();
    }
}
