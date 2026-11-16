using System.Security.Claims;
using BuildingBlocks.Api;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.MasterData;
using Planning.Application.Services;

namespace Planning.Api;

public static class MasterDataEndpoints
{
    public static RouteGroupBuilder MapCustomerCountryLookup(this RouteGroupBuilder api)
    {
        api.MapGet("/customer/countries", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetCountriesAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<CountryDto>>.Success(result.Items,
                new PaginationMeta { Page = result.Page, PageSize = result.PageSize, TotalItems = result.TotalItems, TotalPages = result.TotalPages }));
        }).WithName("ListCountryNamesForCustomerAndCompliance");
        api.MapGet("/customer/countries/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<CountryDto>.Success(await svc.GetCountryByIdAsync(id, ct))))
            .WithName("GetCountryNameForCustomerHorseDetails");
        return api;
    }

    public static RouteGroupBuilder MapPlanningMasterData(this RouteGroupBuilder api)
    {
        // ----------------------------------------------------
        // Countries
        // ----------------------------------------------------
        api.MapGet("/countries", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetCountriesAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<CountryDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        });

        api.MapGet("/countries/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<CountryDto>.Success(await svc.GetCountryByIdAsync(id, ct))));

        api.MapPost("/countries", async (CountryInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateCountryAsync(input, ct);
            return Results.Created($"/api/v1/countries/{created.Id}", ApiResponse<CountryDto>.Success(created, "Country created successfully"));
        }).RequireAuthorization("PlanningDataAdmin");

        api.MapPut("/countries/{id:guid}", async (Guid id, CountryInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<CountryDto>.Success(await svc.UpdateCountryAsync(id, input, ct), "Country updated successfully")))
            .RequireAuthorization("PlanningDataAdmin");

        api.MapPut("/countries/{id:guid}/support", async (Guid id, SupportFlagInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var existing = await svc.GetCountryByIdAsync(id, ct);
            var updated = await svc.UpdateCountryAsync(id, new CountryInput(existing.IsoCode, existing.Name, input.Active), ct);
            return Results.Ok(ApiResponse<CountryDto>.Success(updated, "Country support updated."));
        }).RequireAuthorization("CountrySupportAdmin");

        api.MapDelete("/countries/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteCountryAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization("PlanningDataAdmin");

        // ----------------------------------------------------
        // Locations
        // ----------------------------------------------------
        api.MapGet("/locations", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetLocationsAsync(filter, ct);
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
        });

        api.MapGet("/locations/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<LocationDto>.Success(await svc.GetLocationByIdAsync(id, ct))));

        api.MapPost("/locations", async (LocationInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateLocationAsync(input, ct);
            return Results.Created($"/api/v1/locations/{created.Id}", ApiResponse<LocationDto>.Success(created, "Location created successfully"));
        });

        api.MapPut("/locations/{id:guid}", async (Guid id, LocationInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<LocationDto>.Success(await svc.UpdateLocationAsync(id, input, ct), "Location updated successfully")));

        api.MapDelete("/locations/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteLocationAsync(id, ct);
            return Results.NoContent();
        });

        // ----------------------------------------------------
        // Vehicles
        // ----------------------------------------------------
        api.MapGet("/vehicles", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetVehiclesAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<VehicleDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        });

        api.MapGet("/vehicles/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<VehicleDto>.Success(await svc.GetVehicleByIdAsync(id, ct))));

        api.MapPost("/vehicles", async (VehicleInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateVehicleAsync(input, ct);
            return Results.Created($"/api/v1/vehicles/{created.Id}", ApiResponse<VehicleDto>.Success(created, "Vehicle created successfully"));
        });

        api.MapPut("/vehicles/{id:guid}", async (Guid id, VehicleInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<VehicleDto>.Success(await svc.UpdateVehicleAsync(id, input, ct), "Vehicle updated successfully")));

        api.MapDelete("/vehicles/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteVehicleAsync(id, ct);
            return Results.NoContent();
        });

        // ----------------------------------------------------
        // Stalls
        // ----------------------------------------------------
        api.MapGet("/stalls", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetStallsAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<StallDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        });

        api.MapGet("/stalls/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<StallDto>.Success(await svc.GetStallByIdAsync(id, ct))));

        api.MapPost("/stalls", async (StallInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateStallAsync(input, ct);
            return Results.Created($"/api/v1/stalls/{created.Id}", ApiResponse<StallDto>.Success(created, "Stall created successfully"));
        });

        api.MapPut("/stalls/{id:guid}", async (Guid id, StallInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<StallDto>.Success(await svc.UpdateStallAsync(id, input, ct), "Stall updated successfully")));

        api.MapDelete("/stalls/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteStallAsync(id, ct);
            return Results.NoContent();
        });

        // ----------------------------------------------------
        // Airlines
        // ----------------------------------------------------
        api.MapGet("/airlines", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetAirlinesAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<AirlineDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        });

        api.MapGet("/airlines/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<AirlineDto>.Success(await svc.GetAirlineByIdAsync(id, ct))));

        api.MapPost("/airlines", async (AirlineInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAirlineAsync(input, ct);
            return Results.Created($"/api/v1/airlines/{created.Id}", ApiResponse<AirlineDto>.Success(created, "Airline created successfully"));
        }).RequireAuthorization("PlanningDataAdmin");

        api.MapPut("/airlines/{id:guid}", async (Guid id, AirlineInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<AirlineDto>.Success(await svc.UpdateAirlineAsync(id, input, ct), "Airline updated successfully")))
            .RequireAuthorization("PlanningDataAdmin");

        api.MapDelete("/airlines/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteAirlineAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization("PlanningDataAdmin");

        // ----------------------------------------------------
        // Flight Bookings
        // ----------------------------------------------------
        api.MapGet("/flight-bookings", async ([AsParameters] PaginationFilter filter, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var result = await svc.GetFlightBookingsAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<FlightBookingDto>>.Success(
                result.Items,
                new PaginationMeta
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalItems = result.TotalItems,
                    TotalPages = result.TotalPages
                }
            ));
        });

        api.MapGet("/flight-bookings/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<FlightBookingDto>.Success(await svc.GetFlightBookingByIdAsync(id, ct))));

        api.MapPost("/flight-bookings", async (FlightInput input, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateFlightBookingAsync(input, ct);
            return Results.Created($"/api/v1/flight-bookings/{created.Id}", ApiResponse<FlightBookingDto>.Success(created, "Flight booking created successfully"));
        }).RequireAuthorization("PlanningDataAdmin");

        api.MapPut("/flight-bookings/{id:guid}", async (Guid id, FlightInput input, IFleetMasterDataService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<FlightBookingDto>.Success(await svc.UpdateFlightBookingAsync(id, input, ct), "Flight booking updated successfully")))
            .RequireAuthorization("PlanningDataAdmin");

        api.MapPost("/flight-bookings/{id:guid}/confirm", async (Guid id, ClaimsPrincipal principal, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var actor = Guid.TryParse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;
            if (actor == Guid.Empty) return Results.Unauthorized();
            var booking = await svc.SetFlightBookingConfirmationAsync(id, actor, "CONFIRMED", ct);
            return Results.Ok(ApiResponse<FlightBookingDto>.Success(booking, "Manual flight booking confirmation recorded."));
        }).RequireAuthorization("FlightBookingManage").WithName("ConfirmInternalFlightBooking");

        api.MapPost("/flight-bookings/{id:guid}/cancel", async (Guid id, ClaimsPrincipal principal, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            var actor = Guid.TryParse(principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : Guid.Empty;
            if (actor == Guid.Empty) return Results.Unauthorized();
            var booking = await svc.SetFlightBookingConfirmationAsync(id, actor, "CANCELLED", ct);
            return Results.Ok(ApiResponse<FlightBookingDto>.Success(booking, "Flight booking cancelled."));
        }).RequireAuthorization("FlightBookingManage").WithName("CancelInternalFlightBooking");

        api.MapDelete("/flight-bookings/{id:guid}", async (Guid id, IFleetMasterDataService svc, CancellationToken ct) =>
        {
            await svc.DeleteFlightBookingAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization("PlanningDataAdmin");

        // ----------------------------------------------------
        // Availability Queries (T09)
        // ----------------------------------------------------
        api.MapGet("/availability/vehicles", async (Guid vehicleId, DateTime from, DateTime to, IAvailabilityService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<object>.Success(await svc.CheckVehicleAvailabilityAsync(vehicleId, from, to, ct))));

        api.MapGet("/availability/stalls", async (Guid stallId, DateTime from, DateTime to, IAvailabilityService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<object>.Success(await svc.CheckStallAvailabilityAsync(stallId, from, to, ct))));

        api.MapGet("/availability/flights", async (Guid flightBookingId, DateTime from, DateTime to, IAvailabilityService svc, CancellationToken ct) =>
            Results.Ok(ApiResponse<object>.Success(await svc.CheckFlightAvailabilityAsync(flightBookingId, from, to, ct))));

        return api;
    }
}

public sealed record SupportFlagInput(bool Active);
