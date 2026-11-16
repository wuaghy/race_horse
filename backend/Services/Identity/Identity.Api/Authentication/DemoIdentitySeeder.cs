using Identity.Application.Authentication;

namespace Identity.Api.Authentication;

public sealed class DemoIdentitySeeder(
    IConfiguration configuration,
    IHostEnvironment environment,
    IServiceScopeFactory scopeFactory,
    ILogger<DemoIdentitySeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("DemoData:Enabled"))
        {
            return;
        }

        var password = configuration["DemoData:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new InvalidOperationException("DemoData:Password must be configured with at least 8 characters when demo seeding is enabled.");
        }

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
        var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        await EnsureUserAsync(store, passwordHashService, DemoUsers.CustomerId, "customer.demo@racehorse.local", "Demo Customer", "CUSTOMER", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.ManagerId, "manager.demo@racehorse.local", "Demo Logistics Manager", "LOGISTICS_MANAGER", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.LogisticsId, "logistics.demo@racehorse.local", "Demo Logistics Specialist", "TRANSPORT_SPECIALIST", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.DriverId, "driver.demo@racehorse.local", "Demo Driver & Escort", "DRIVER_ESCORT", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.EscortId, "escort.demo@racehorse.local", "Demo Horse Escort", "ESCORT", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.AdminId, "admin.demo@racehorse.local", "Demo Administrator", "ADMIN", password, cancellationToken);
        logger.LogInformation("Development demo identities are ready; all use the configured DemoData password.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Task EnsureUserAsync(
        IIdentityStore store,
        IPasswordHashService passwordHashService,
        Guid id,
        string email,
        string fullName,
        string roleCode,
        string password,
        CancellationToken cancellationToken)
    {
        var user = new AuthUser(id, email, fullName, null, roleCode, null, "ACTIVE");
        var passwordHash = passwordHashService.Hash(user, password);
        return store.EnsureDevelopmentUserAsync(id, email, fullName, roleCode, passwordHash, cancellationToken);
    }
}

public static class DemoUsers
{
    public static readonly Guid CustomerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public static readonly Guid ManagerId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public static readonly Guid LogisticsId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public static readonly Guid DriverId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public static readonly Guid EscortId = Guid.Parse("a83ca03b-ea88-4693-9a43-5c7019c2c697");
    public static readonly Guid AdminId = Guid.Parse("44444444-4444-4444-8444-444444444444");
}
