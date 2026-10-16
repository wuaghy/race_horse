using System.Text;
using System.Text.Json;
using Handover.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Handover.Infrastructure.Messaging;

public sealed class HandoverEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<HandoverEventConsumer> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Handover event consumer is disabled.");
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
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Tracking.TripDeparted", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                try
                {
                    var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
                    await HandleTripDepartedAsync(body, stoppingToken);
                    await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process Tracking.TripDeparted in Handover consumer.");
                    await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, cancellationToken: stoppingToken);
            logger.LogInformation("Handover consumer is listening for Tracking.TripDeparted on {Queue}.", _options.QueueName);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Handover consumer could not connect to RabbitMQ; HTTP handover API remains available.");
        }
    }

    public async Task HandleTripDepartedAsync(string body, CancellationToken cancellationToken = default)
    {
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;

        var tripId = GetGuid(data, "tripId");
        if (tripId is null || tripId == Guid.Empty)
            throw new InvalidOperationException("Tracking.TripDeparted is missing a valid tripId.");

        var actorUserId = GetGuid(data, "departedByUserId")
            ?? GetGuid(data, "createdByUserId")
            ?? GetGuid(data, "actorUserId")
            ?? Guid.Empty;

        var correlationId = root.TryGetProperty("correlationId", out var corr) && Guid.TryParse(corr.GetString(), out var parsedCorr)
            ? parsedCorr
            : Guid.NewGuid();

        var horseIds = new List<Guid>();
        if (data.TryGetProperty("horseIds", out var horsesProp) && horsesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in horsesProp.EnumerateArray())
            {
                if (Guid.TryParse(item.GetString(), out var horseId) && horseId != Guid.Empty)
                    horseIds.Add(horseId);
            }
        }

        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHandoverService>();
        await service.EnsureFromTripDepartedAsync(tripId.Value, actorUserId, horseIds, correlationId, cancellationToken);
    }

    private static Guid? GetGuid(JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
