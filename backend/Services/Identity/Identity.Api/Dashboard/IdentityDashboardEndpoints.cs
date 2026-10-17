using BuildingBlocks.Api;
using Identity.Application.Dashboard;

namespace Identity.Api.Dashboard;

public static class IdentityDashboardEndpoints
{
    public static void MapIdentityDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/manager/dashboard/identity", async (
            IIdentityDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetSummaryAsync(cancellationToken);
            return Results.Ok(ApiResponse<IdentityDashboardSummary>.Success(result));
        })
        .RequireAuthorization("LogisticsManager")
        .WithName("GetIdentityDashboardSummary");
    }
}
