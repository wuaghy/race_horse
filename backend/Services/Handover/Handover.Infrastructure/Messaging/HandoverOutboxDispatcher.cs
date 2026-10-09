using Handover.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Handover.Infrastructure.Messaging;

public sealed class HandoverOutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<HandoverOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxRetryCount = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessPendingMessagesAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Handover outbox dispatch loop failed."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHandoverDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IHandoverMessageBus>();
        var messages = await db.OutboxMessages
            .Where(x => x.Status == "PENDING")
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var delivered = 0;
        foreach (var message in messages)
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
                logger.LogWarning(ex, "Handover event {EventId} publish failed (attempt {RetryCount}).", message.Id, message.RetryCount);
                if (message.RetryCount >= MaxRetryCount) message.Status = "FAILED";
            }
        }

        if (messages.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }
}
