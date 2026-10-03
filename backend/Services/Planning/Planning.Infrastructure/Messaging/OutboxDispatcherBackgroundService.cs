using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Messaging;

public class OutboxDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int MaxRetryCount = 5;

    public OutboxDispatcherBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Dispatcher Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Outbox Dispatcher loop.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Outbox Dispatcher Background Service stopped.");
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IPlanningDbContext>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.Status == "PENDING")
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Found {Count} pending outbox messages to dispatch.", pendingMessages.Count);

        int processed = 0;
        foreach (var msg in pendingMessages)
        {
            try
            {
                await messageBus.PublishAsync(
                    routingKey: msg.EventType,
                    eventType: msg.EventType,
                    payload: msg.Payload,
                    correlationId: msg.CorrelationId,
                    ct: ct
                );

                msg.Status = "PUBLISHED";
                msg.ProcessedAt = DateTime.UtcNow;
                processed++;
            }
            catch (Exception ex)
            {
                msg.RetryCount++;
                _logger.LogWarning(ex, "Failed to dispatch outbox message {Id} ({EventType}). Retry count: {RetryCount}",
                    msg.Id, msg.EventType, msg.RetryCount);

                if (msg.RetryCount >= MaxRetryCount)
                {
                    msg.Status = "FAILED";
                    _logger.LogError("Outbox message {Id} reached maximum retries ({MaxRetries}) and is marked FAILED.",
                        msg.Id, MaxRetryCount);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return processed;
    }
}
