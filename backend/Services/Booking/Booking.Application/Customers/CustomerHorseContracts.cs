namespace Booking.Application.Customers;

public sealed record CustomerProfile(
    Guid Id,
    string CustomerCode,
    string Name,
    string CustomerType,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    Guid? CountryId,
    int VersionNo);

public sealed record HorseRecord(
    Guid Id,
    string HorseCode,
    string HorseName,
    string? RegistrationNumber,
    string? PassportNumber,
    string? Breed,
    string? Sex,
    DateOnly? DateOfBirth,
    string? Color,
    Guid? CountryOfOriginId,
    string? SpecialRequirements,
    string Status,
    int VersionNo);

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public sealed record CustomerProfileInput(
    string Name,
    string CustomerType,
    string? ContactPerson,
    string? Phone,
    string? Address,
    Guid? CountryId,
    int? VersionNo);

public sealed record HorseInput(
    string HorseName,
    string? RegistrationNumber,
    string? PassportNumber,
    string? Breed,
    string? Sex,
    DateOnly? DateOfBirth,
    string? Color,
    Guid? CountryOfOriginId,
    string? SpecialRequirements,
    int? VersionNo);

public interface ICustomerHorseService
{
    Task<CustomerProfile?> GetProfileAsync(Guid identityUserId, CancellationToken cancellationToken);

    Task<CustomerProfile> SaveProfileAsync(
        Guid identityUserId,
        string email,
        CustomerProfileInput input,
        CancellationToken cancellationToken);

    Task<PageResult<HorseRecord>> GetHorsesAsync(
        Guid identityUserId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken);

    Task<HorseRecord?> GetHorseAsync(Guid identityUserId, Guid horseId, CancellationToken cancellationToken);

    Task<HorseRecord> CreateHorseAsync(Guid identityUserId, HorseInput input, CancellationToken cancellationToken);

    Task<HorseRecord> UpdateHorseAsync(Guid identityUserId, Guid horseId, HorseInput input, CancellationToken cancellationToken);

    Task DeleteHorseAsync(Guid identityUserId, Guid horseId, CancellationToken cancellationToken);
}
