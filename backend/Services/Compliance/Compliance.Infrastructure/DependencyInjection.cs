using Compliance.Application.Abstractions;
using Compliance.Application.Services;
using Compliance.Infrastructure.Messaging;
using Compliance.Infrastructure.Persistence;
using Compliance.Infrastructure.Services;
using Compliance.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Compliance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddComplianceInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ComplianceDb");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ComplianceDbContext>(options =>
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ComplianceDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                }));
        }
        else
        {
            services.AddDbContext<ComplianceDbContext>(options =>
                options.UseInMemoryDatabase("ComplianceDb_Fallback"));
        }

        services.AddScoped<IComplianceDbContext>(sp => sp.GetRequiredService<ComplianceDbContext>());

        // File Storage
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        // Domain & Application Services
        services.AddScoped<IComplianceMasterDataService, ComplianceMasterDataService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IClearanceService, ClearanceService>();
        services.AddScoped<IComplianceReadinessService, ComplianceReadinessService>();

        // Messaging
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();
        services.AddHostedService<ComplianceOutboxDispatcher>();

        return services;
    }
}
