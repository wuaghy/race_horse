using BuildingBlocks.Api;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.MasterData;
using Planning.Application.Services;

namespace Planning.Api;

public static class BookingLocationReadEndpoints
{
    public static RouteGroupBuilder MapBookingLocationRead(this RouteGroupBuilder api)
    {
        api.MapGet("/booking/locations", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetLocationsAsync(filter, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<LocationDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        }).WithName("ListBookingLocations");

        api.MapGet("/booking/locations/nearest-receiving", async (decimal latitude, decimal longitude, IFleetMasterDataService service, CancellationToken cancellationToken) =>
        {
            var result = await service.FindNearestReceivingLocationAsync(latitude, longitude, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<object>.Failure(40400, "No active receiving location with coordinates is available."))
                : Results.Ok(ApiResponse<NearestReceivingLocationDto>.Success(result));
        }).WithName("FindNearestReceivingLocation");

        return api;
    }
}
