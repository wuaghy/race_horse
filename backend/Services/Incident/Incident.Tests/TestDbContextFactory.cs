using Incident.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Incident.Tests;

public static class TestDbContextFactory
{
    public static IncidentDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<IncidentDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new IncidentDbContext(options);
    }
}
