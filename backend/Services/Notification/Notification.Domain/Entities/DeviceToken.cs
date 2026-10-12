namespace Notification.Domain.Entities;

public class DeviceToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>
    /// Platform: IOS, ANDROID
    /// </summary>
    public string Platform { get; set; } = "ANDROID";
    public string Token { get; set; } = string.Empty;
    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
