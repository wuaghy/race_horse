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
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException("DemoData:Password must be configured with at least 12 characters when demo seeding is enabled.");
        }

        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdentityStore>();
        var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        await EnsureUserAsync(store, passwordHashService, DemoUsers.CustomerId, "customer.demo@racehorse.local", "Demo Customer", "CUSTOMER", password, cancellationToken);
        await EnsureUserAsync(store, passwordHashService, DemoUsers.ManagerId, "manager.demo@racehorse.local", "Demo Logistics Manager", "LOGISTICS_MANAGER", password, cancellationToken);
        logger.LogInformation("Development demo identities are ready; both use the configured DemoData password.");
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
}