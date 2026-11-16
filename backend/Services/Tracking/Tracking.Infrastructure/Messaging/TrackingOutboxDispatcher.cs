using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tracking.Application.Services;

namespace Tracking.Infrastructure.Messaging;

public sealed class TrackingOutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<TrackingOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingMessagesAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Tracking outbox dispatch loop failed."); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var bus = scope.ServiceProvider.GetRequiredService<ITrackingMessageBus>();
        var pending = await repository.GetPendingAsync(20, cancellationToken);
        var delivered = 0;
        foreach (var message in pending)
        {
            try
            {
                await bus.PublishAsync(message.Id, message.EventType, message.Payload, message.CorrelationId, message.OccurredAt, cancellationToken);
                message.Status = "PUBLISHED";
                message.ProcessedAt = DateTime.UtcNow;
                delivered++;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                if (message.RetryCount >= 5) message.Status = "FAILED";
                logger.LogWarning(ex, "Tracking event {EventId} publish failed (attempt {RetryCount}).", message.Id, message.RetryCount);
            }
            await repository.UpdateAsync(message, cancellationToken);
        }
        return delivered;
    }
}
