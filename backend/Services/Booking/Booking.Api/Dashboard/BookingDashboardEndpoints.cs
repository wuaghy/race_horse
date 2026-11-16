using Booking.Application.Dashboard;
using BuildingBlocks.Api;

namespace Booking.Api.Dashboard;

public static class BookingDashboardEndpoints
{
    public static void MapBookingDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/manager/dashboard/booking", async (
            IBookingDashboardService service,
            CancellationToken cancellationToken) =>
        {
            var result = await service.GetSummaryAsync(cancellationToken);
            return Results.Ok(ApiResponse<BookingDashboardSummary>.Success(result));
        })
        .RequireAuthorization("LogisticsManager")
        .WithName("GetBookingDashboardSummary");
    }
}
