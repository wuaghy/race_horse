using BuildingBlocks.Api;

namespace BuildingBlocks.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public int ErrorCode { get; }
    public List<ApiError>? Errors { get; }

    public AppException(string message, int statusCode = 500, int errorCode = 50000, List<ApiError>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Errors = errors;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with identifier '{key}' was not found.", 404, 40400)
    {
    }
}

public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message, string reason = "BUSINESS_RULE_VIOLATION", string? field = null)
        : base(message, 422, 42200, new List<ApiError> { new() { Field = field, Reason = reason, Message = message } })
    {
    }
}

public class ConcurrencyException : AppException
{
    public ConcurrencyException(string message = "The entity has been modified by another actor.")
        : base(message, 409, 40900, new List<ApiError> { new() { Reason = "COMMON_CONCURRENCY_CONFLICT", Message = message } })
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized access.")
        : base(message, 401, 40100)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden. You do not have permission to perform this action.")
        : base(message, 403, 40300)
    {
    }
}
