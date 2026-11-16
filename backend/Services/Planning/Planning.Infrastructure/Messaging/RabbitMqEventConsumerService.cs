using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planning.Application.DTOs.Planning;
using Planning.Application.Services;
using Planning.Domain;
using Planning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

        while (!stoppingToken.IsCancellationRequested)
        {
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
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Booking.OrderPaymentCompleted", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Booking.OrderCancelled", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Compliance.ComplianceReady", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Handover.HandoverCompleted", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Incident.IncidentCreated", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Incident.IncidentResolved", cancellationToken: stoppingToken);

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

            _logger.LogInformation("Planning RabbitMQ Consumer listening on queue '{Queue}' for Booking order, compliance & handover events.", _options.QueueName);

            // Keep alive until cancelled
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ consumer could not connect. Retrying in 5 seconds.");
            _channel?.Dispose();
            _channel = null;
            _connection?.Dispose();
            _connection = null;
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
        }
    }

    private async Task HandleEventAsync(string eventType, string body, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Event publishers may wrap the integration event in an API envelope,
        // and the integration event payload itself can contain another `data` object.
        var data = TryGetPropertyIgnoreCase(root, "data", out var dataProp) ? dataProp : root;
        if (TryGetPropertyIgnoreCase(data, "data", out var nestedData) && nestedData.ValueKind == JsonValueKind.Object)
        {
            data = nestedData;
        }

        if (eventType == "Booking.OrderCreated")
        {
            if (TryGetPropertyIgnoreCase(data, "orderId", out var orderIdProp) && Guid.TryParse(orderIdProp.GetString(), out var orderId))
            {
                var planningSvc = scope.ServiceProvider.GetRequiredService<ITripPlanningService>();
                await planningSvc.RegisterOrderForDispatchAsync(new CreateTripInput(
                    orderId,
                    GetGuid(data, "requestId"),
                    GetGuid(data, "originLocationId"),
                    GetGuid(data, "destinationLocationId"),
                    GetDateTime(data, "requestedDepartureAt"),
                    GetDateTime(data, "requestedArrivalAt"),
                    GetString(data, "preferredTransportMode"),
                    GetDecimal(data, "customPickupLatitude"),
                    GetDecimal(data, "customPickupLongitude"),
                    GetString(data, "customPickupLabel"),
                    GetGuid(data, "customerId"),
                    GetGuidArray(data, "horseIds"),
                    GetString(data, "receiverName"),
                    GetString(data, "receiverPhone"),
                    GetString(data, "receiverEmail"),
                    GetString(data, "orderNo"),
                    GetString(data, "requestNo"),
                    GetString(data, "customerName"),
                    GetGuidStringDictionary(data, "horseNames"),
                    GetString(data, "paymentStatus") ?? "PAID",
                    GetGuid(data, "customerUserId")), ct);
                _logger.LogInformation("Added Booking.OrderCreated {OrderId} to the Logistics departure-date calendar.", orderId);
            }
        }
        else if (eventType == "Booking.OrderPaymentCompleted")
        {
            if (TryGetPropertyIgnoreCase(data, "orderId", out var orderIdProp) && Guid.TryParse(orderIdProp.GetString(), out var orderId))
            {
                var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
                var order = await db.TripOrders.SingleOrDefaultAsync(item => item.OrderId == orderId, ct);
                if (order is not null && !string.Equals(order.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    order.PaymentStatus = "PAID";
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Marked Booking order {OrderId} paid; it is now eligible for trip assignment.", orderId);
                }
            }
        }
        else if (eventType == "Booking.OrderCancelled")
        {
            if (TryGetPropertyIgnoreCase(data, "orderId", out var orderIdProp) && Guid.TryParse(orderIdProp.GetString(), out var orderId))
            {
                var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
                var memberOrder = await db.TripOrders.SingleOrDefaultAsync(item => item.OrderId == orderId, ct);
                if (memberOrder is not null && memberOrder.TripId is null)
                {
                    db.TripOrders.Remove(memberOrder);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Removed cancelled Order {OrderId} from the Logistics date queue.", orderId);
                    return;
                }
                var trip = memberOrder?.TripId is Guid tripId
                    ? await db.TransportTrips.SingleOrDefaultAsync(item => item.Id == tripId, ct)
                    : await db.TransportTrips.SingleOrDefaultAsync(item => item.OrderId == orderId, ct);
                if (trip is not null && trip.Status == PlanningConstants.TripStatus.Planned)
                {
                    if (memberOrder is not null) db.TripOrders.Remove(memberOrder);
                    var remainingOrders = memberOrder is null ? 0 : await db.TripOrders.CountAsync(item => item.TripId == trip.Id && item.OrderId != orderId, ct);
                    if (remainingOrders == 0) trip.Status = PlanningConstants.TripStatus.Cancelled;
                    trip.CancellationPending = false;
                    trip.UpdatedAt = DateTime.UtcNow;
                    trip.VersionNo++;
                    db.AuditLogs.Add(new AuditLog
                    {
                        EntityType = "TransportTrip",
                        EntityId = trip.Id,
                        Action = "CANCELLED_BY_CUSTOMER",
                        OldState = PlanningConstants.TripStatus.Planned,
                        NewState = PlanningConstants.TripStatus.Cancelled,
                        ActorUserId = Guid.TryParse(TryGetPropertyIgnoreCase(data, "customerUserId", out var actorProp) ? actorProp.GetString() : null, out var actorId) ? actorId : Guid.Empty,
                        Reason = "Customer cancelled the order before trip readiness.",
                        OccurredAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(ct);
                }
                else if (trip is not null)
                {
                    _logger.LogWarning("Order {OrderId} was cancelled in Booking after trip {TripId} reached {TripStatus}; Planning retained its trip state.", orderId, trip.Id, trip.Status);
                }
            }
        }
        else if (eventType == "Incident.IncidentCreated")
        {
            if (TryGetPropertyIgnoreCase(data, "incidentId", out var incidentIdProp) && Guid.TryParse(incidentIdProp.GetString(), out var incidentId) &&
                TryGetPropertyIgnoreCase(data, "tripId", out var incidentTripProp) && Guid.TryParse(incidentTripProp.GetString(), out var incidentTripId))
            {
                var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
                var flag = await db.TripIncidentFlags.SingleOrDefaultAsync(item => item.IncidentId == incidentId, ct);
                var severity = TryGetPropertyIgnoreCase(data, "severity", out var severityProp) ? severityProp.GetString() ?? "UNKNOWN" : "UNKNOWN";
                if (flag is null)
                {
                    db.TripIncidentFlags.Add(new TripIncidentFlag
                    {
                        IncidentId = incidentId,
                        TripId = incidentTripId,
                        Severity = severity.ToUpperInvariant(),
                        Status = "OPEN",
                        UpdatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync(ct);
                }
                else if (flag.Status is not ("RESOLVED" or "CLOSED" or "CANCELLED"))
                {
                    flag.Severity = severity.ToUpperInvariant();
                    flag.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        else if (eventType == "Incident.IncidentResolved")
        {
            if (TryGetPropertyIgnoreCase(data, "incidentId", out var incidentIdProp) && Guid.TryParse(incidentIdProp.GetString(), out var incidentId) &&
                TryGetPropertyIgnoreCase(data, "tripId", out var incidentTripProp) && Guid.TryParse(incidentTripProp.GetString(), out var incidentTripId))
            {
                var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
                var flag = await db.TripIncidentFlags.SingleOrDefaultAsync(item => item.IncidentId == incidentId, ct);
                if (flag is null)
                {
                    db.TripIncidentFlags.Add(new TripIncidentFlag
                    {
                        IncidentId = incidentId,
                        TripId = incidentTripId,
                        Severity = "UNKNOWN",
                        Status = "RESOLVED",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    flag.Status = "RESOLVED";
                    flag.UpdatedAt = DateTime.UtcNow;
                }
                await db.SaveChangesAsync(ct);
            }
        }
        else if (eventType == "Compliance.ComplianceReady")
        {
            if (TryGetPropertyIgnoreCase(data, "tripId", out var tripIdProp) && Guid.TryParse(tripIdProp.GetString(), out var tripId))
            {
                var readinessSvc = scope.ServiceProvider.GetRequiredService<ITripReadinessService>();
                await readinessSvc.SetComplianceReadyAsync(tripId, ct);
                _logger.LogInformation("Auto-marked ComplianceReady for Trip {TripId} from Compliance.ComplianceReady.", tripId);
            }
        }
        else if (eventType == "Handover.HandoverCompleted")
        {
            if (TryGetPropertyIgnoreCase(data, "tripId", out var tripIdProp) && Guid.TryParse(tripIdProp.GetString(), out var tripId)
                && TryGetPropertyIgnoreCase(data, "completedByUserId", out var actorProp) && Guid.TryParse(actorProp.GetString(), out var actorId))
            {
                var db = scope.ServiceProvider.GetRequiredService<PlanningDbContext>();
                var trip = await db.TransportTrips.SingleOrDefaultAsync(x => x.Id == tripId, ct);
                if (trip is null)
                {
                    _logger.LogWarning("Ignoring Handover.HandoverCompleted for unknown Trip {TripId}.", tripId);
                    return;
                }

                if (trip.Status != PlanningConstants.TripStatus.Completed)
                {
                    var oldState = trip.Status;
                    trip.Status = PlanningConstants.TripStatus.Completed;
                    trip.UpdatedAt = DateTime.UtcNow;
                    trip.VersionNo++;
                    db.AuditLogs.Add(new AuditLog
                    {
                        EntityType = "TransportTrip",
                        EntityId = trip.Id,
                        Action = "HANDOVER_COMPLETED",
                        OldState = oldState,
                        NewState = trip.Status,
                        ActorUserId = actorId,
                        Reason = $"Handover {data.GetProperty("handoverNo").GetString()} completed.",
                        CorrelationId = root.TryGetProperty("correlationId", out var correlation) && Guid.TryParse(correlation.GetString(), out var correlationId) ? correlationId : null,
                        OccurredAt = DateTime.UtcNow
                    });
                }

                var completedAt = DateTime.UtcNow;
                var tripOrders = await db.TripOrders.Where(order => order.TripId == trip.Id).ToListAsync(ct);
                foreach (var order in tripOrders) order.CompletedAt ??= completedAt;
                var completedOrderIds = tripOrders.Count > 0 ? tripOrders.Select(order => order.OrderId).Distinct().ToArray() : trip.OrderId is Guid fallbackOrderId ? [fallbackOrderId] : [];
                var customerOrder = tripOrders.FirstOrDefault(order => !string.IsNullOrWhiteSpace(order.ReceiverEmail)) ?? tripOrders.FirstOrDefault();

                // Also recover a previously completed Trip if its original consumer ran before
                // this downstream Order-completion event existed and the handover event is replayed.
                var tripCompletedEventQueued = await db.OutboxMessages.AnyAsync(
                    message => message.EventType == "Planning.TripCompleted" && message.AggregateId == trip.Id,
                    ct);
                if (!tripCompletedEventQueued)
                {
                    var completionCorrelationId = root.TryGetProperty("correlationId", out var eventCorrelation)
                        && Guid.TryParse(eventCorrelation.GetString(), out var parsedCorrelationId)
                        ? parsedCorrelationId
                        : Guid.NewGuid();
                    db.OutboxMessages.Add(new OutboxMessage
                    {
                        EventType = "Planning.TripCompleted",
                        AggregateType = "TransportTrip",
                        AggregateId = trip.Id,
                        Payload = JsonSerializer.Serialize(new
                        {
                            tripId = trip.Id,
                            tripNo = trip.TripNo,
                            orderId = trip.OrderId,
                            orderIds = completedOrderIds,
                            status = PlanningConstants.TripStatus.Completed,
                            requestId = customerOrder?.RequestId ?? trip.RequestId,
                            customerEmail = customerOrder?.ReceiverEmail,
                            requestNo = customerOrder?.RequestNo,
                            orderNo = customerOrder?.OrderNo
                        }),
                        CorrelationId = completionCorrelationId,
                        OccurredAt = DateTime.UtcNow,
                        Status = "PENDING",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (db.ChangeTracker.HasChanges())
                    await db.SaveChangesAsync(ct);

                _logger.LogInformation("Marked Trip {TripId} completed from Handover.HandoverCompleted.", tripId);
            }
            else
            {
                throw new InvalidOperationException("Handover.HandoverCompleted is missing tripId or completedByUserId.");
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
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

    private static Guid? GetGuid(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var value) && Guid.TryParse(value.GetString(), out var id) ? id : null;

    private static IReadOnlyList<Guid> GetGuidArray(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Select(item => Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
    }

    private static IReadOnlyDictionary<Guid, string> GetGuidStringDictionary(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Object) return new Dictionary<Guid, string>();
        var result = new Dictionary<Guid, string>();
        foreach (var property in value.EnumerateObject())
            if (Guid.TryParse(property.Name, out var id) && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { Length: > 0 } name)
                result[id] = name;
        return result;
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out var dateTime)
            ? dateTime
            : null;

    private static decimal? GetDecimal(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number
            : null;

    private static string? GetString(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
