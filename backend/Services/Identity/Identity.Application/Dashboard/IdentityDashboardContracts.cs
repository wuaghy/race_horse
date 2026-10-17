namespace Identity.Application.Dashboard;

public sealed record IdentityDashboardSummary(
    long ActiveUsers,
    long CustomerAccounts,
    long SessionsLast30Days,
    long ActiveUsersLast30Days);

public interface IIdentityDashboardService
{
    Task<IdentityDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
