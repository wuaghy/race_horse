using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly string _portalBaseUrl;
    private IConnection? _connection;
    private IChannel? _channel;

    public NotificationEventConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<NotificationEventConsumer> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _portalBaseUrl = (configuration["Portal:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
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
                "Incident.*",
                "Planning.*",
                "Handover.*"
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

        var email = CreateCustomerEmail(eventId, eventType, dataElem, notifications);
        if (email is not null)
            db.EmailOutboxMessages.Add(email);

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

    private EmailOutboxMessage? CreateCustomerEmail(
        Guid eventId,
        string eventType,
        JsonElement data,
        IReadOnlyCollection<Notification.Domain.Entities.Notification> notifications)
    {
        var emailProperty = eventType switch
        {
            "Booking.RequestApproved" => "customerEmail",
            "Booking.RequestDecisionChanged" => "recipientEmail",
            "Booking.OrderPaymentCompleted" => "customerEmail",
            "Planning.TripCompleted" => "customerEmail",
            "Handover.HandoverCompleted" => "customerEmail",
            _ => null
        };
        if (emailProperty is null || !data.TryGetProperty(emailProperty, out var emailElement))
            return null;

        var recipientEmail = emailElement.GetString()?.Trim();
        if (!MailAddress.TryCreate(recipientEmail, out var address))
            return null;

        var notification = notifications.FirstOrDefault(item => item.RecipientUserId != Guid.Parse("22222222-2222-4222-8222-222222222222"));
        if (notification is null)
            return null;

        var title = WebUtility.HtmlEncode(notification.Title);
        var message = WebUtility.HtmlEncode(notification.Message);
        var requestId = ReadGuid(data, "requestId");
        var orderId = ReadGuid(data, "orderId");
        var action = eventType == "Booking.RequestApproved" ? "pay=1" : "";
        var portalLink = requestId.HasValue || orderId.HasValue ? BuildPortalLink(requestId, orderId, action) : null;
        var linkLabel = eventType == "Booking.RequestApproved" ? "Review request and pay" : "Open transport update";
        var linkHtml = portalLink is null ? string.Empty : $"<p style=\"margin:24px 0\"><a href=\"{WebUtility.HtmlEncode(portalLink)}\" style=\"display:inline-block;padding:12px 18px;border-radius:7px;background:#16847c;color:#fff;text-decoration:none;font-weight:700\">{linkLabel}</a></p>";
        var linkText = portalLink is null ? string.Empty : $"\n{linkLabel}: {portalLink}\n";
        var htmlBody = $"<html><body><h2>{title}</h2><p>{message}</p>{linkHtml}<p>EquiRoute Transport Operations</p></body></html>";
        var textBody = $"{notification.Title}\n\n{notification.Message}\n{linkText}\nEquiRoute Transport Operations";
        if (eventType == "Booking.RequestApproved")
        {
            string? ReadString(string property) => data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            string FormatUtc(string property) => DateTimeOffset.TryParse(ReadString(property), out var date)
                ? date.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'") : "Not available";
            var horseNames = data.TryGetProperty("horseNames", out var names) && names.ValueKind == JsonValueKind.Array
                ? names.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray()
                : [];
            var details = new List<(string Label, string Value)>
            {
                ("Request", ReadString("requestNo") ?? "Not available"),
                ("Horses", horseNames.Length > 0 ? string.Join(", ", horseNames) : "Not available"),
                ("Approved by", ReadString("approvedBy") ?? "Operations team"),
                ("Approved at", FormatUtc("approvedAt")),
                ("Transport order", ReadString("orderNo") ?? "Not available"),
                ("Requested departure", FormatUtc("requestedDepartureAt"))
            };
            var approvalNote = ReadString("approvalNote");
            if (!string.IsNullOrWhiteSpace(approvalNote)) details.Add(("Approval note", approvalNote));
            var htmlDetails = string.Join("", details.Select(item => $"<tr><td style=\"padding:14px 0;border-bottom:1px solid #e8eef2\"><div style=\"font-size:12px;font-weight:700;color:#6d8295;letter-spacing:.3px\">{WebUtility.HtmlEncode(item.Label)}</div><div style=\"padding-top:5px;font-size:15px;line-height:1.5;font-weight:600;color:#17324d;overflow-wrap:anywhere\">{WebUtility.HtmlEncode(item.Value)}</div></td></tr>"));
            htmlBody = $$"""
                <!doctype html><html><body style="margin:0;padding:0;background-color:#f2f6f8;font-family:Arial,Helvetica,sans-serif;color:#17324d">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color:#f2f6f8"><tr><td align="center" style="padding:32px 16px">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background-color:#ffffff;border:1px solid #dce8ed;border-radius:12px">
                <tr><td style="padding:28px 32px 20px;border-bottom:1px solid #e8eef2"><div style="font-size:24px;font-weight:700;color:#17324d">Equi<span style="color:#16847c">Route</span></div><div style="padding-top:4px;font-size:11px;letter-spacing:1px;color:#768c9c">TRANSPORT OPERATIONS</div></td></tr>
                <tr><td style="padding:28px 32px 10px"><span style="display:inline-block;padding:6px 12px;border-radius:20px;background-color:#e3f5ed;color:#176c4d;font-size:12px;font-weight:700">APPROVED</span><h1 style="margin:16px 0 10px;font-size:24px;line-height:1.3;color:#17324d">Transport request approved</h1><p style="margin:0;color:#536d80;font-size:15px;line-height:1.6">{{message}}</p></td></tr>
                <tr><td style="padding:8px 32px 20px"><table role="presentation" width="100%" cellspacing="0" cellpadding="0">{{htmlDetails}}</table>{{linkHtml}}</td></tr>
                <tr><td style="padding:20px 32px 28px;color:#768c9c;font-size:12px;line-height:1.6">EquiRoute Transport Operations<br>Times are shown in UTC.</td></tr>
                </table></td></tr></table></body></html>
                """;
            textBody = $"{notification.Title}\n\n{notification.Message}\n\n{string.Join("\n", details.Select(item => $"{item.Label}: {item.Value}"))}\n{linkText}\nEquiRoute Transport Operations";
        }
        return new EmailOutboxMessage
        {
            EventId = eventId,
            ToEmail = address.Address,
            Subject = notification.Title,
            HtmlBody = htmlBody,
            TextBody = textBody,
            Status = "PENDING",
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private string BuildPortalLink(Guid? requestId, Guid? orderId, string action)
    {
        var query = requestId.HasValue
            ? $"open=request&requestId={Uri.EscapeDataString(requestId.Value.ToString())}"
            : $"open=order&orderId={Uri.EscapeDataString(orderId!.Value.ToString())}";
        return $"{_portalBaseUrl}/?{query}{(string.IsNullOrWhiteSpace(action) ? string.Empty : $"&{action}")}";
    }

    private static Guid? ReadGuid(JsonElement data, string property)
    {
        return data.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
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
                    var customerId = data.TryGetProperty("customerUserId", out var c) && Guid.TryParse(c.GetString(), out var cid) ? cid : systemCoordinatorId;
                    var requestNo = data.TryGetProperty("requestNo", out var r) ? r.GetString() : "";
                    var requestId = data.TryGetProperty("requestId", out var req) && Guid.TryParse(req.GetString(), out var rid) ? rid : (Guid?)null;

                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(),
                        RecipientUserId = customerId,
                        Type = "REQUEST_APPROVED",
                        Title = "Transport Request Approved",
                        Message = $"Your transport request {requestNo} has been approved.",
                        ReferenceType = "TransportRequest",
                        ReferenceId = requestId,
                        Status = "SENT",
                        CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Booking.RequestDecisionChanged":
                {
                    var recipient = data.TryGetProperty("recipientUserId", out var recipientElement) && Guid.TryParse(recipientElement.GetString(), out var recipientId) ? recipientId : systemCoordinatorId;
                    var requestId = data.TryGetProperty("requestId", out var requestElement) && Guid.TryParse(requestElement.GetString(), out var parsedRequestId) ? parsedRequestId : (Guid?)null;
                    var requestNo = data.TryGetProperty("requestNo", out var requestNoElement) ? requestNoElement.GetString() : "request";
                    var status = data.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : "UPDATED";
                    var reason = data.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() : null;
                    var (title, message, type) = status switch
                    {
                        "NEED_INFORMATION" => ("More information requested", $"The manager requested more information for transport request {requestNo}. {reason}".Trim(), "REQUEST_NEED_INFORMATION"),
                        "REJECTED" => ("Transport request declined", $"Transport request {requestNo} was declined. {reason}".Trim(), "REQUEST_REJECTED"),
                        _ => ("Transport request updated", $"Transport request {requestNo} changed to {status}.", "REQUEST_UPDATED")
                    };
                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(), RecipientUserId = recipient, Type = type, Title = title, Message = message,
                        ReferenceType = "TransportRequest", ReferenceId = requestId, Status = "SENT", CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Booking.OrderPaymentCompleted":
                {
                    var customerId = data.TryGetProperty("customerUserId", out var customerElement) && Guid.TryParse(customerElement.GetString(), out var parsedCustomerId) ? parsedCustomerId : systemCoordinatorId;
                    var orderId = data.TryGetProperty("orderId", out var orderElement) && Guid.TryParse(orderElement.GetString(), out var parsedOrderId) ? parsedOrderId : (Guid?)null;
                    var requestId = data.TryGetProperty("requestId", out var requestElement) && Guid.TryParse(requestElement.GetString(), out var parsedRequestId) ? parsedRequestId : (Guid?)null;
                    var orderNo = data.TryGetProperty("orderNo", out var orderNoElement) ? orderNoElement.GetString() : "the transport order";
                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(), RecipientUserId = customerId, Type = "ORDER_PAYMENT_COMPLETED",
                        Title = "Transport payment received",
                        Message = $"Payment for transport order {orderNo} was received. Logistics can now place it on a trip.",
                        ReferenceType = "TransportRequest", ReferenceId = requestId ?? orderId, Status = "SENT", CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Planning.TripCompleted":
                {
                    var customerId = data.TryGetProperty("customerUserId", out var customerElement) && Guid.TryParse(customerElement.GetString(), out var parsedCustomerId) ? parsedCustomerId : systemCoordinatorId;
                    var tripId = data.TryGetProperty("tripId", out var tripElement) && Guid.TryParse(tripElement.GetString(), out var parsedTripId) ? parsedTripId : (Guid?)null;
                    var requestId = data.TryGetProperty("requestId", out var requestElement) && Guid.TryParse(requestElement.GetString(), out var parsedRequestId) ? parsedRequestId : (Guid?)null;
                    var tripNo = data.TryGetProperty("tripNo", out var tripNoElement) ? tripNoElement.GetString() : "your trip";
                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(), RecipientUserId = customerId, Type = "TRIP_COMPLETED",
                        Title = "Transport trip completed",
                        Message = $"Trip {tripNo} has completed its journey and is ready for handover confirmation.",
                        ReferenceType = "TransportRequest", ReferenceId = requestId ?? tripId, Status = "SENT", CreatedAt = DateTime.UtcNow
                    });
                    break;
                }

            case "Handover.HandoverCompleted":
                {
                    var customerId = data.TryGetProperty("customerUserId", out var customerElement) && Guid.TryParse(customerElement.GetString(), out var parsedCustomerId) ? parsedCustomerId : systemCoordinatorId;
                    var handoverId = data.TryGetProperty("handoverId", out var handoverElement) && Guid.TryParse(handoverElement.GetString(), out var parsedHandoverId) ? parsedHandoverId : (Guid?)null;
                    var requestId = data.TryGetProperty("requestId", out var requestElement) && Guid.TryParse(requestElement.GetString(), out var parsedRequestId) ? parsedRequestId : (Guid?)null;
                    var handoverNo = data.TryGetProperty("handoverNo", out var handoverNoElement) ? handoverNoElement.GetString() : "your handover";
                    list.Add(new Notification.Domain.Entities.Notification
                    {
                        Id = Guid.NewGuid(), RecipientUserId = customerId, Type = "HANDOVER_COMPLETED",
                        Title = "Horse handover completed",
                        Message = $"Handover {handoverNo} has been completed. Your transport journey is now finished.",
                        ReferenceType = "TransportRequest", ReferenceId = requestId ?? handoverId, Status = "SENT", CreatedAt = DateTime.UtcNow
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
