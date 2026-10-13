namespace Notification.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; set; } = false;
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "racehorse.events";
    public string QueueName { get; set; } = "notification.service.events";
}
