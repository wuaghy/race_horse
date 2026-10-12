using Microsoft.Extensions.DependencyInjection;
using Tracking.Application.Services;

namespace Tracking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTrackingApplication(this IServiceCollection services)
    {
        services.AddScoped<ITripExecutionService, TripExecutionService>();
        services.AddScoped<IGpsTrackingService, GpsTrackingService>();
        services.AddScoped<IHorseHealthService, HorseHealthService>();

        return services;
    }
}
