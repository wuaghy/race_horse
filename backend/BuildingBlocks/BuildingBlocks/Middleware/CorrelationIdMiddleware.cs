using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace BuildingBlocks.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string RequestIdHeader = "X-Request-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate _next)
    {
        this._next = _next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeader] = correlationId;
        }

        if (!context.Request.Headers.TryGetValue(RequestIdHeader, out StringValues requestId) || string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString();
            context.Request.Headers[RequestIdHeader] = requestId;
        }

        context.Items[CorrelationIdHeader] = correlationId.ToString();
        context.Items[RequestIdHeader] = requestId.ToString();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            context.Response.Headers[RequestIdHeader] = requestId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
