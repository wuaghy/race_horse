using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Planning.Infrastructure.Messaging;

public class RabbitMqEventConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEventConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RabbitMQ Consumer is disabled by configuration.");
            return;
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare topic exchange
            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken
            );

            // Declare inbound queue for Planning
            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken
            );

            // Bind events consumed by Planning (per docs/INTEGRATION_EVENTS.md)
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Booking.OrderCreated", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Compliance.ComplianceReady", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var eventType = ea.BasicProperties?.Type ?? ea.RoutingKey;

                    _logger.LogInformation("Planning Consumer received message with routing key '{RoutingKey}': {Body}", ea.RoutingKey, body);

                    await HandleEventAsync(eventType, body, stoppingToken);

                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message from {RoutingKey}. Re-queuing.", ea.RoutingKey);
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: _options.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("Planning RabbitMQ Consumer listening on queue '{Queue}' for Booking.OrderCreated & Compliance.ComplianceReady.", _options.QueueName);

            // Keep alive until cancelled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ Consumer failed to start or connect. Planning will continue operating with HTTP fallback.");
        }
    }

    private async Task HandleEventAsync(string eventType, string body, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Try extracting inner 'data' envelope or directly from root
        var data = root.TryGetProperty("data", out var dataProp) ? dataProp : root;

        if (eventType == "Booking.OrderCreated")
        {
            if (data.TryGetProperty("orderId", out var orderIdProp) && Guid.TryParse(orderIdProp.GetString(), out var orderId))
            {
                var planningSvc = scope.ServiceProvider.GetRequiredService<ITripPlanningService>();
                var trip = await planningSvc.CreateTripAsync(new CreateTripInput(orderId), ct);
                _logger.LogInformation("Auto-created Trip {TripNo} (Id: {TripId}) from Booking.OrderCreated {OrderId}.",
                    trip.TripNo, trip.Id, orderId);
            }
        }
        else if (eventType == "Compliance.ComplianceReady")
        {
            if (data.TryGetProperty("tripId", out var tripIdProp) && Guid.TryParse(tripIdProp.GetString(), out var tripId))
            {
                var readinessSvc = scope.ServiceProvider.GetRequiredService<ITripReadinessService>();
                await readinessSvc.SetComplianceReadyAsync(tripId, ct);
                _logger.LogInformation("Auto-marked ComplianceReady for Trip {TripId} from Compliance.ComplianceReady.", tripId);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
