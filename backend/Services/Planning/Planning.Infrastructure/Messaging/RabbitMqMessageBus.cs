using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planning.Application.Abstractions;
using RabbitMQ.Client;

namespace Planning.Infrastructure.Messaging;

public class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageBus> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqMessageBus(IOptions<RabbitMqOptions> options, ILogger<RabbitMqMessageBus> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(string routingKey, string eventType, string payload, Guid correlationId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[RabbitMQ Disabled] Would publish {EventType} to {RoutingKey}: {Payload}", eventType, routingKey, payload);
            return;
        }

        try
        {
            var channel = await GetChannelAsync(ct);

            // Create standard envelope compliant with docs/INTEGRATION_EVENTS.md
            object dataElement;
            try
            {
                dataElement = JsonDocument.Parse(payload).RootElement;
            }
            catch
            {
                dataElement = payload;
            }

            var envelope = new
            {
                eventId = Guid.NewGuid(),
                eventType = eventType,
                version = 1,
                occurredAt = DateTime.UtcNow,
                correlationId = correlationId,
                source = "planning-service",
                data = dataElement
            };

            var json = JsonSerializer.Serialize(envelope);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = correlationId.ToString(),
                Type = eventType,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct
            );

            _logger.LogInformation("Successfully published event {EventType} with correlation {CorrelationId} to exchange {Exchange}",
                eventType, correlationId, _options.ExchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to RabbitMQ broker.", eventType);
            throw;
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is not null && _channel.IsOpen)
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_channel is not null && _channel.IsOpen)
            {
                return _channel;
            }

            if (_connection is null || !_connection.IsOpen)
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    AutomaticRecoveryEnabled = true
                };

                _connection = await factory.CreateConnectionAsync(ct);
            }

            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            // Declare durable topic exchange for cross-service events
            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct
            );

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }
}
