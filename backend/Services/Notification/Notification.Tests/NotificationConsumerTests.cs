using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notification.Application.Abstractions;
using Notification.Infrastructure.Messaging;
using Notification.Infrastructure.Persistence;
using Xunit;

namespace Notification.Tests;

public class NotificationConsumerTests
{
    private class TestScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public TestScopeFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
        public IServiceScope CreateScope() => new TestScope(_serviceProvider);

        private class TestScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; }
            public TestScope(IServiceProvider provider) => ServiceProvider = provider;
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task ProcessEventAsync_DuplicateEventId_DropsDuplicateIdempotently()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(ProcessEventAsync_DuplicateEventId_DropsDuplicateIdempotently));
        var services = new ServiceCollection();
        services.AddSingleton<INotificationDbContext>(db);
        var provider = services.BuildServiceProvider();
        var scopeFactory = new TestScopeFactory(provider);

        var consumer = new NotificationEventConsumer(
            scopeFactory,
            Options.Create(new RabbitMqOptions { Enabled = false }),
            NullLogger<NotificationEventConsumer>.Instance);

        var eventId = Guid.NewGuid();
        var eventPayload = JsonSerializer.Serialize(new
        {
            eventId = eventId,
            eventType = "Tracking.HealthAlertRaised",
            source = "tracking-service",
            data = new
            {
                horseId = Guid.NewGuid().ToString(),
                alertType = "BODY_TEMPERATURE",
                severity = "CRITICAL",
                message = "Body temperature reached 39.8C"
            }
        });

        // Act 1: First delivery
        var result1 = await consumer.ProcessEventAsync(eventPayload);
        Assert.True(result1);

        var notifCountAfterFirst = await db.Notifications.CountAsync();
        var inboxCountAfterFirst = await db.InboxMessages.CountAsync();
        Assert.Equal(1, notifCountAfterFirst);
        Assert.Equal(1, inboxCountAfterFirst);

        // Act 2: Duplicate delivery of same eventId (at-least-once simulation)
        var result2 = await consumer.ProcessEventAsync(eventPayload);
        Assert.True(result2);

        // Assert: No new notification created
        var notifCountAfterSecond = await db.Notifications.CountAsync();
        var inboxCountAfterSecond = await db.InboxMessages.CountAsync();
        Assert.Equal(1, notifCountAfterSecond);
        Assert.Equal(1, inboxCountAfterSecond);
    }

    [Fact]
    public async Task ProcessEventAsync_IncidentCreated_GeneratesIncidentNotification()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(ProcessEventAsync_IncidentCreated_GeneratesIncidentNotification));
        var services = new ServiceCollection();
        services.AddSingleton<INotificationDbContext>(db);
        var provider = services.BuildServiceProvider();
        var scopeFactory = new TestScopeFactory(provider);

        var consumer = new NotificationEventConsumer(
            scopeFactory,
            Options.Create(new RabbitMqOptions { Enabled = false }),
            NullLogger<NotificationEventConsumer>.Instance);

        var incidentId = Guid.NewGuid();
        var eventPayload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(),
            eventType = "Incident.IncidentCreated",
            source = "incident-service",
            data = new
            {
                incidentId = incidentId.ToString(),
                incidentNo = "INC-20261012-0001",
                severity = "HIGH",
                title = "Engine issue on highway"
            }
        });

        // Act
        var result = await consumer.ProcessEventAsync(eventPayload);

        // Assert
        Assert.True(result);

        var notification = await db.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal("INCIDENT", notification.Type);
        Assert.Contains("INC-20261012-0001", notification.Title);
        Assert.Equal("Incident", notification.ReferenceType);
        Assert.Equal(incidentId, notification.ReferenceId);
    }
}
