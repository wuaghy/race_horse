namespace Booking.Application.Dashboard;

public sealed record BookingDashboardSummary(
    long TotalCustomers,
    long TotalHorses,
    long ActiveHorses,
    long TotalRequests,
    long PendingReviewRequests,
    long ApprovedRequests,
    long TotalOrders);

public interface IBookingDashboardService
{
    Task<BookingDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
