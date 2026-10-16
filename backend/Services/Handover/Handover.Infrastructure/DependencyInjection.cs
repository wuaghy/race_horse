using Handover.Application.Abstractions;
using Handover.Application.Services;
using Handover.Infrastructure.Messaging;
using Handover.Infrastructure.Persistence;
using Handover.Infrastructure.Services;
using Handover.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Handover.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHandoverInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HandoverDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<HandoverDbContext>(options => options.UseSqlServer(connectionString));
        }
        else
        {
            services.AddDbContext<HandoverDbContext>(options => options.UseInMemoryDatabase("HandoverDb_LocalFallback"));
        }

        services.AddScoped<IHandoverDbContext>(sp => sp.GetRequiredService<HandoverDbContext>());
        services.AddScoped<IHandoverService, HandoverService>();
        services.AddSingleton<IHandoverFileStorage, LocalHandoverFileStorage>();
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<HandoverMessageBus>();
        services.AddSingleton<IHandoverMessageBus>(sp => sp.GetRequiredService<HandoverMessageBus>());
        services.AddHostedService<HandoverOutboxDispatcher>();
        services.AddHostedService<HandoverEventConsumer>();
        return services;
    }
}
