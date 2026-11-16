using Identity.Application.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.Dashboard;

public sealed class SqlIdentityDashboardService(IConfiguration configuration) : IIdentityDashboardService
{
    private readonly string _connectionString = configuration.GetConnectionString("IdentityDb")
        ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb must be configured.");

    public async Task<IdentityDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @Since DATETIME2(3) = DATEADD(DAY, -30, SYSUTCDATETIME());
            SELECT
                (SELECT COUNT_BIG(*) FROM dbo.Users WHERE Status = 'ACTIVE') AS ActiveUsers,
                (SELECT COUNT_BIG(*) FROM dbo.Users u INNER JOIN dbo.Roles r ON r.Id = u.RoleId WHERE r.Code = 'CUSTOMER' AND u.Status = 'ACTIVE') AS CustomerAccounts,
                (SELECT COUNT_BIG(*) FROM dbo.RefreshTokens WHERE CreatedAt >= @Since) AS SessionsLast30Days,
                (SELECT COUNT_BIG(DISTINCT UserId) FROM dbo.RefreshTokens WHERE CreatedAt >= @Since) AS ActiveUsersLast30Days;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new IdentityDashboardSummary(0, 0, 0, 0);

        return new IdentityDashboardSummary(
            reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3));
    }
}
