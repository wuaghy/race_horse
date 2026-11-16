using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Email;

public sealed class EmailOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IEmailSender emailSender,
    IOptions<EmailOptions> options,
    ILogger<EmailOutboxDispatcher> logger) : BackgroundService
{
    private const int MaxRetries = 10;
    private readonly EmailOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("SMTP email delivery is disabled. Email outbox messages will remain queued.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var email = await db.EmailOutboxMessages
                    .Where(item => item.Status == "PENDING" && item.NextAttemptAt <= DateTime.UtcNow)
                    .OrderBy(item => item.CreatedAt)
                    .FirstOrDefaultAsync(stoppingToken);

                if (email is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                try
                {
                    await emailSender.SendAsync(email.ToEmail, email.Subject, email.HtmlBody, email.TextBody, stoppingToken);
                    email.Status = "SENT";
                    email.SentAt = DateTime.UtcNow;
                    email.LastError = null;
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Sent email outbox item {EmailId} for event {EventId}.", email.Id, email.EventId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    email.RetryCount++;
                    email.LastError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
                    email.Status = email.RetryCount >= MaxRetries ? "FAILED" : "PENDING";
                    email.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, email.RetryCount) * 5));
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogWarning(ex, "Email outbox item {EmailId} failed attempt {RetryCount}/{MaxRetries}.", email.Id, email.RetryCount, MaxRetries);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email outbox dispatcher failed; it will retry shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
