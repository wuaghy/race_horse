using System.Text;
using System.Text.Json;
using Handover.Application.Services;
using Handover.Domain.Entities;
using Handover.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
            await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(_options.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Tracking.TripDeparted", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Tracking.TripArrived", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Incident.IncidentCreated", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Incident.IncidentResolved", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Booking.RequestApproved", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Planning.TripCreated", cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(_options.QueueName, _options.ExchangeName, "Planning.TripsConsolidated", cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                var eventType = delivery.BasicProperties?.Type ?? delivery.RoutingKey;
                try
                {
                    var body = Encoding.UTF8.GetString(delivery.Body.ToArray());
                    if (eventType == "Tracking.TripDeparted")
                        await HandleTripDepartedAsync(body, stoppingToken);
                    else if (eventType == "Tracking.TripArrived")
                        await HandleTripArrivedAsync(body, stoppingToken);
                    else if (eventType == "Incident.IncidentCreated")
                        await HandleIncidentCreatedAsync(body, stoppingToken);
                    else if (eventType == "Incident.IncidentResolved")
                        await HandleIncidentResolvedAsync(body, stoppingToken);
                    else if (eventType == "Booking.RequestApproved")
                        await HandleRequestApprovedAsync(body, stoppingToken);
                    else if (eventType == "Planning.TripCreated")
                        await HandleTripCreatedAsync(body, stoppingToken);
                    else if (eventType == "Planning.TripsConsolidated")
                        await HandleTripsConsolidatedAsync(body, stoppingToken);
                    await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process {EventType} in Handover consumer.", eventType);
                    await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, cancellationToken: stoppingToken);
            logger.LogInformation("Handover consumer is listening for trip departure, arrival, and incident events on {Queue}.", _options.QueueName);
            await Task.Delay(Timeout.Infinite, stoppingToken);
          }
          catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
          {
              break;
          }
          catch (Exception ex)
          {
              logger.LogWarning(ex, "Handover consumer could not connect to RabbitMQ; retrying in 5 seconds. HTTP handover API remains available.");
              try
              {
                  await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
              }
              catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
              {
                  break;
              }
          }
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

    private async Task HandleTripArrivedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var tripId = GetGuidIgnoreCase(data, "tripId");
        if (tripId is null || tripId == Guid.Empty)
            throw new InvalidOperationException("Tracking.TripArrived is missing a valid tripId.");
        var arrivedAt = GetDateTimeIgnoreCase(data, "arrivedAt") ?? DateTime.UtcNow;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var handover = await db.HandoverRecords.SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (handover is null)
            throw new InvalidOperationException($"Handover draft for arrived trip {tripId} has not been created yet.");
        if (!handover.TripArrivedAt.HasValue || arrivedAt < handover.TripArrivedAt)
        {
            handover.TripArrivedAt = arrivedAt;
            handover.UpdatedAt = DateTime.UtcNow;
            handover.VersionNo++;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleIncidentCreatedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var incidentId = GetGuidIgnoreCase(data, "incidentId");
        var tripId = GetGuidIgnoreCase(data, "tripId");
        if (incidentId is null || tripId is null) throw new InvalidOperationException("Incident.IncidentCreated is missing IDs.");
        var severity = GetStringIgnoreCase(data, "severity")?.ToUpperInvariant() ?? "UNKNOWN";

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var flag = await db.HandoverIncidentFlags.SingleOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken);
        if (flag is null)
        {
            db.HandoverIncidentFlags.Add(new HandoverIncidentFlag { IncidentId = incidentId.Value, TripId = tripId.Value, Severity = severity, Status = "OPEN", UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (flag.Status is not ("RESOLVED" or "CLOSED" or "CANCELLED"))
        {
            flag.Severity = severity;
            flag.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleIncidentResolvedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var incidentId = GetGuidIgnoreCase(data, "incidentId");
        var tripId = GetGuidIgnoreCase(data, "tripId");
        if (incidentId is null || tripId is null) throw new InvalidOperationException("Incident.IncidentResolved is missing IDs.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var flag = await db.HandoverIncidentFlags.SingleOrDefaultAsync(item => item.IncidentId == incidentId, cancellationToken);
        if (flag is null)
            db.HandoverIncidentFlags.Add(new HandoverIncidentFlag { IncidentId = incidentId.Value, TripId = tripId.Value, Severity = "UNKNOWN", Status = "RESOLVED", UpdatedAt = DateTime.UtcNow });
        else
        {
            flag.Status = "RESOLVED";
            flag.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleRequestApprovedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var orderId = GetGuidIgnoreCase(data, "orderId");
        var customerUserId = GetGuidIgnoreCase(data, "customerUserId");
        var receiverName = GetStringIgnoreCase(data, "receiverName");
        var receiverPhone = GetStringIgnoreCase(data, "receiverPhone");
        var receiverEmail = GetStringIgnoreCase(data, "receiverEmail");
        if (orderId is null || customerUserId is null)
            throw new InvalidOperationException("Booking.RequestApproved is missing orderId or customerUserId.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var orderLink = await db.HandoverOrderCustomerLinks.SingleOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
        if (orderLink is null)
            db.HandoverOrderCustomerLinks.Add(new HandoverOrderCustomerLink { OrderId = orderId.Value, CustomerUserId = customerUserId.Value, ReceiverName = receiverName, ReceiverPhone = receiverPhone, ReceiverEmail = receiverEmail });
        else
        {
            orderLink.CustomerUserId = customerUserId.Value;
            orderLink.ReceiverName = receiverName ?? orderLink.ReceiverName;
            orderLink.ReceiverPhone = receiverPhone ?? orderLink.ReceiverPhone;
            orderLink.ReceiverEmail = receiverEmail ?? orderLink.ReceiverEmail;
            orderLink.UpdatedAt = DateTime.UtcNow;
        }
        var tripLinks = await db.HandoverTripCustomerLinks.Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);
        foreach (var tripLink in tripLinks)
        {
            tripLink.CustomerUserId = customerUserId.Value;
            tripLink.ReceiverName = receiverName ?? tripLink.ReceiverName;
            tripLink.ReceiverPhone = receiverPhone ?? tripLink.ReceiverPhone;
            tripLink.ReceiverEmail = receiverEmail ?? tripLink.ReceiverEmail;
            tripLink.UpdatedAt = DateTime.UtcNow;
            var handover = await db.HandoverRecords.SingleOrDefaultAsync(item => item.TripId == tripLink.TripId, cancellationToken);
            if (handover is not null)
            {
                handover.CustomerUserId = customerUserId.Value;
                handover.ReceiverName ??= receiverName;
                handover.ReceiverPhone ??= receiverPhone;
                handover.ReceiverEmail ??= receiverEmail;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleTripCreatedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var tripId = GetGuidIgnoreCase(data, "tripId");
        var orderIds = GetGuidArrayIgnoreCase(data, "orderIds");
        var orderId = GetGuidIgnoreCase(data, "orderId");
        if (orderIds.Length == 0 && orderId.HasValue) orderIds = [orderId.Value];
        if (tripId is null || orderIds.Length == 0)
            throw new InvalidOperationException("Planning.TripCreated is missing tripId or orderIds.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var owner = await db.HandoverOrderCustomerLinks.AsNoTracking()
            .Where(item => orderIds.Contains(item.OrderId))
            .Select(item => new { item.OrderId, item.CustomerUserId, item.ReceiverName, item.ReceiverPhone, item.ReceiverEmail })
            .SingleOrDefaultAsync(cancellationToken);
        var tripLink = await db.HandoverTripCustomerLinks.SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (tripLink is null)
            db.HandoverTripCustomerLinks.Add(new HandoverTripCustomerLink { TripId = tripId.Value, OrderId = owner?.OrderId ?? orderIds[0], CustomerUserId = owner?.CustomerUserId, ReceiverName = owner?.ReceiverName, ReceiverPhone = owner?.ReceiverPhone, ReceiverEmail = owner?.ReceiverEmail, UpdatedAt = DateTime.UtcNow });
        else
        {
            tripLink.OrderId = owner?.OrderId ?? orderIds[0];
            tripLink.CustomerUserId = owner?.CustomerUserId ?? tripLink.CustomerUserId;
            tripLink.ReceiverName ??= owner?.ReceiverName;
            tripLink.ReceiverPhone ??= owner?.ReceiverPhone;
            tripLink.ReceiverEmail ??= owner?.ReceiverEmail;
            tripLink.UpdatedAt = DateTime.UtcNow;
        }
        var handover = await db.HandoverRecords.SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (handover is not null && owner is not null)
        {
            handover.CustomerUserId = owner.CustomerUserId;
            handover.ReceiverName ??= owner.ReceiverName;
            handover.ReceiverPhone ??= owner.ReceiverPhone;
            handover.ReceiverEmail ??= owner.ReceiverEmail;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleTripsConsolidatedAsync(string body, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(body);
        var data = GetData(json.RootElement);
        var tripId = GetGuidIgnoreCase(data, "tripId");
        if (tripId is null) throw new InvalidOperationException("Planning.TripsConsolidated is missing tripId.");
        if (!TryGetPropertyIgnoreCase(data, "orderIds", out var orderIdsElement) || orderIdsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Planning.TripsConsolidated is missing orderIds.");
        var orderIds = orderIdsElement.EnumerateArray().Select(item => Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        if (orderIds.Length < 2) throw new InvalidOperationException("Planning.TripsConsolidated must contain multiple orders.");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var owner = await db.HandoverOrderCustomerLinks.AsNoTracking().Where(item => orderIds.Contains(item.OrderId))
            .OrderBy(item => item.OrderId).Select(item => new { item.OrderId, item.CustomerUserId, item.ReceiverName, item.ReceiverPhone, item.ReceiverEmail })
            .FirstOrDefaultAsync(cancellationToken);
        var tripLink = await db.HandoverTripCustomerLinks.SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (tripLink is null)
        {
            tripLink = new HandoverTripCustomerLink { TripId = tripId.Value, OrderId = owner?.OrderId ?? orderIds[0], UpdatedAt = DateTime.UtcNow };
            db.HandoverTripCustomerLinks.Add(tripLink);
        }
        tripLink.OrderId = owner?.OrderId ?? orderIds[0];
        tripLink.CustomerUserId = owner?.CustomerUserId ?? tripLink.CustomerUserId;
        tripLink.ReceiverName = owner?.ReceiverName ?? tripLink.ReceiverName;
        tripLink.ReceiverPhone = owner?.ReceiverPhone ?? tripLink.ReceiverPhone;
        tripLink.ReceiverEmail = owner?.ReceiverEmail ?? tripLink.ReceiverEmail;
        tripLink.UpdatedAt = DateTime.UtcNow;
        var handover = await db.HandoverRecords.SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        if (handover is not null && owner is not null)
        {
            handover.CustomerUserId = owner.CustomerUserId;
            handover.ReceiverName ??= owner.ReceiverName;
            handover.ReceiverPhone ??= owner.ReceiverPhone;
            handover.ReceiverEmail ??= owner.ReceiverEmail;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static JsonElement GetData(JsonElement root) =>
        TryGetPropertyIgnoreCase(root, "data", out var data) ? data : root;

    private static Guid? GetGuidIgnoreCase(JsonElement data, string property) =>
        TryGetPropertyIgnoreCase(data, property, out var value) && Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;

    private static Guid[] GetGuidArrayIgnoreCase(JsonElement data, string property) =>
        TryGetPropertyIgnoreCase(data, property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => Guid.TryParse(item.GetString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray()
            : [];

    private static string? GetStringIgnoreCase(JsonElement data, string property) =>
        TryGetPropertyIgnoreCase(data, property, out var value) ? value.GetString() : null;

    private static DateTime? GetDateTimeIgnoreCase(JsonElement data, string property) =>
        TryGetPropertyIgnoreCase(data, property, out var value) && value.TryGetDateTime(out var parsed) ? parsed : null;

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

    private static Guid? GetGuid(JsonElement data, string property) =>
        data.TryGetProperty(property, out var value) && Guid.TryParse(value.GetString(), out var parsed) ? parsed : null;

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
