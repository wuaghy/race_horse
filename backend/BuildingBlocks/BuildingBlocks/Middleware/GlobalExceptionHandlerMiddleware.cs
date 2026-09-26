using System.Net;
using System.Text.Json;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        Guid requestId = Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var parsedReqId)
            ? parsedReqId
            : Guid.NewGuid();

        var correlationId = context.Items["X-Correlation-ID"]?.ToString() ?? "unknown";

        int statusCode;
        int errorCode;
        string message;
        List<ApiError>? errors = null;

        if (exception is AppException appEx)
        {
            statusCode = appEx.StatusCode;
            errorCode = appEx.ErrorCode;
            message = appEx.Message;
            errors = appEx.Errors;

            _logger.LogWarning(exception, "[{CorrelationId}] Handled business exception: {Message}", correlationId, message);
        }
        else
        {
            statusCode = (int)HttpStatusCode.InternalServerError;
            errorCode = 50000;
            message = "An unexpected server error occurred.";

            _logger.LogError(exception, "[{CorrelationId}] Unhandled server exception: {Message}", correlationId, exception.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = ApiResponse<object>.Failure(errorCode, message, errors, requestId);
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
