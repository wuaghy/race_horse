using BuildingBlocks.Exceptions;
using Planning.Application.DTOs.Common;
using Planning.Application.DTOs.MasterData;
using Planning.Application.Services;
using Planning.Domain;
using Xunit;

namespace Planning.Tests;

public class FleetMasterDataServiceTests
{
    [Fact]
    public async Task CreateCountry_DuplicateIsoCode_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new FleetMasterDataService(db);

        await svc.CreateCountryAsync(new CountryInput("VN", "Vietnam", true));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateCountryAsync(new CountryInput("vn", "Viet Nam Second", true)));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task CreateVehicle_InvalidCapacity_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new FleetMasterDataService(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateVehicleAsync(new VehicleInput("V01", "51H-12345", "TRUCK", 0, 4, true, "AVAILABLE")));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateVehicleAsync(new VehicleInput("V02", "51H-12346", "TRUCK", 4, -1, true, "AVAILABLE")));
    }

    [Fact]
    public async Task CreateFlightBooking_InvalidTimes_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new FleetMasterDataService(db);

        var country = await svc.CreateCountryAsync(new CountryInput("VN", "Vietnam", true));
        var sgn = await svc.CreateLocationAsync(new LocationInput("SGN", "Tan Son Nhat", "AIRPORT", null, "HCM", country.Id, null, null, true));
        var han = await svc.CreateLocationAsync(new LocationInput("HAN", "Noi Bai", "AIRPORT", null, "HN", country.Id, null, null, true));
        var airline = await svc.CreateAirlineAsync(new AirlineInput("VN", "Vietnam Airlines", true));

        var now = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateFlightBookingAsync(new FlightInput("BK-01", airline.Id, "VN123", sgn.Id, han.Id, now, now.AddHours(-1))));

        Assert.Equal("INVALID_SCHEDULE", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task CreateFlightBooking_SameAirports_ThrowsBusinessRuleException()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new FleetMasterDataService(db);

        var country = await svc.CreateCountryAsync(new CountryInput("VN", "Vietnam", true));
        var sgn = await svc.CreateLocationAsync(new LocationInput("SGN", "Tan Son Nhat", "AIRPORT", null, "HCM", country.Id, null, null, true));
        var airline = await svc.CreateAirlineAsync(new AirlineInput("VN", "Vietnam Airlines", true));

        var now = DateTime.UtcNow;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            svc.CreateFlightBookingAsync(new FlightInput("BK-02", airline.Id, "VN124", sgn.Id, sgn.Id, now, now.AddHours(2))));

        Assert.Equal("SAME_AIRPORT", ex.Errors?[0].Reason);
    }

    [Fact]
    public async Task GetVehicles_Pagination_ReturnsCorrectMeta()
    {
        using var db = TestDbContextFactory.Create();
        var svc = new FleetMasterDataService(db);

        for (int i = 1; i <= 5; i++)
        {
            await svc.CreateVehicleAsync(new VehicleInput($"V0{i}", $"PLATE-0{i}", "TRUCK", 4, 4, true, "AVAILABLE"));
        }

        var page0 = await svc.GetVehiclesAsync(new PaginationFilter { Page = 0, PageSize = 2 });
        Assert.Equal(2, page0.Items.Count);
        Assert.Equal(5, page0.TotalItems);
        Assert.Equal(3, page0.TotalPages);

        var page2 = await svc.GetVehiclesAsync(new PaginationFilter { Page = 2, PageSize = 2 });
        Assert.Single(page2.Items);
    }
}
