using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;

namespace Planning.Tests;

public static class TestDbContextFactory
{
    public static PlanningDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<PlanningDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new PlanningDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
