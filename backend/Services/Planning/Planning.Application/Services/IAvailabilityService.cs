using Planning.Application.DTOs.Availability;

namespace Planning.Application.Services;

public interface IAvailabilityService
{
    Task<AvailabilityResponse> CheckVehicleAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<AvailabilityResponse> CheckStallAvailabilityAsync(Guid stallId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<AvailabilityResponse> CheckFlightAvailabilityAsync(Guid flightBookingId, DateTime from, DateTime to, CancellationToken ct = default);
}
