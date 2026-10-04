using Compliance.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compliance.Infrastructure.Messaging;

public class ComplianceOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComplianceOutboxDispatcher> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(3);
    private const int MaxRetryCount = 5;

    public ComplianceOutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<ComplianceOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Compliance Outbox Dispatcher Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Compliance Outbox Dispatcher loop.");
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

        _logger.LogInformation("Compliance Outbox Dispatcher Background Service stopped.");
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IComplianceDbContext>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pendingMessages = await db.OutboxMessages
            .Where(m => m.Status == "PENDING")
            .OrderBy(m => m.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Found {Count} pending outbox messages to dispatch.", pendingMessages.Count);
        var processedCount = 0;

        foreach (var message in pendingMessages)
        {
            try
            {
                await messageBus.PublishAsync(
                    routingKey: message.EventType,
                    eventType: message.EventType,
                    payload: message.Payload,
                    correlationId: message.CorrelationId,
                    cancellationToken: cancellationToken
                );

                message.Status = "PROCESSED";
                message.ProcessedAt = DateTime.UtcNow;
                processedCount++;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                _logger.LogWarning(ex, "Failed to dispatch outbox message {Id} (Attempt {RetryCount}/{MaxRetryCount}).",
                    message.Id, message.RetryCount, MaxRetryCount);

                if (message.RetryCount >= MaxRetryCount)
                {
                    message.Status = "FAILED";
                    _logger.LogError("Outbox message {Id} marked as FAILED after {MaxRetryCount} attempts.", message.Id, MaxRetryCount);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return processedCount;
    }
}
