using Planning.Api;
using Planning.Domain;
using Xunit;

namespace Planning.Tests;

public sealed class RouteCapabilityDurationTests
{
    [Fact]
    public async Task GetEstimatedDurations_ReturnsConfiguredModeRangeOnly()
    {
        await using var db = TestDbContextFactory.Create();
        var origin = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var capability = new RouteTransportCapability
        {
            OriginLocationId = origin,
            DestinationLocationId = destination,
            AllowedModesJson = "[\"ROAD\",\"AIR\"]"
        };
        db.RouteTransportCapabilities.Add(capability);
        db.RouteTransportCapabilityModes.AddRange(
            new RouteTransportCapabilityMode
            {
                RouteTransportCapabilityId = capability.Id,
                Mode = "ROAD",
                MinDurationMinutes = 180,
                MaxDurationMinutes = 300
            },
            new RouteTransportCapabilityMode
            {
                RouteTransportCapabilityId = capability.Id,
                Mode = "AIR",
                MinDurationMinutes = 90,
                MaxDurationMinutes = 150
            });
        await db.SaveChangesAsync();

        var estimates = await RouteCapabilityEndpoints.GetEstimatedDurationsAsync(db, origin, destination, ["ROAD"]);

        var estimate = Assert.Single(estimates);
        Assert.Equal("ROAD", estimate.Mode);
        Assert.Equal(180, estimate.MinDurationMinutes);
        Assert.Equal(300, estimate.MaxDurationMinutes);
    }

    [Fact]
    public async Task GetEstimatedDurations_WithoutConfiguredRouteReturnsEmpty()
    {
        await using var db = TestDbContextFactory.Create();

        var estimates = await RouteCapabilityEndpoints.GetEstimatedDurationsAsync(
            db, Guid.NewGuid(), Guid.NewGuid(), ["ROAD"]);

        Assert.Empty(estimates);
    }
}
