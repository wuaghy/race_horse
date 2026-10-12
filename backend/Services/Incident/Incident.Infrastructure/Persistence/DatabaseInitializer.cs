using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Incident.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IncidentDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            if (context.Database.IsRelational())
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }

            logger.LogInformation("IncidentDb initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while initializing the IncidentDb database.");
            throw;
        }
    }
}
