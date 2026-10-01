namespace Planning.Application.DTOs.MasterData;

public record CountryDto(Guid Id, string IsoCode, string Name, bool Active);
public record CountryInput(string IsoCode, string Name, bool Active);

public record LocationDto(
    Guid Id,
    string? LocationCode,
    string Name,
    string LocationType,
    string? Address,
    string? City,
    Guid CountryId,
    decimal? Latitude,
    decimal? Longitude,
    bool Active
);
public record LocationInput(
    string? LocationCode,
    string Name,
    string LocationType,
    string? Address,
    string? City,
    Guid CountryId,
    decimal? Latitude,
    decimal? Longitude,
    bool Active
);

public record VehicleDto(
    Guid Id,
    string VehicleCode,
    string PlateNumber,
    string VehicleType,
    int HorseCapacity,
    int StallCapacity,
    bool TemperatureControlled,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int VersionNo
);
public record VehicleInput(
    string VehicleCode,
    string PlateNumber,
    string VehicleType,
    int HorseCapacity,
    int StallCapacity,
    bool TemperatureControlled,
    string Status
);

public record StallDto(
    Guid Id,
    string StallCode,
    string? StallType,
    int Capacity,
    bool Active,
    string? Notes
);
public record StallInput(
    string StallCode,
    string? StallType,
    int Capacity,
    bool Active,
    string? Notes
);

public record AirlineDto(Guid Id, string AirlineCode, string Name, bool Active);
public record AirlineInput(string AirlineCode, string Name, bool Active);

public record FlightBookingDto(
    Guid Id,
    string BookingReference,
    Guid AirlineId,
    string FlightNumber,
    Guid OriginAirportId,
    Guid DestinationAirportId,
    DateTime ScheduledDepartureAt,
    DateTime ScheduledArrivalAt,
    DateTime? ActualDepartureAt,
    DateTime? ActualArrivalAt
);
public record FlightInput(
    string BookingReference,
    Guid AirlineId,
    string FlightNumber,
    Guid OriginAirportId,
    Guid DestinationAirportId,
    DateTime ScheduledDepartureAt,
    DateTime ScheduledArrivalAt
);
