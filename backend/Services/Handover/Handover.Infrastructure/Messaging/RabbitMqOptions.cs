namespace Handover.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "racehorse.events";
    public string QueueName { get; set; } = "handover-service.inbox";
    public bool Enabled { get; set; } = true;
}
