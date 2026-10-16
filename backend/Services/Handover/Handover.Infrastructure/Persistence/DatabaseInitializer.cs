using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Handover.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();

        try
        {
            if (db.Database.IsRelational())
            {
                logger.LogInformation("Verifying and ensuring SQL Server database schema for HandoverDb...");
                await db.Database.EnsureCreatedAsync();
                logger.LogInformation("HandoverDb database schema successfully verified and ensured.");
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HandoverDb schema auto-initialization encountered an issue. Ensure SQL Server is reachable.");
        }
    }
}
