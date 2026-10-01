using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Planning.Application.Abstractions;
using Planning.Domain;
using Planning.Infrastructure.Messaging;
using Planning.Infrastructure.Persistence;
using Xunit;

namespace Planning.Tests;

public class OutboxDispatcherTests
{
    private class FakeMessageBus : IMessageBus
    {
        public List<(string RoutingKey, string EventType, string Payload)> Published { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task PublishAsync(string routingKey, string eventType, string payload, Guid correlationId, CancellationToken ct = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Broker communication error");
            }

            Published.Add((routingKey, eventType, payload));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessPendingMessages_PublishesAndMarksPublished()
    {
        var services = new ServiceCollection();
        var fakeBus = new FakeMessageBus();

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<PlanningDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IPlanningDbContext>(sp => sp.GetRequiredService<PlanningDbContext>());
        services.AddSingleton<IMessageBus>(fakeBus);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        // Seed pending message
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
            db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = "Planning.TripReady",
                AggregateType = "TransportTrip",
                AggregateId = Guid.NewGuid(),
                Payload = "{\"tripId\":\"abc\"}",
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new OutboxDispatcherBackgroundService(scopeFactory, NullLogger<OutboxDispatcherBackgroundService>.Instance);

        var processed = await dispatcher.ProcessPendingMessagesAsync();
        Assert.Equal(1, processed);
        Assert.Single(fakeBus.Published);
        Assert.Equal("Planning.TripReady", fakeBus.Published[0].EventType);

        // Verify status is PUBLISHED in DB
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
            var msg = await db.OutboxMessages.FirstAsync();
            Assert.Equal("PUBLISHED", msg.Status);
            Assert.NotNull(msg.ProcessedAt);
        }
    }

    [Fact]
    public async Task ProcessPendingMessages_OnFailure_IncrementsRetryCount()
    {
        var services = new ServiceCollection();
        var fakeBus = new FakeMessageBus { ShouldThrow = true };

        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<PlanningDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IPlanningDbContext>(sp => sp.GetRequiredService<PlanningDbContext>());
        services.AddSingleton<IMessageBus>(fakeBus);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
            db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = "Planning.TripCreated",
                AggregateType = "TransportTrip",
                AggregateId = Guid.NewGuid(),
                Payload = "{}",
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new OutboxDispatcherBackgroundService(scopeFactory, NullLogger<OutboxDispatcherBackgroundService>.Instance);

        var processed = await dispatcher.ProcessPendingMessagesAsync();
        Assert.Equal(0, processed);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
            var msg = await db.OutboxMessages.FirstAsync();
            Assert.Equal("PENDING", msg.Status);
            Assert.Equal(1, msg.RetryCount);
        }
    }
}
