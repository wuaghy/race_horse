using Notification.Application.DTOs;

namespace Notification.Application.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool? unreadOnly, int page, int pageSize, CancellationToken ct = default);
    Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct = default);
    Task UnregisterDeviceTokenAsync(Guid userId, string token, CancellationToken ct = default);
    Task<NotificationDto> CreateNotificationAsync(Guid recipientUserId, string type, string title, string message, string? refType, Guid? refId, CancellationToken ct = default);
}
