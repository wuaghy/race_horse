using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planning.Application.Abstractions;
using Planning.Application.Services;
using Planning.Infrastructure.Messaging;

namespace Planning.Infrastructure.Persistence;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlanningPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PlanningDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IPlanningDbContext>(sp => sp.GetRequiredService<PlanningDbContext>());
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        // Application Services
        services.AddScoped<IFleetMasterDataService, FleetMasterDataService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<ITripPlanningService, TripPlanningService>();
        services.AddScoped<ITripReadinessService, TripReadinessService>();

        return services;
    }

    public static IServiceCollection AddPlanningMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.AddHostedService<OutboxDispatcherBackgroundService>();
        services.AddHostedService<RabbitMqEventConsumerService>();

        return services;
    }
}
