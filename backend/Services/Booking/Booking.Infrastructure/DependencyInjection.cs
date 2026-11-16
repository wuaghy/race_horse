using Booking.Application.Customers;
using Booking.Application.TransportRequests;
using Booking.Application.Payments;
using Booking.Infrastructure.Customers;
using Booking.Application.Dashboard;
using Booking.Infrastructure.Dashboard;
using Booking.Infrastructure.Messaging;
using Booking.Infrastructure.TransportRequests;
using Booking.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient("PayOS");
        services.AddScoped<ICustomerHorseService, SqlCustomerHorseService>();
        services.AddScoped<IBookingDashboardService, SqlBookingDashboardService>();
        services.AddScoped<ITransportRequestService, SqlTransportRequestService>();
        services.AddScoped<IPaymentService, SqlPaymentService>();
        services.AddHostedService<BookingOutboxPublisher>();
        services.AddHostedService<BookingEventConsumer>();
        return services;
    }
}
