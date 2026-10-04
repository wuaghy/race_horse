using Identity.Application.Authentication;
using Identity.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, JwtSettings jwtSettings)
    {
        services.AddSingleton(jwtSettings);
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<IIdentityTokenService>(provider => provider.GetRequiredService<JwtTokenService>());
        services.AddSingleton<PasswordHasher<AuthUser>>();
        services.AddSingleton<IPasswordHashService, AspNetPasswordHashService>();
        services.AddScoped<IIdentityStore, SqlIdentityStore>();
        return services;
    }
}