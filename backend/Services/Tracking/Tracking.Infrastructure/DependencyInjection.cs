using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tracking.Application.Services;
using Tracking.Infrastructure.Persistence;
using Tracking.Infrastructure.Repositories;

namespace Tracking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TrackingDb")
            ?? throw new InvalidOperationException("Connection string 'TrackingDb' not found.");

        services.AddDbContext<TrackingDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(TrackingDbContext).Assembly.GetName().Name)));

        services.AddScoped<ITripExecutionRepository, EFTripExecutionRepository>();
        services.AddScoped<ICheckpointEventRepository, EFCheckpointEventRepository>();
        services.AddScoped<IOutboxMessageRepository, EFOutboxMessageRepository>();
        services.AddScoped<IGpsPointRepository, EFGpsPointRepository>();
        services.AddScoped<ILocationHistoryRepository, EFLocationHistoryRepository>();
        services.AddScoped<IEtaRepository, EFEtaRepository>();
        services.AddScoped<IHealthLogRepository, EFHealthLogRepository>();
        services.AddScoped<IHealthAlertRepository, EFHealthAlertRepository>();
        services.AddScoped<IHealthStatusRepository, EFHealthStatusRepository>();

        return services;
    }
}
