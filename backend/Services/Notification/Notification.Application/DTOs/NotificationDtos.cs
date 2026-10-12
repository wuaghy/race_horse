namespace Notification.Application.DTOs;

public record NotificationDto(
    Guid Id,
    Guid RecipientUserId,
    string Type,
    string Title,
    string Message,
    string? ReferenceType,
    Guid? ReferenceId,
    string Status,
    DateTime? ReadAt,
    DateTime CreatedAt);

public record UnreadCountDto(
    Guid UserId,
    int UnreadCount);

public record RegisterDeviceTokenRequest(
    string Platform,
    string Token);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
