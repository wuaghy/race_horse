using System.Text;
using Handover.Application.Abstractions;
using Handover.Application.Services;
using Handover.Domain.Entities;
using Handover.Infrastructure.Messaging;
using Handover.Infrastructure.Persistence;
using Handover.Infrastructure.Services;
using Handover.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Handover.Tests;

public class HandoverMessagingAndStorageTests
{
    private sealed class FakeHandoverMessageBus : IHandoverMessageBus
    {
        public List<(Guid EventId, string EventType, string Payload, Guid CorrelationId)> Published { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task PublishAsync(Guid eventId, string eventType, string payload, Guid correlationId, DateTime occurredAt, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("Simulated broker failure");

            Published.Add((eventId, eventType, payload, correlationId));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task OutboxDispatcher_PublishesHandoverCompletedAndMarksPublished()
    {
        var services = new ServiceCollection();
        var fakeBus = new FakeHandoverMessageBus();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<HandoverDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IHandoverDbContext>(sp => sp.GetRequiredService<HandoverDbContext>());
        services.AddSingleton<IHandoverMessageBus>(fakeBus);

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHandoverDbContext>();
            await db.OutboxMessages.AddAsync(new HandoverOutboxMessage
            {
                EventType = "Handover.HandoverCompleted",
                AggregateType = "HandoverRecord",
                AggregateId = Guid.NewGuid(),
                Payload = "{\"handoverNo\":\"HND-20261009-001\"}",
                CorrelationId = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Status = "PENDING"
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new HandoverOutboxDispatcher(scopeFactory, NullLogger<HandoverOutboxDispatcher>.Instance);
        var count = await dispatcher.ProcessPendingMessagesAsync();

        Assert.Equal(1, count);
        Assert.Single(fakeBus.Published);
        Assert.Equal("Handover.HandoverCompleted", fakeBus.Published[0].EventType);

        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IHandoverDbContext>();
            var msg = await db.OutboxMessages.SingleAsync();
            Assert.Equal("PUBLISHED", msg.Status);
            Assert.NotNull(msg.ProcessedAt);
        }
    }

    [Fact]
    public async Task EventConsumer_HandleTripDeparted_IdempotentlyCreatesDraftHandover()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<HandoverDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IHandoverDbContext>(sp => sp.GetRequiredService<HandoverDbContext>());
        services.AddScoped<IHandoverService, HandoverService>();

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var consumer = new HandoverEventConsumer(
            scopeFactory,
            Options.Create(new RabbitMqOptions { Enabled = false }),
            NullLogger<HandoverEventConsumer>.Instance);

        var tripId = Guid.NewGuid();
        var horseId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var json = $$"""
        {
          "eventId": "{{Guid.NewGuid()}}",
          "eventType": "Tracking.TripDeparted",
          "version": 1,
          "occurredAt": "2026-10-09T10:00:00Z",
          "correlationId": "{{Guid.NewGuid()}}",
          "source": "tracking-service",
          "data": {
            "tripId": "{{tripId}}",
            "departedByUserId": "{{driverId}}",
            "horseIds": ["{{horseId}}"]
          }
        }
        """;

        await consumer.HandleTripDepartedAsync(json);
        await consumer.HandleTripDepartedAsync(json); // Duplicate delivery must be idempotent

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHandoverDbContext>();
        var records = await db.HandoverRecords.Include(x => x.Horses).Where(x => x.TripId == tripId).ToListAsync();
        Assert.Single(records);
        Assert.Equal(HandoverStatuses.Draft, records[0].Status);
        Assert.Single(records[0].Horses);
        Assert.Equal(horseId, records[0].Horses[0].HorseId);
    }

    [Fact]
    public async Task LocalHandoverFileStorage_SavesReadsAndDeletesFileSafely()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"handover-storage-test-{Guid.NewGuid():N}");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:HandoverDirectory"] = tempDir })
                .Build();

            var storage = new LocalHandoverFileStorage(config);
            var bytes = Encoding.UTF8.GetBytes("test-signature-content");
            using var input = new MemoryStream(bytes);

            var key = await storage.SaveAsync(input, ".png");
            Assert.EndsWith(".png", key);

            await using (var readStream = await storage.OpenReadAsync(key))
            {
                Assert.NotNull(readStream);
                using var reader = new StreamReader(readStream!);
                Assert.Equal("test-signature-content", await reader.ReadToEndAsync());
            }

            await storage.DeleteAsync(key);
            Assert.Null(await storage.OpenReadAsync(key));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
