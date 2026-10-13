using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notification.Application.Abstractions;
using Notification.Application.DTOs;
using Notification.Application.Services;
using Notification.Domain.Entities;

namespace Notification.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(INotificationDbContext db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PagedResult<NotificationDto>> GetUserNotificationsAsync(Guid userId, bool? unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == userId);

        if (unreadOnly == true)
        {
            query = query.Where(n => n.Status != "READ");
        }

        var totalCount = await query.CountAsync(ct);

        var pageNumber = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(n => new NotificationDto(
                n.Id,
                n.RecipientUserId,
                n.Type,
                n.Title,
                n.Message,
                n.ReferenceType,
                n.ReferenceId,
                n.Status,
                n.ReadAt,
                n.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<NotificationDto>(items, totalCount, pageNumber, size);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var count = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && n.Status != "READ", ct);

        return new UnreadCountDto(userId, count);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct);

        if (notification == null) return false;

        notification.Status = "READ";
        notification.ReadAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Marked notification {Id} as READ for user {UserId}", notificationId, userId);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && n.Status != "READ")
            .ToListAsync(ct);

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.Status = "READ";
            n.ReadAt = now;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Marked {Count} notifications as READ for user {UserId}", unread.Count, userId);
        return unread.Count;
    }

    public async Task RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken ct = default)
    {
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token, ct);

        if (existing != null)
        {
            existing.UserId = userId;
            existing.Platform = request.Platform.ToUpperInvariant();
            existing.Active = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.DeviceTokens.Add(new DeviceToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Platform = request.Platform.ToUpperInvariant(),
                Token = request.Token,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Registered device token for user {UserId} on platform {Platform}", userId, request.Platform);
    }

    public async Task UnregisterDeviceTokenAsync(Guid userId, string token, CancellationToken ct = default)
    {
        var existing = await _db.DeviceTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == token, ct);

        if (existing != null)
        {
            existing.Active = false;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Deactivated device token for user {UserId}", userId);
        }
    }

    public async Task<NotificationDto> CreateNotificationAsync(
        Guid recipientUserId,
        string type,
        string title,
        string message,
        string? refType,
        Guid? refId,
        CancellationToken ct = default)
    {
        var notification = new Notification.Domain.Entities.Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceType = refType,
            ReferenceId = refId,
            Status = "SENT",
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created custom notification {Id} for user {UserId}", notification.Id, recipientUserId);

        return new NotificationDto(
            notification.Id,
            notification.RecipientUserId,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.ReferenceType,
            notification.ReferenceId,
            notification.Status,
            notification.ReadAt,
            notification.CreatedAt);
    }
}
