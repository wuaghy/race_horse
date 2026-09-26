using System.Text.Json.Serialization;

namespace BuildingBlocks.Api;

public class ApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; } = 0;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Success";

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<ApiError>? Errors { get; set; }

    [JsonPropertyName("meta")]
    public ApiMeta Meta { get; set; } = new();

    public static ApiResponse<T> Success(T data, string message = "Success", Guid? requestId = null) =>
        new()
        {
            Code = 0,
            Message = message,
            Data = data,
            Errors = null,
            Meta = new ApiMeta { RequestId = requestId ?? Guid.NewGuid(), Timestamp = DateTime.UtcNow }
        };

    public static ApiResponse<T> Success(T data, PaginationMeta pagination, string message = "Success", Guid? requestId = null) =>
        new()
        {
            Code = 0,
            Message = message,
            Data = data,
            Errors = null,
            Meta = new ApiMeta
            {
                RequestId = requestId ?? Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Pagination = pagination
            }
        };

    public static ApiResponse<T> Failure(int code, string message, List<ApiError>? errors = null, Guid? requestId = null) =>
        new()
        {
            Code = code,
            Message = message,
            Data = default,
            Errors = errors,
            Meta = new ApiMeta { RequestId = requestId ?? Guid.NewGuid(), Timestamp = DateTime.UtcNow }
        };
}

public class ApiError
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ApiMeta
{
    [JsonPropertyName("requestId")]
    public Guid RequestId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("pagination")]
    public PaginationMeta? Pagination { get; set; }
}

public class PaginationMeta
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalItems")]
    public long TotalItems { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }
}
