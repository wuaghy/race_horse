using Handover.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Handover.Tests;

public static class TestDbContextFactory
{
    public static HandoverDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<HandoverDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new HandoverDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
