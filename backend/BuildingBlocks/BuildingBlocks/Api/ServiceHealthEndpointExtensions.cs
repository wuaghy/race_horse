using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Api;

public static class ServiceHealthEndpointExtensions
{
    public static WebApplication MapServiceHealth(this WebApplication app, string serviceName)
    {
        app.MapGet("/health", (HttpContext context) =>
        {
            var requestId = Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var parsedRequestId)
                ? parsedRequestId
                : Guid.NewGuid();
            return Results.Ok(ApiResponse<object>.Success(new
            {
                status = "healthy",
                service = serviceName,
                environment = app.Environment.EnvironmentName
            }, requestId: requestId));
        })
        .WithName($"Get{serviceName}Health")
        .AllowAnonymous();

        return app;
    }
}