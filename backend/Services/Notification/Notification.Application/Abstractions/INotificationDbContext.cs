using Microsoft.EntityFrameworkCore;
using Notification.Domain.Entities;

namespace Notification.Application.Abstractions;

public interface INotificationDbContext
{
    DbSet<Notification.Domain.Entities.Notification> Notifications { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    DbSet<InboxMessage> InboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
