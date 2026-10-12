using FluentValidation;
using Incident.Application.Abstractions;
using Incident.Application.DTOs;
using Incident.Application.Services;
using Incident.Application.Validators;
using Incident.Infrastructure.Messaging;
using Incident.Infrastructure.Persistence;
using Incident.Infrastructure.Services;
using Incident.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Incident.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIncidentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IncidentDb");

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<IncidentDbContext>(options =>
                options.UseInMemoryDatabase("IncidentDb_InMemory"));
        }
        else
        {
            services.AddDbContext<IncidentDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        services.AddScoped<IIncidentDbContext>(sp => sp.GetRequiredService<IncidentDbContext>());
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        // Messaging
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.AddHostedService<IncidentOutboxDispatcher>();

        // Validators
        services.AddScoped<IValidator<CreateIncidentRequest>, CreateIncidentRequestValidator>();
        services.AddScoped<IValidator<UpdateIncidentStatusRequest>, UpdateIncidentStatusRequestValidator>();
        services.AddScoped<IValidator<ResolveIncidentRequest>, ResolveIncidentRequestValidator>();

        // Application Services
        services.AddScoped<IIncidentService, IncidentService>();

        return services;
    }
}
