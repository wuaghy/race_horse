using System.Data;
using System.Text;
using Booking.Infrastructure.Messaging;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Booking.Infrastructure.Messaging;

public sealed class BookingOutboxPublisher : BackgroundService
{
    private const string ExchangeName = "racehorse.events";
    private readonly string _connectionString;
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<BookingOutboxPublisher> _logger;

    public BookingOutboxPublisher(IConfiguration configuration, ILogger<BookingOutboxPublisher> logger)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            Port = configuration.GetValue("RabbitMQ:Port", 5672),
            AutomaticRecoveryEnabled = true
        };
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await ReadPendingAsync(stoppingToken);
                if (messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
                channel.ConfirmSelect();

                foreach (var message in messages)
                {
                    try
                    {
                        var properties = channel.CreateBasicProperties();
                        properties.Persistent = true;
                        properties.ContentType = "application/json";
                        properties.MessageId = message.Id.ToString();
                        properties.CorrelationId = message.CorrelationId.ToString();
                        channel.BasicPublish(
                            ExchangeName,
                            message.EventType,
                            mandatory: true,
                            basicProperties: properties,
                            body: Encoding.UTF8.GetBytes(message.Payload));
                        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
                        await MarkPublishedAsync(message.Id, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        await IncrementRetryCountAsync(message.Id, stoppingToken);
                        _logger.LogWarning(exception, "Outbox event {EventId} ({EventType}) remains pending.", message.Id, message.EventType);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "Booking outbox publisher is waiting for SQL Server and RabbitMQ.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<OutboxMessage>> ReadPendingAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT TOP (25) Id, EventType, Payload, CorrelationId FROM dbo.OutboxMessages " +
            "WHERE Status = 'PENDING' ORDER BY CreatedAt, Id;",
            connection);
        var messages = new List<OutboxMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(new OutboxMessage(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3)));
        }

        return messages;
    }

    private async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE dbo.OutboxMessages SET Status = 'PUBLISHED', ProcessedAt = SYSUTCDATETIME() WHERE Id = @Id AND Status = 'PENDING';",
            connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task IncrementRetryCountAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE dbo.OutboxMessages SET RetryCount = RetryCount + 1 WHERE Id = @Id AND Status = 'PENDING';",
            connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record OutboxMessage(Guid Id, string EventType, string Payload, Guid CorrelationId);
}