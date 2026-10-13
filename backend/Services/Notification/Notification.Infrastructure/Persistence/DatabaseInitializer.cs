using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Notification.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(NotificationDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("NotificationDb initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing NotificationDb database.");
            throw;
        }
    }
}
