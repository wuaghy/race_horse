using Booking.Application.Customers;
using Booking.Application.TransportRequests;
using Booking.Infrastructure.Customers;
using Booking.Infrastructure.Messaging;
using Booking.Infrastructure.TransportRequests;
using Microsoft.Extensions.DependencyInjection;

namespace Booking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICustomerHorseService, SqlCustomerHorseService>();
        services.AddScoped<ITransportRequestService, SqlTransportRequestService>();
        services.AddHostedService<BookingOutboxPublisher>();
        return services;
    }
}