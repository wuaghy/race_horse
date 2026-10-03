using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.MasterData;
using Planning.Domain;

namespace Planning.Application.Services;

public class FleetMasterDataService(IPlanningDbContext db) : IFleetMasterDataService
{
    // ==========================================
    // Countries
    // ==========================================
    public async Task<PagedResult<CountryDto>> GetCountriesAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.Countries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => x.IsoCode.ToLower().Contains(search) || x.Name.ToLower().Contains(search));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderBy(x => x.IsoCode)
            .Skip(page * size)
            .Take(size)
            .Select(x => new CountryDto(x.Id, x.IsoCode, x.Name, x.Active))
            .ToListAsync(ct);

        return new PagedResult<CountryDto>(items, page, size, total);
    }

    public async Task<CountryDto> GetCountryByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Country", id);
        return new CountryDto(x.Id, x.IsoCode, x.Name, x.Active);
    }

    public async Task<CountryDto> CreateCountryAsync(CountryInput input, CancellationToken ct = default)
    {
        if (await db.Countries.AnyAsync(c => c.IsoCode.ToLower() == input.IsoCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Country with ISO code '{input.IsoCode}' already exists.", "DUPLICATE_ISO_CODE", "isoCode");

        var entity = new Country
        {
            IsoCode = input.IsoCode.Trim().ToUpperInvariant(),
            Name = input.Name.Trim(),
            Active = input.Active
        };

        db.Countries.Add(entity);
        await db.SaveChangesAsync(ct);

        return new CountryDto(entity.Id, entity.IsoCode, entity.Name, entity.Active);
    }

    public async Task<CountryDto> UpdateCountryAsync(Guid id, CountryInput input, CancellationToken ct = default)
    {
        var entity = await db.Countries.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Country", id);

        if (await db.Countries.AnyAsync(c => c.Id != id && c.IsoCode.ToLower() == input.IsoCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Country with ISO code '{input.IsoCode}' already exists.", "DUPLICATE_ISO_CODE", "isoCode");

        entity.IsoCode = input.IsoCode.Trim().ToUpperInvariant();
        entity.Name = input.Name.Trim();
        entity.Active = input.Active;

        await db.SaveChangesAsync(ct);
        return new CountryDto(entity.Id, entity.IsoCode, entity.Name, entity.Active);
    }

    public async Task DeleteCountryAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Countries.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Country", id);

        if (await db.Locations.AnyAsync(l => l.CountryId == id, ct))
            throw new BusinessRuleException("Cannot delete country because it is associated with existing locations.", "COUNTRY_IN_USE");

        db.Countries.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Locations
    // ==========================================
    public async Task<PagedResult<LocationDto>> GetLocationsAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.Locations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => (x.LocationCode != null && x.LocationCode.ToLower().Contains(search)) || x.Name.ToLower().Contains(search));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderBy(x => x.Name)
            .Skip(page * size)
            .Take(size)
            .Select(x => new LocationDto(x.Id, x.LocationCode, x.Name, x.LocationType, x.Address, x.City, x.CountryId, x.Latitude, x.Longitude, x.Active))
            .ToListAsync(ct);

        return new PagedResult<LocationDto>(items, page, size, total);
    }

    public async Task<LocationDto> GetLocationByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Location", id);
        return new LocationDto(x.Id, x.LocationCode, x.Name, x.LocationType, x.Address, x.City, x.CountryId, x.Latitude, x.Longitude, x.Active);
    }

    public async Task<LocationDto> CreateLocationAsync(LocationInput input, CancellationToken ct = default)
    {
        if (!await db.Countries.AnyAsync(c => c.Id == input.CountryId, ct))
            throw new NotFoundException("Country", input.CountryId);

        if (!string.IsNullOrWhiteSpace(input.LocationCode) &&
            await db.Locations.AnyAsync(l => l.LocationCode != null && l.LocationCode.ToLower() == input.LocationCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Location with code '{input.LocationCode}' already exists.", "DUPLICATE_LOCATION_CODE", "locationCode");

        var entity = new Location
        {
            LocationCode = input.LocationCode?.Trim(),
            Name = input.Name.Trim(),
            LocationType = input.LocationType.Trim().ToUpperInvariant(),
            Address = input.Address?.Trim(),
            City = input.City?.Trim(),
            CountryId = input.CountryId,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Active = input.Active
        };

        db.Locations.Add(entity);
        await db.SaveChangesAsync(ct);

        return new LocationDto(entity.Id, entity.LocationCode, entity.Name, entity.LocationType, entity.Address, entity.City, entity.CountryId, entity.Latitude, entity.Longitude, entity.Active);
    }

    public async Task<LocationDto> UpdateLocationAsync(Guid id, LocationInput input, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Location", id);

        if (!await db.Countries.AnyAsync(c => c.Id == input.CountryId, ct))
            throw new NotFoundException("Country", input.CountryId);

        if (!string.IsNullOrWhiteSpace(input.LocationCode) &&
            await db.Locations.AnyAsync(l => l.Id != id && l.LocationCode != null && l.LocationCode.ToLower() == input.LocationCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Location with code '{input.LocationCode}' already exists.", "DUPLICATE_LOCATION_CODE", "locationCode");

        entity.LocationCode = input.LocationCode?.Trim();
        entity.Name = input.Name.Trim();
        entity.LocationType = input.LocationType.Trim().ToUpperInvariant();
        entity.Address = input.Address?.Trim();
        entity.City = input.City?.Trim();
        entity.CountryId = input.CountryId;
        entity.Latitude = input.Latitude;
        entity.Longitude = input.Longitude;
        entity.Active = input.Active;

        await db.SaveChangesAsync(ct);
        return new LocationDto(entity.Id, entity.LocationCode, entity.Name, entity.LocationType, entity.Address, entity.City, entity.CountryId, entity.Latitude, entity.Longitude, entity.Active);
    }

    public async Task DeleteLocationAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Locations.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Location", id);

        if (await db.RouteLegs.AnyAsync(r => r.OriginLocationId == id || r.DestinationLocationId == id, ct) ||
            await db.FlightBookings.AnyAsync(f => f.OriginAirportId == id || f.DestinationAirportId == id, ct) ||
            await db.Checkpoints.AnyAsync(c => c.LocationId == id, ct))
        {
            throw new BusinessRuleException("Location is in use by route legs, flight bookings, or checkpoints.", "LOCATION_IN_USE");
        }

        db.Locations.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Vehicles
    // ==========================================
    public async Task<PagedResult<VehicleDto>> GetVehiclesAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.Vehicles.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => x.VehicleCode.ToLower().Contains(search) || x.PlateNumber.ToLower().Contains(search));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderBy(x => x.VehicleCode)
            .Skip(page * size)
            .Take(size)
            .Select(x => new VehicleDto(x.Id, x.VehicleCode, x.PlateNumber, x.VehicleType, x.HorseCapacity, x.StallCapacity, x.TemperatureControlled, x.Status, x.CreatedAt, x.UpdatedAt, x.VersionNo))
            .ToListAsync(ct);

        return new PagedResult<VehicleDto>(items, page, size, total);
    }

    public async Task<VehicleDto> GetVehicleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle", id);
        return new VehicleDto(x.Id, x.VehicleCode, x.PlateNumber, x.VehicleType, x.HorseCapacity, x.StallCapacity, x.TemperatureControlled, x.Status, x.CreatedAt, x.UpdatedAt, x.VersionNo);
    }

    public async Task<VehicleDto> CreateVehicleAsync(VehicleInput input, CancellationToken ct = default)
    {
        if (input.HorseCapacity <= 0 || input.StallCapacity <= 0)
            throw new BusinessRuleException("Horse and stall capacity must be greater than zero.", "INVALID_CAPACITY", "horseCapacity");

        if (await db.Vehicles.AnyAsync(v => v.VehicleCode.ToLower() == input.VehicleCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Vehicle code '{input.VehicleCode}' already exists.", "DUPLICATE_VEHICLE_CODE", "vehicleCode");

        if (await db.Vehicles.AnyAsync(v => v.PlateNumber.ToLower() == input.PlateNumber.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Plate number '{input.PlateNumber}' already exists.", "DUPLICATE_PLATE_NUMBER", "plateNumber");

        var entity = new Vehicle
        {
            VehicleCode = input.VehicleCode.Trim().ToUpperInvariant(),
            PlateNumber = input.PlateNumber.Trim().ToUpperInvariant(),
            VehicleType = input.VehicleType.Trim().ToUpperInvariant(),
            HorseCapacity = input.HorseCapacity,
            StallCapacity = input.StallCapacity,
            TemperatureControlled = input.TemperatureControlled,
            Status = string.IsNullOrWhiteSpace(input.Status) ? PlanningConstants.VehicleStatus.Available : input.Status.Trim().ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            VersionNo = 1
        };

        db.Vehicles.Add(entity);
        await db.SaveChangesAsync(ct);

        return new VehicleDto(entity.Id, entity.VehicleCode, entity.PlateNumber, entity.VehicleType, entity.HorseCapacity, entity.StallCapacity, entity.TemperatureControlled, entity.Status, entity.CreatedAt, entity.UpdatedAt, entity.VersionNo);
    }

    public async Task<VehicleDto> UpdateVehicleAsync(Guid id, VehicleInput input, CancellationToken ct = default)
    {
        var entity = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle", id);

        if (input.HorseCapacity <= 0 || input.StallCapacity <= 0)
            throw new BusinessRuleException("Horse and stall capacity must be greater than zero.", "INVALID_CAPACITY", "horseCapacity");

        if (await db.Vehicles.AnyAsync(v => v.Id != id && v.VehicleCode.ToLower() == input.VehicleCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Vehicle code '{input.VehicleCode}' already exists.", "DUPLICATE_VEHICLE_CODE", "vehicleCode");

        if (await db.Vehicles.AnyAsync(v => v.Id != id && v.PlateNumber.ToLower() == input.PlateNumber.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Plate number '{input.PlateNumber}' already exists.", "DUPLICATE_PLATE_NUMBER", "plateNumber");

        entity.VehicleCode = input.VehicleCode.Trim().ToUpperInvariant();
        entity.PlateNumber = input.PlateNumber.Trim().ToUpperInvariant();
        entity.VehicleType = input.VehicleType.Trim().ToUpperInvariant();
        entity.HorseCapacity = input.HorseCapacity;
        entity.StallCapacity = input.StallCapacity;
        entity.TemperatureControlled = input.TemperatureControlled;
        entity.Status = input.Status.Trim().ToUpperInvariant();
        entity.UpdatedAt = DateTime.UtcNow;
        entity.VersionNo++;

        await db.SaveChangesAsync(ct);
        return new VehicleDto(entity.Id, entity.VehicleCode, entity.PlateNumber, entity.VehicleType, entity.HorseCapacity, entity.StallCapacity, entity.TemperatureControlled, entity.Status, entity.CreatedAt, entity.UpdatedAt, entity.VersionNo);
    }

    public async Task DeleteVehicleAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vehicle", id);

        if (await db.TripLegAssignments.AnyAsync(a => a.VehicleId == id, ct))
            throw new BusinessRuleException("Vehicle has assigned trips/legs and cannot be deleted.", "VEHICLE_IN_USE");

        db.Vehicles.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Stalls
    // ==========================================
    public async Task<PagedResult<StallDto>> GetStallsAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.Stalls.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => x.StallCode.ToLower().Contains(search) || (x.StallType != null && x.StallType.ToLower().Contains(search)));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderBy(x => x.StallCode)
            .Skip(page * size)
            .Take(size)
            .Select(x => new StallDto(x.Id, x.StallCode, x.StallType, x.Capacity, x.Active, x.Notes))
            .ToListAsync(ct);

        return new PagedResult<StallDto>(items, page, size, total);
    }

    public async Task<StallDto> GetStallByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.Stalls.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Stall", id);
        return new StallDto(x.Id, x.StallCode, x.StallType, x.Capacity, x.Active, x.Notes);
    }

    public async Task<StallDto> CreateStallAsync(StallInput input, CancellationToken ct = default)
    {
        if (input.Capacity <= 0)
            throw new BusinessRuleException("Stall capacity must be greater than zero.", "INVALID_CAPACITY", "capacity");

        if (await db.Stalls.AnyAsync(s => s.StallCode.ToLower() == input.StallCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Stall code '{input.StallCode}' already exists.", "DUPLICATE_STALL_CODE", "stallCode");

        var entity = new Stall
        {
            StallCode = input.StallCode.Trim().ToUpperInvariant(),
            StallType = input.StallType?.Trim(),
            Capacity = input.Capacity,
            Active = input.Active,
            Notes = input.Notes?.Trim()
        };

        db.Stalls.Add(entity);
        await db.SaveChangesAsync(ct);

        return new StallDto(entity.Id, entity.StallCode, entity.StallType, entity.Capacity, entity.Active, entity.Notes);
    }

    public async Task<StallDto> UpdateStallAsync(Guid id, StallInput input, CancellationToken ct = default)
    {
        var entity = await db.Stalls.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Stall", id);

        if (input.Capacity <= 0)
            throw new BusinessRuleException("Stall capacity must be greater than zero.", "INVALID_CAPACITY", "capacity");

        if (await db.Stalls.AnyAsync(s => s.Id != id && s.StallCode.ToLower() == input.StallCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Stall code '{input.StallCode}' already exists.", "DUPLICATE_STALL_CODE", "stallCode");

        entity.StallCode = input.StallCode.Trim().ToUpperInvariant();
        entity.StallType = input.StallType?.Trim();
        entity.Capacity = input.Capacity;
        entity.Active = input.Active;
        entity.Notes = input.Notes?.Trim();

        await db.SaveChangesAsync(ct);
        return new StallDto(entity.Id, entity.StallCode, entity.StallType, entity.Capacity, entity.Active, entity.Notes);
    }

    public async Task DeleteStallAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Stalls.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Stall", id);

        if (await db.HorseLegAssignments.AnyAsync(h => h.StallId == id, ct))
            throw new BusinessRuleException("Stall has assigned horses and cannot be deleted.", "STALL_IN_USE");

        db.Stalls.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Airlines
    // ==========================================
    public async Task<PagedResult<AirlineDto>> GetAirlinesAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.Airlines.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => x.AirlineCode.ToLower().Contains(search) || x.Name.ToLower().Contains(search));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderBy(x => x.AirlineCode)
            .Skip(page * size)
            .Take(size)
            .Select(x => new AirlineDto(x.Id, x.AirlineCode, x.Name, x.Active))
            .ToListAsync(ct);

        return new PagedResult<AirlineDto>(items, page, size, total);
    }

    public async Task<AirlineDto> GetAirlineByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.Airlines.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Airline", id);
        return new AirlineDto(x.Id, x.AirlineCode, x.Name, x.Active);
    }

    public async Task<AirlineDto> CreateAirlineAsync(AirlineInput input, CancellationToken ct = default)
    {
        if (await db.Airlines.AnyAsync(a => a.AirlineCode.ToLower() == input.AirlineCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Airline code '{input.AirlineCode}' already exists.", "DUPLICATE_AIRLINE_CODE", "airlineCode");

        var entity = new Airline
        {
            AirlineCode = input.AirlineCode.Trim().ToUpperInvariant(),
            Name = input.Name.Trim(),
            Active = input.Active
        };

        db.Airlines.Add(entity);
        await db.SaveChangesAsync(ct);

        return new AirlineDto(entity.Id, entity.AirlineCode, entity.Name, entity.Active);
    }

    public async Task<AirlineDto> UpdateAirlineAsync(Guid id, AirlineInput input, CancellationToken ct = default)
    {
        var entity = await db.Airlines.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Airline", id);

        if (await db.Airlines.AnyAsync(a => a.Id != id && a.AirlineCode.ToLower() == input.AirlineCode.Trim().ToLower(), ct))
            throw new BusinessRuleException($"Airline code '{input.AirlineCode}' already exists.", "DUPLICATE_AIRLINE_CODE", "airlineCode");

        entity.AirlineCode = input.AirlineCode.Trim().ToUpperInvariant();
        entity.Name = input.Name.Trim();
        entity.Active = input.Active;

        await db.SaveChangesAsync(ct);
        return new AirlineDto(entity.Id, entity.AirlineCode, entity.Name, entity.Active);
    }

    public async Task DeleteAirlineAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Airlines.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Airline", id);

        if (await db.FlightBookings.AnyAsync(f => f.AirlineId == id, ct))
            throw new BusinessRuleException("Airline has associated flight bookings and cannot be deleted.", "AIRLINE_IN_USE");

        db.Airlines.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    // ==========================================
    // Flight Bookings
    // ==========================================
    public async Task<PagedResult<FlightBookingDto>> GetFlightBookingsAsync(PaginationFilter filter, CancellationToken ct = default)
    {
        var q = db.FlightBookings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            q = q.Where(x => x.BookingReference.ToLower().Contains(search) || x.FlightNumber.ToLower().Contains(search));
        }

        var total = await q.LongCountAsync(ct);
        var page = filter.NormalizedPage;
        var size = filter.NormalizedPageSize;

        var items = await q
            .OrderByDescending(x => x.ScheduledDepartureAt)
            .Skip(page * size)
            .Take(size)
            .Select(x => new FlightBookingDto(x.Id, x.BookingReference, x.AirlineId, x.FlightNumber, x.OriginAirportId, x.DestinationAirportId, x.ScheduledDepartureAt, x.ScheduledArrivalAt, x.ActualDepartureAt, x.ActualArrivalAt))
            .ToListAsync(ct);

        return new PagedResult<FlightBookingDto>(items, page, size, total);
    }

    public async Task<FlightBookingDto> GetFlightBookingByIdAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.FlightBookings.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException("FlightBooking", id);
        return new FlightBookingDto(x.Id, x.BookingReference, x.AirlineId, x.FlightNumber, x.OriginAirportId, x.DestinationAirportId, x.ScheduledDepartureAt, x.ScheduledArrivalAt, x.ActualDepartureAt, x.ActualArrivalAt);
    }

    public async Task<FlightBookingDto> CreateFlightBookingAsync(FlightInput input, CancellationToken ct = default)
    {
        await ValidateFlightBookingInputAsync(input, null, ct);

        var entity = new FlightBooking
        {
            BookingReference = input.BookingReference.Trim().ToUpperInvariant(),
            AirlineId = input.AirlineId,
            FlightNumber = input.FlightNumber.Trim().ToUpperInvariant(),
            OriginAirportId = input.OriginAirportId,
            DestinationAirportId = input.DestinationAirportId,
            ScheduledDepartureAt = input.ScheduledDepartureAt,
            ScheduledArrivalAt = input.ScheduledArrivalAt
        };

        db.FlightBookings.Add(entity);
        await db.SaveChangesAsync(ct);

        return new FlightBookingDto(entity.Id, entity.BookingReference, entity.AirlineId, entity.FlightNumber, entity.OriginAirportId, entity.DestinationAirportId, entity.ScheduledDepartureAt, entity.ScheduledArrivalAt, entity.ActualDepartureAt, entity.ActualArrivalAt);
    }

    public async Task<FlightBookingDto> UpdateFlightBookingAsync(Guid id, FlightInput input, CancellationToken ct = default)
    {
        var entity = await db.FlightBookings.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException("FlightBooking", id);

        await ValidateFlightBookingInputAsync(input, id, ct);

        entity.BookingReference = input.BookingReference.Trim().ToUpperInvariant();
        entity.AirlineId = input.AirlineId;
        entity.FlightNumber = input.FlightNumber.Trim().ToUpperInvariant();
        entity.OriginAirportId = input.OriginAirportId;
        entity.DestinationAirportId = input.DestinationAirportId;
        entity.ScheduledDepartureAt = input.ScheduledDepartureAt;
        entity.ScheduledArrivalAt = input.ScheduledArrivalAt;

        await db.SaveChangesAsync(ct);
        return new FlightBookingDto(entity.Id, entity.BookingReference, entity.AirlineId, entity.FlightNumber, entity.OriginAirportId, entity.DestinationAirportId, entity.ScheduledDepartureAt, entity.ScheduledArrivalAt, entity.ActualDepartureAt, entity.ActualArrivalAt);
    }

    public async Task DeleteFlightBookingAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.FlightBookings.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException("FlightBooking", id);

        if (await db.TripLegAssignments.AnyAsync(a => a.FlightBookingId == id, ct))
            throw new BusinessRuleException("Flight booking is assigned to a trip route leg and cannot be deleted.", "FLIGHT_BOOKING_IN_USE");

        db.FlightBookings.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateFlightBookingInputAsync(FlightInput input, Guid? currentId, CancellationToken ct)
    {
        if (input.ScheduledArrivalAt <= input.ScheduledDepartureAt)
            throw new BusinessRuleException("Flight arrival time must be strictly after departure time.", "INVALID_SCHEDULE", "scheduledArrivalAt");

        if (input.OriginAirportId == input.DestinationAirportId)
            throw new BusinessRuleException("Origin and destination airports must be distinct locations.", "SAME_AIRPORT", "destinationAirportId");

        if (!await db.Airlines.AnyAsync(a => a.Id == input.AirlineId, ct))
            throw new NotFoundException("Airline", input.AirlineId);

        var origin = await db.Locations.FirstOrDefaultAsync(l => l.Id == input.OriginAirportId, ct)
            ?? throw new NotFoundException("Origin Location", input.OriginAirportId);
        var dest = await db.Locations.FirstOrDefaultAsync(l => l.Id == input.DestinationAirportId, ct)
            ?? throw new NotFoundException("Destination Location", input.DestinationAirportId);

        if (origin.LocationType != PlanningConstants.LocationType.Airport || dest.LocationType != PlanningConstants.LocationType.Airport)
            throw new BusinessRuleException("Both origin and destination locations must be of type AIRPORT.", "NOT_AN_AIRPORT");

        var duplicateQuery = db.FlightBookings.Where(f => f.BookingReference.ToLower() == input.BookingReference.Trim().ToLower());
        if (currentId.HasValue)
            duplicateQuery = duplicateQuery.Where(f => f.Id != currentId.Value);

        if (await duplicateQuery.AnyAsync(ct))
            throw new BusinessRuleException($"Flight booking reference '{input.BookingReference}' already exists.", "DUPLICATE_BOOKING_REFERENCE", "bookingReference");
    }
}
