using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.MasterData;

namespace Planning.Application.Services;

public interface IFleetMasterDataService
{
    // Countries
    Task<PagedResult<CountryDto>> GetCountriesAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<CountryDto> GetCountryByIdAsync(Guid id, CancellationToken ct = default);
    Task<CountryDto> CreateCountryAsync(CountryInput input, CancellationToken ct = default);
    Task<CountryDto> UpdateCountryAsync(Guid id, CountryInput input, CancellationToken ct = default);
    Task DeleteCountryAsync(Guid id, CancellationToken ct = default);

    // Locations
    Task<PagedResult<LocationDto>> GetLocationsAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<LocationDto> GetLocationByIdAsync(Guid id, CancellationToken ct = default);
    Task<LocationDto> CreateLocationAsync(LocationInput input, CancellationToken ct = default);
    Task<LocationDto> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken ct = default);
    Task DeleteLocationAsync(Guid id, CancellationToken ct = default);

    // Vehicles
    Task<PagedResult<VehicleDto>> GetVehiclesAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<VehicleDto> GetVehicleByIdAsync(Guid id, CancellationToken ct = default);
    Task<VehicleDto> CreateVehicleAsync(VehicleInput input, CancellationToken ct = default);
    Task<VehicleDto> UpdateVehicleAsync(Guid id, VehicleInput input, CancellationToken ct = default);
    Task DeleteVehicleAsync(Guid id, CancellationToken ct = default);

    // Stalls
    Task<PagedResult<StallDto>> GetStallsAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<StallDto> GetStallByIdAsync(Guid id, CancellationToken ct = default);
    Task<StallDto> CreateStallAsync(StallInput input, CancellationToken ct = default);
    Task<StallDto> UpdateStallAsync(Guid id, StallInput input, CancellationToken ct = default);
    Task DeleteStallAsync(Guid id, CancellationToken ct = default);

    // Airlines
    Task<PagedResult<AirlineDto>> GetAirlinesAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<AirlineDto> GetAirlineByIdAsync(Guid id, CancellationToken ct = default);
    Task<AirlineDto> CreateAirlineAsync(AirlineInput input, CancellationToken ct = default);
    Task<AirlineDto> UpdateAirlineAsync(Guid id, AirlineInput input, CancellationToken ct = default);
    Task DeleteAirlineAsync(Guid id, CancellationToken ct = default);

    // FlightBookings
    Task<PagedResult<FlightBookingDto>> GetFlightBookingsAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<FlightBookingDto> GetFlightBookingByIdAsync(Guid id, CancellationToken ct = default);
    Task<FlightBookingDto> CreateFlightBookingAsync(FlightInput input, CancellationToken ct = default);
    Task<FlightBookingDto> UpdateFlightBookingAsync(Guid id, FlightInput input, CancellationToken ct = default);
    Task DeleteFlightBookingAsync(Guid id, CancellationToken ct = default);
}
