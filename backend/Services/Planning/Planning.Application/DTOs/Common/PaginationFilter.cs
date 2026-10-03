namespace Planning.Application.DTOs.Common;

public record PaginationFilter
{
    public int Page { get; init; } = 0;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }

    public int NormalizedPage => Math.Max(0, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? 20 : PageSize, 1, 100);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
}
