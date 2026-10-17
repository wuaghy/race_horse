using Booking.Application.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Booking.Infrastructure.Dashboard;

public sealed class SqlBookingDashboardService(IConfiguration configuration) : IBookingDashboardService
{
    private readonly string _connectionString = configuration.GetConnectionString("BookingDb")
        ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");

    public async Task<BookingDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT COUNT_BIG(*) FROM dbo.Customers) AS TotalCustomers,
                (SELECT COUNT_BIG(*) FROM dbo.Horses) AS TotalHorses,
                (SELECT COUNT_BIG(*) FROM dbo.Horses WHERE Status = 'ACTIVE') AS ActiveHorses,
                (SELECT COUNT_BIG(*) FROM dbo.TransportRequests) AS TotalRequests,
                (SELECT COUNT_BIG(*) FROM dbo.TransportRequests WHERE Status IN ('SUBMITTED', 'UNDER_REVIEW')) AS PendingReviewRequests,
                (SELECT COUNT_BIG(*) FROM dbo.TransportRequests WHERE Status = 'APPROVED') AS ApprovedRequests,
                (SELECT COUNT_BIG(*) FROM dbo.TransportOrders) AS TotalOrders;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new BookingDashboardSummary(0, 0, 0, 0, 0, 0, 0);

        return new BookingDashboardSummary(
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
            reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6));
    }
}
