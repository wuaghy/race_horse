using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.DTOs;
using Notification.Infrastructure.Services;
using Xunit;

namespace Notification.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsOnlyUserNotificationsWithPaging()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(GetUserNotificationsAsync_ReturnsOnlyUserNotificationsWithPaging));
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await service.CreateNotificationAsync(user1, "TRIP_DEPARTED", "Trip Departed", "Trip has departed origin", "Trip", Guid.NewGuid());
        await service.CreateNotificationAsync(user1, "HEALTH_ALERT", "Health Alert", "High heart rate", "Horse", Guid.NewGuid());
        await service.CreateNotificationAsync(user2, "REQUEST_APPROVED", "Approved", "Request approved", "TransportRequest", Guid.NewGuid());

        // Act
        var result = await service.GetUserNotificationsAsync(user1, unreadOnly: false, page: 1, pageSize: 10);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, n => Assert.Equal(user1, n.RecipientUserId));
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesStatusAndReadAt()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(MarkAsReadAsync_UpdatesStatusAndReadAt));
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var userId = Guid.NewGuid();
        var created = await service.CreateNotificationAsync(userId, "INCIDENT", "Incident reported", "Vehicle issue", "Incident", Guid.NewGuid());

        // Act
        var success = await service.MarkAsReadAsync(created.Id, userId);

        // Assert
        Assert.True(success);

        var updated = await db.Notifications.FirstOrDefaultAsync(n => n.Id == created.Id);
        Assert.NotNull(updated);
        Assert.Equal("READ", updated.Status);
        Assert.NotNull(updated.ReadAt);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllUnreadNotificationsAsRead()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(MarkAllAsReadAsync_MarksAllUnreadNotificationsAsRead));
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var userId = Guid.NewGuid();
        await service.CreateNotificationAsync(userId, "INCIDENT", "Incident 1", "Desc", null, null);
        await service.CreateNotificationAsync(userId, "HEALTH_ALERT", "Alert 2", "Desc", null, null);

        var unreadBefore = await service.GetUnreadCountAsync(userId);
        Assert.Equal(2, unreadBefore.UnreadCount);

        // Act
        var updatedCount = await service.MarkAllAsReadAsync(userId);

        // Assert
        Assert.Equal(2, updatedCount);
        var unreadAfter = await service.GetUnreadCountAsync(userId);
        Assert.Equal(0, unreadAfter.UnreadCount);
    }

    [Fact]
    public async Task RegisterDeviceTokenAsync_UpsertsTokenRecord()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(RegisterDeviceTokenAsync_UpsertsTokenRecord));
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var token = "fcm_token_device_abc123";

        // Act 1: Register for user1
        await service.RegisterDeviceTokenAsync(user1, new RegisterDeviceTokenRequest("ANDROID", token));

        var record1 = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token);
        Assert.NotNull(record1);
        Assert.Equal(user1, record1.UserId);
        Assert.True(record1.Active);

        // Act 2: Same device token now registered by user2 (device handover/login switch)
        await service.RegisterDeviceTokenAsync(user2, new RegisterDeviceTokenRequest("ANDROID", token));

        var record2 = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token);
        Assert.NotNull(record2);
        Assert.Equal(user2, record2.UserId);
    }

    [Fact]
    public async Task UnregisterDeviceTokenAsync_DeactivatesToken()
    {
        // Arrange
        var db = TestDbContextFactory.CreateInMemoryDbContext(nameof(UnregisterDeviceTokenAsync_DeactivatesToken));
        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var userId = Guid.NewGuid();
        var token = "apns_token_device_xyz";

        await service.RegisterDeviceTokenAsync(userId, new RegisterDeviceTokenRequest("IOS", token));

        // Act
        await service.UnregisterDeviceTokenAsync(userId, token);

        // Assert
        var record = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token);
        Assert.NotNull(record);
        Assert.False(record.Active);
    }
}
