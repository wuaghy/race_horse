using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Booking.Infrastructure.Messaging;

public sealed class BookingEventConsumer : BackgroundService
{
    private const string TripCompletedEventType = "Planning.TripCompleted";
    private readonly string _connectionString;
    private readonly ConnectionFactory _connectionFactory;
    private readonly string _exchangeName;
    private readonly string _queueName;
    private readonly bool _enabled;
    private readonly ILogger<BookingEventConsumer> _logger;

    public BookingEventConsumer(IConfiguration configuration, ILogger<BookingEventConsumer> logger)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            Port = configuration.GetValue("RabbitMQ:Port", 5672),
            AutomaticRecoveryEnabled = true
        };
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "racehorse.events";
        _queueName = configuration["RabbitMQ:BookingQueueName"] ?? "booking-service.inbox";
        _enabled = configuration.GetValue("RabbitMQ:Enabled", true);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Booking RabbitMQ consumer is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
                channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
                channel.QueueBind(_queueName, _exchangeName, TripCompletedEventType);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (_, delivery) => ConsumeDelivery(channel, delivery, stoppingToken);
                channel.BasicConsume(_queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("Booking RabbitMQ consumer listening on queue {Queue} for {EventType}.", _queueName, TripCompletedEventType);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Booking RabbitMQ consumer is waiting for the broker.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void ConsumeDelivery(IModel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            var body = System.Text.Encoding.UTF8.GetString(delivery.Body.ToArray());
            var eventType = delivery.BasicProperties.Type ?? delivery.RoutingKey;
            if (!string.Equals(eventType, TripCompletedEventType, StringComparison.Ordinal))
            {
                _logger.LogWarning("Booking received unsupported event {EventType}; acknowledging it.", eventType);
                channel.BasicAck(delivery.DeliveryTag, multiple: false);
                return;
            }

            var (tripId, orderId) = ReadCompletionIds(body);
            UpdateOrderCompletedAsync(orderId, tripId, cancellationToken).GetAwaiter().GetResult();
            channel.BasicAck(delivery.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Booking failed to process {EventType}; message will be requeued.", TripCompletedEventType);
            channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task UpdateOrderCompletedAsync(Guid orderId, Guid tripId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using var update = new SqlCommand(
            "UPDATE dbo.TransportOrders " +
            "SET Status = 'COMPLETED', UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
            "WHERE Id = @OrderId AND Status NOT IN ('COMPLETED', 'CANCELLED');",
            connection,
            transaction);
        update.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
        var changed = await update.ExecuteNonQueryAsync(cancellationToken);

        if (changed == 0)
        {
            await using var read = new SqlCommand(
                "SELECT Status FROM dbo.TransportOrders WHERE Id = @OrderId;",
                connection,
                transaction);
            read.Parameters.Add("@OrderId", SqlDbType.UniqueIdentifier).Value = orderId;
            var currentStatus = (string?)await read.ExecuteScalarAsync(cancellationToken);
            if (currentStatus is null)
                throw new InvalidOperationException($"Trip {tripId} completed, but Order {orderId} was not found.");
            if (currentStatus == "CANCELLED")
                _logger.LogWarning("Not completing cancelled Order {OrderId} for completed Trip {TripId}.", orderId, tripId);
            else
                _logger.LogInformation("Order {OrderId} was already completed for Trip {TripId}; duplicate event is harmless.", orderId, tripId);
        }
        else
        {
            _logger.LogInformation("Marked Order {OrderId} completed from TripCompleted event for Trip {TripId}.", orderId, tripId);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static (Guid TripId, Guid OrderId) ReadCompletionIds(string body)
    {
        using var document = JsonDocument.Parse(body);
        var data = FindProperty(document.RootElement, "data");
        if (data.ValueKind == JsonValueKind.Object && TryFindProperty(data, "data", out var nestedData) && nestedData.ValueKind == JsonValueKind.Object)
            data = nestedData;

        if (!Guid.TryParse(FindProperty(data, "tripId").GetString(), out var tripId) ||
            !Guid.TryParse(FindProperty(data, "orderId").GetString(), out var orderId))
            throw new InvalidOperationException("Planning.TripCompleted must include valid tripId and orderId values.");

        return (tripId, orderId);
    }

    private static JsonElement FindProperty(JsonElement element, string propertyName) =>
        TryFindProperty(element, propertyName, out var value)
            ? value
            : throw new InvalidOperationException($"Event is missing '{propertyName}'.");

    private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
