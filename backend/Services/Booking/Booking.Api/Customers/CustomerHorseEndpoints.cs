using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Booking.Application.Customers;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;

namespace Booking.Api.Customers;

public static class CustomerHorseEndpoints
{
    public static void MapCustomerHorseEndpoints(this WebApplication app)
    {
        var customer = app.MapGroup("/api/v1/customers")
            .RequireAuthorization("Customer");
        customer.MapGet("/me", async (ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var profile = await service.GetProfileAsync(GetUserId(principal), cancellationToken);
            return profile is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Customer profile was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<CustomerProfile>.Success(profile, requestId: GetRequestId(context)));
        });
        customer.MapPut("/me", async (SaveCustomerProfileRequest request, ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var profile = await service.SaveProfileAsync(
                GetUserId(principal),
                principal.FindFirstValue("email") ?? throw new UnauthorizedException(),
                new CustomerProfileInput(request.Name, request.CustomerType, request.ContactPerson, request.Phone, request.Address, request.CountryId, request.VersionNo),
                cancellationToken);
            return Results.Ok(ApiResponse<CustomerProfile>.Success(profile, "Customer profile saved.", GetRequestId(context)));
        }).WithName("SaveCustomerProfile");

        var horses = app.MapGroup("/api/v1/horses")
            .RequireAuthorization("Customer");
        horses.MapGet("/", async (int? page, int? pageSize, string? search, ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var result = await service.GetHorsesAsync(GetUserId(principal), page ?? 0, pageSize ?? 20, search, cancellationToken);
            var pagination = new PaginationMeta
            {
                Page = result.Page,
                PageSize = result.PageSize,
                TotalItems = result.TotalItems,
                TotalPages = result.TotalPages
            };
            return Results.Ok(ApiResponse<IReadOnlyList<HorseRecord>>.Success(result.Items, pagination, requestId: GetRequestId(context)));
        }).WithName("ListOwnedHorses");
        horses.MapGet("/{horseId:guid}", async (Guid horseId, ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var horse = await service.GetHorseAsync(GetUserId(principal), horseId, cancellationToken);
            return horse is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "Horse was not found.", requestId: GetRequestId(context)))
                : Results.Ok(ApiResponse<HorseRecord>.Success(horse, requestId: GetRequestId(context)));
        }).WithName("GetOwnedHorse");
        horses.MapPost("/", async (SaveHorseRequest request, ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var horse = await service.CreateHorseAsync(GetUserId(principal), ToInput(request), cancellationToken);
            return Results.Created($"/api/v1/horses/{horse.Id}", ApiResponse<HorseRecord>.Success(horse, "Horse created.", GetRequestId(context)));
        }).WithName("CreateHorse");
        horses.MapPut("/{horseId:guid}", async (Guid horseId, SaveHorseRequest request, ClaimsPrincipal principal, ICustomerHorseService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var horse = await service.UpdateHorseAsync(GetUserId(principal), horseId, ToInput(request, request.VersionNo), cancellationToken);
            return Results.Ok(ApiResponse<HorseRecord>.Success(horse, "Horse updated.", GetRequestId(context)));
        }).WithName("UpdateHorse");
        horses.MapDelete("/{horseId:guid}", async (Guid horseId, ClaimsPrincipal principal, ICustomerHorseService service, CancellationToken cancellationToken) =>
        {
            await service.DeleteHorseAsync(GetUserId(principal), horseId, cancellationToken);
            return Results.NoContent();
        }).WithName("DeleteHorse");
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out var userId) ? userId : throw new UnauthorizedException();

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();

    private static HorseInput ToInput(SaveHorseRequest request, int? versionNo = null) => new(
        request.HorseName,
        request.RegistrationNumber,
        request.PassportNumber,
        request.Breed,
        request.Sex,
        request.DateOfBirth,
        request.Color,
        request.CountryOfOriginId,
        request.SpecialRequirements,
        versionNo);
}

public sealed record SaveCustomerProfileRequest
{
    [Required, MaxLength(255)]
    public required string Name { get; init; }

    [Required, MaxLength(30)]
    public required string CustomerType { get; init; }

    [MaxLength(200)]
    public string? ContactPerson { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(500)]
    public string? Address { get; init; }

    public Guid? CountryId { get; init; }

    public int? VersionNo { get; init; }
}

public sealed record SaveHorseRequest
{
    [Required, MaxLength(200)]
    public required string HorseName { get; init; }

    [MaxLength(100)]
    public string? RegistrationNumber { get; init; }

    [MaxLength(100)]
    public string? PassportNumber { get; init; }

    [MaxLength(100)]
    public string? Breed { get; init; }

    [MaxLength(30)]
    public string? Sex { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [MaxLength(100)]
    public string? Color { get; init; }

    public Guid? CountryOfOriginId { get; init; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; init; }

    public int? VersionNo { get; init; }
}
