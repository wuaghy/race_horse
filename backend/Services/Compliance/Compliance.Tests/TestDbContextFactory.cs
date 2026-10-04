using Microsoft.EntityFrameworkCore;
using Compliance.Infrastructure.Persistence;

namespace Compliance.Tests;

public static class TestDbContextFactory
{
    public static ComplianceDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new ComplianceDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
