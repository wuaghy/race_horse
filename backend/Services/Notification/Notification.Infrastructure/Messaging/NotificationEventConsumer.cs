using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions;
using Notification.Domain.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Notification.Infrastructure.Messaging;

public class NotificationEventConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<NotificationEventConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationEventConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<NotificationEventConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RabbitMQ consumer is disabled in configuration.");
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

            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: stoppingToken);

            await _channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            // Bind to system events
            string[] routingKeys =
            [
                "Booking.*",
                "Compliance.*",
                "Tracking.*",
                "Incident.*"
            ];

            foreach (var routingKey in routingKeys)
            {
                await _channel.QueueBindAsync(
                    queue: _options.QueueName,
                    exchange: _options.ExchangeName,
                    routingKey: routingKey,
                    cancellationToken: stoppingToken);
            }

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var success = await ProcessEventAsync(json, stoppingToken);

                    if (success)
                    {
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    else
                    {
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received message in NotificationEventConsumer.");
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                }
            };

            await _channel.BasicConsumeAsync(
                queue: _options.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            _logger.LogInformation("NotificationEventConsumer is actively listening on queue {QueueName}.", _options.QueueName);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NotificationEventConsumer stopping due to cancellation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error running NotificationEventConsumer.");
        }
    }

    public async Task<bool> ProcessEventAsync(string messageJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("eventId", out var eventIdElem) ||
            !root.TryGetProperty("eventType", out var eventTypeElem))
        {
            _logger.LogWarning("Malformed event payload received: {Payload}", messageJson);
            return true; // ACK malformed so it does not poison queue
        }

        var eventId = eventIdElem.GetGuid();
        var eventType = eventTypeElem.GetString() ?? string.Empty;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<INotificationDbContext>();

        // Idempotency check: InboxMessages
        var alreadyProcessed = await db.InboxMessages.AnyAsync(m => m.EventId == eventId, ct);
        if (alreadyProcessed)
        {
            _logger.LogInformation("Event {EventId} ({EventType}) already processed in inbox. Dropping duplicate.", eventId, eventType);
            return true;
        }

        var dataElem = root.TryGetProperty("data", out var d) ? d : default;

        // Process notification depending on event type
        var notifications = GenerateNotificationsForEvent(eventType, dataElem);

        foreach (var notification in notifications)
        {
            db.Notifications.Add(notification);
        }

        db.InboxMessages.Add(new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            ProcessedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Processed event {EventType} ({EventId}) and created {Count} notifications.", eventType, eventId, notifications.Count);

        return true;
    }

    public static List<Notification.Domain.Entities.Notification> GenerateNotificationsForEvent(string eventType, JsonElement data)
    {
        var list = new List<Notification.Domain.Entities.Notification>();

        // Default admin / coordinator system recipient fallback
        var systemCoordinatorId = Guid.Parse("22222222-2222-4222-8222-222222222222");

        switch (eventType)
        {
            case "Tracking.HealthAlertRaised":
                {
                    var horseId = data.TryGetProperty("horseId", out var h) && h.ValueKind == JsonValueKind.String && Guid.TryParse(h.GetString(), out var hid) ? hid : (Guid?)null;
                    var alertType = data.TryGetProperty("alertType", out var a) ? a.GetString() : "VITAL_SIGNS";
                    var message = data.TryGetProperty("message", out var m) ? m.GetString() : "Health alert triggered";
                    var severity = data.TryGetProperty("severity", out var s) ? s.GetString() : "HIGH";

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = systemCoordinatorId,
                        Type = "HEALTH_ALERT",
                        Title = $"[Health Alert] {severity} - {alertType}",
                        Message = message ?? "A horse vital sign anomaly was recorded.",
                        ReferenceType = "Horse",
                        ReferenceId = horseId,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Incident.IncidentCreated":
                {
                    var incidentId = data.TryGetProperty("incidentId", out var i) && Guid.TryParse(i.GetString(), out var iid) ? iid : Guid.NewGuid();
                    var incidentNo = data.TryGetProperty("incidentNo", out var no) ? no.GetString() : "INCIDENT";
                    var title = data.TryGetProperty("title", out var t) ? t.GetString() : "New Incident";
                    var severity = data.TryGetProperty("severity", out var s) ? s.GetString() : "MEDIUM";
                    var tripId = data.TryGetProperty("tripId", out var tr) && Guid.TryParse(tr.GetString(), out var tid) ? tid : (Guid?)null;

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = systemCoordinatorId,
                        Type = "INCIDENT",
                        Title = $"[{severity}] Incident Reported: {incidentNo}",
                        Message = title ?? "An incident has been reported during transit.",
                        ReferenceType = "Incident",
                        ReferenceId = incidentId,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Incident.IncidentResolved":
                {
                    var incidentId = data.TryGetProperty("incidentId", out var i) && Guid.TryParse(i.GetString(), out var iid) ? iid : Guid.NewGuid();
                    var incidentNo = data.TryGetProperty("incidentNo", out var no) ? no.GetString() : "INCIDENT";
                    var note = data.TryGetProperty("resolutionNote", out var n) ? n.GetString() : "Incident resolved";

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = systemCoordinatorId,
                        Type = "INCIDENT",
                        Title = $"Incident Resolved: {incidentNo}",
                        Message = note ?? "The incident has been marked as resolved.",
                        ReferenceType = "Incident",
                        ReferenceId = incidentId,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Compliance.ComplianceReady":
                {
                    var tripId = data.TryGetProperty("tripId", out var tr) && Guid.TryParse(tr.GetString(), out var tid) ? tid : Guid.NewGuid();

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = systemCoordinatorId,
                        Type = "DOCUMENT_NEED_INFO",
                        Title = "Compliance Ready for Trip",
                        Message = $"All regulatory documents for trip {tripId} have been approved and cleared.",
                        ReferenceType = "Trip",
                        ReferenceId = tripId,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Booking.RequestApproved":
                {
                    var customerId = data.TryGetProperty("customerId", out var c) && Guid.TryParse(c.GetString(), out var cid) ? cid : systemCoordinatorId;
                    var requestNo = data.TryGetProperty("requestNo", out var r) ? r.GetString() : "";

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = customerId,
                        Type = "REQUEST_APPROVED",
                        Title = "Transport Request Approved",
                        Message = $"Your transport request {requestNo} has been approved.",
                        ReferenceType = "TransportRequest",
                        ReferenceId = null,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            default:
                break;
        }

        return list;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
