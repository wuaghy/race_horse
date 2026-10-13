using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Persistence;

namespace Notification.Tests;

public static class TestDbContextFactory
{
    public static NotificationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new NotificationDbContext(options);
    }
}
