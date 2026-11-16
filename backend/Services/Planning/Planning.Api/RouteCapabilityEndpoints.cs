using System.Security.Claims;
using System.Text.Json;
using BuildingBlocks.Api;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Domain;

namespace Planning.Api;

public sealed record RouteTransportModesDto(
    Guid OriginLocationId,
    Guid DestinationLocationId,
    bool Configured,
    IReadOnlyList<string> AllowedModes,
    Guid? OriginCountryId,
    Guid? DestinationCountryId,
    IReadOnlyList<RouteModeDurationEstimateDto> EstimatedDurations);

public sealed record RouteModeDurationEstimateDto(string Mode, int MinDurationMinutes, int MaxDurationMinutes, int? MinExpressDurationMinutes = null, int? MaxExpressDurationMinutes = null);
public sealed record RouteModeDurationEstimateInput(string? Mode, int MinDurationMinutes, int MaxDurationMinutes, int? MinExpressDurationMinutes = null, int? MaxExpressDurationMinutes = null);

public sealed record SaveRouteTransportModesRequest(
    Guid OriginLocationId,
    Guid DestinationLocationId,
    IReadOnlyList<string> AllowedModes,
    IReadOnlyList<RouteModeDurationEstimateInput>? EstimatedDurations = null);

public static class RouteCapabilityEndpoints
{
    private static readonly string[] ValidModes = ["ROAD", "AIR", "MIXED"];

    public static RouteGroupBuilder MapRouteCapabilityEndpoints(this RouteGroupBuilder api, IConfiguration configuration)
    {
        api.MapGet("/route-capabilities", async (Guid originLocationId, Guid destinationLocationId, IPlanningDbContext db, CancellationToken ct) =>
        {
            if (!AreDistinctLocations(originLocationId, destinationLocationId))
                return Results.BadRequest(ApiResponse<object>.Failure(400, "Choose different origin and destination locations."));

            var (configured, modes) = await GetCapabilitiesAsync(db, configuration, originLocationId, destinationLocationId, ct);
            var locations = await GetActiveLocationsAsync(db, originLocationId, destinationLocationId, ct);
            var configuredForActiveLocations = configured && locations.Count == 2;
            var durations = configuredForActiveLocations ? await GetEstimatedDurationsAsync(db, originLocationId, destinationLocationId, modes, ct) : [];
            return Results.Ok(ApiResponse<RouteTransportModesDto>.Success(ToDto(originLocationId, destinationLocationId, configuredForActiveLocations, configuredForActiveLocations ? modes : [], locations, durations)));
        })
        .WithName("GetRouteTransportModeCapabilities");

        api.MapGet("/route-capabilities/catalog", async (IPlanningDbContext db, CancellationToken ct) =>
        {
            var capabilityRows = await db.RouteTransportCapabilities.AsNoTracking().ToListAsync(ct);
            var locationRows = await (from location in db.Locations.AsNoTracking()
                                      join country in db.Countries.AsNoTracking() on location.CountryId equals country.Id
                                      where location.Active && country.Active
                                      select new { location.Id, location.CountryId }).ToListAsync(ct);
            var locations = locationRows.Select(location => new LocationLookup(location.Id, location.CountryId)).ToList();
            var locationIds = locations.Select(location => location.Id).ToHashSet();
            var rows = capabilityRows
                .Where(row => locationIds.Contains(row.OriginLocationId) && locationIds.Contains(row.DestinationLocationId))
                .OrderBy(row => row.OriginLocationId).ThenBy(row => row.DestinationLocationId).ToArray();
            var result = new List<RouteTransportModesDto>(rows.Length);
            foreach (var row in rows)
            {
                var modes = ReadModes(row.AllowedModesJson);
                var durations = await GetEstimatedDurationsAsync(db, row.OriginLocationId, row.DestinationLocationId, modes, ct);
                result.Add(ToDto(row.OriginLocationId, row.DestinationLocationId, true, modes, locations, durations));
            }
            return Results.Ok(ApiResponse<IReadOnlyList<RouteTransportModesDto>>.Success(result));
        })
        .RequireAuthorization("PlanningDataAdmin")
        .WithName("GetRouteTransportCapabilityCatalog");

        api.MapPut("/route-capabilities", async (
            SaveRouteTransportModesRequest request,
            ClaimsPrincipal principal,
            IPlanningDbContext db,
            CancellationToken ct) =>
        {
            if (!AreDistinctLocations(request.OriginLocationId, request.DestinationLocationId))
                return Results.BadRequest(ApiResponse<object>.Failure(400, "Choose different origin and destination locations."));
            var modes = (request.AllowedModes ?? []).Select(mode => (mode ?? string.Empty).Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal).ToArray();
            if (modes.Length == 0 || modes.Any(mode => !ValidModes.Contains(mode, StringComparer.Ordinal)))
                return Results.BadRequest(ApiResponse<object>.Failure(400, "Select at least one valid transport mode: ROAD, AIR, or MIXED."));

            var locations = await GetActiveLocationsAsync(db, request.OriginLocationId, request.DestinationLocationId, ct);
            if (locations.Count != 2)
                return Results.BadRequest(ApiResponse<object>.Failure(400, "Both route locations must exist and be active."));

            var durationEstimates = request.EstimatedDurations ?? [];
            if (durationEstimates.GroupBy(item => (item.Mode ?? string.Empty).Trim().ToUpperInvariant()).Any(group => group.Count() > 1) ||
                durationEstimates.Any(item => !modes.Contains((item.Mode ?? string.Empty).Trim().ToUpperInvariant(), StringComparer.Ordinal) ||
                    item.MinDurationMinutes <= 0 || item.MaxDurationMinutes < item.MinDurationMinutes ||
                    (item.MinExpressDurationMinutes.HasValue != item.MaxExpressDurationMinutes.HasValue) ||
                    (item.MinExpressDurationMinutes.HasValue && (item.MinExpressDurationMinutes <= 0 || item.MaxExpressDurationMinutes < item.MinExpressDurationMinutes))))
                return Results.BadRequest(ApiResponse<object>.Failure(400, "Duration ranges must use a unique allowed mode and positive minutes; express estimates must specify a valid minimum and maximum together."));

            var row = await db.RouteTransportCapabilities.SingleOrDefaultAsync(item =>
                item.OriginLocationId == request.OriginLocationId && item.DestinationLocationId == request.DestinationLocationId, ct);
            if (row is null)
            {
                row = new RouteTransportCapability
                {
                    OriginLocationId = request.OriginLocationId,
                    DestinationLocationId = request.DestinationLocationId
                };
                db.RouteTransportCapabilities.Add(row);
            }
            row.AllowedModesJson = JsonSerializer.Serialize(modes);
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = ReadActorId(principal) ?? Guid.Empty;
            var existingEstimates = await db.RouteTransportCapabilityModes
                .Where(item => item.RouteTransportCapabilityId == row.Id).ToListAsync(ct);
            if (request.EstimatedDurations is not null)
            {
                var configuredByMode = durationEstimates.ToDictionary(item => item.Mode!.Trim().ToUpperInvariant(), StringComparer.Ordinal);
                db.RouteTransportCapabilityModes.RemoveRange(existingEstimates.Where(item => !configuredByMode.ContainsKey(item.Mode)));
                foreach (var estimate in durationEstimates)
                {
                    var mode = estimate.Mode!.Trim().ToUpperInvariant();
                    var existing = existingEstimates.SingleOrDefault(item => item.Mode == mode);
                    if (existing is null)
                    {
                        db.RouteTransportCapabilityModes.Add(new RouteTransportCapabilityMode
                        {
                            RouteTransportCapabilityId = row.Id,
                            Mode = mode,
                            MinDurationMinutes = estimate.MinDurationMinutes,
                            MaxDurationMinutes = estimate.MaxDurationMinutes,
                            MinExpressDurationMinutes = estimate.MinExpressDurationMinutes,
                            MaxExpressDurationMinutes = estimate.MaxExpressDurationMinutes
                        });
                    }
                    else
                    {
                        existing.MinDurationMinutes = estimate.MinDurationMinutes;
                        existing.MaxDurationMinutes = estimate.MaxDurationMinutes;
                        existing.MinExpressDurationMinutes = estimate.MinExpressDurationMinutes;
                        existing.MaxExpressDurationMinutes = estimate.MaxExpressDurationMinutes;
                    }
                }
            }
            else
            {
                db.RouteTransportCapabilityModes.RemoveRange(existingEstimates.Where(item => !modes.Contains(item.Mode, StringComparer.Ordinal)));
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(ApiResponse<RouteTransportModesDto>.Success(ToDto(row.OriginLocationId, row.DestinationLocationId, true, modes, locations,
                await GetEstimatedDurationsAsync(db, row.OriginLocationId, row.DestinationLocationId, modes, ct))));
        })
        .RequireAuthorization("PlanningDataAdmin")
        .WithName("SaveRouteTransportCapability");

        return api;
    }

    public static async Task<(bool Configured, IReadOnlyList<string> AllowedModes)> GetCapabilitiesAsync(
        IPlanningDbContext db,
        IConfiguration configuration,
        Guid originLocationId,
        Guid destinationLocationId,
        CancellationToken ct = default)
    {
        var row = await db.RouteTransportCapabilities.AsNoTracking().FirstOrDefaultAsync(item =>
            item.OriginLocationId == originLocationId && item.DestinationLocationId == destinationLocationId, ct);
        if (row is not null)
        {
            var modes = ReadModes(row.AllowedModesJson);
            return (modes.Count > 0, modes);
        }
        return GetCapabilities(configuration, originLocationId, destinationLocationId);
    }

    public static (bool Configured, IReadOnlyList<string> AllowedModes) GetCapabilities(
        IConfiguration configuration,
        Guid originLocationId,
        Guid destinationLocationId)
    {
        var capabilities = ReadCapabilities(configuration);
        return capabilities.TryGetValue(RouteKey(originLocationId, destinationLocationId), out var modes)
            ? (true, modes)
            : (false, []);
    }

    private static async Task<List<LocationLookup>> GetActiveLocationsAsync(IPlanningDbContext db, Guid origin, Guid destination, CancellationToken ct)
    {
        var rows = await (from location in db.Locations.AsNoTracking()
                          join country in db.Countries.AsNoTracking() on location.CountryId equals country.Id
                          where location.Active && country.Active && (location.Id == origin || location.Id == destination)
                          select new { location.Id, location.CountryId })
            .ToListAsync(ct);
        return rows.Select(location => new LocationLookup(location.Id, location.CountryId)).ToList();
    }

    public static async Task<IReadOnlyList<RouteModeDurationEstimateDto>> GetEstimatedDurationsAsync(
        IPlanningDbContext db, Guid origin, Guid destination, IReadOnlyList<string> modes, CancellationToken ct = default)
    {
        var capabilityId = await db.RouteTransportCapabilities.AsNoTracking()
            .Where(row => row.OriginLocationId == origin && row.DestinationLocationId == destination)
            .Select(row => (Guid?)row.Id).SingleOrDefaultAsync(ct);
        if (!capabilityId.HasValue || modes.Count == 0) return [];
        return await db.RouteTransportCapabilityModes.AsNoTracking()
            .Where(item => item.RouteTransportCapabilityId == capabilityId.Value && modes.Contains(item.Mode))
            .OrderBy(item => item.Mode)
            .Select(item => new RouteModeDurationEstimateDto(item.Mode, item.MinDurationMinutes, item.MaxDurationMinutes, item.MinExpressDurationMinutes, item.MaxExpressDurationMinutes))
            .ToListAsync(ct);
    }

    private static RouteTransportModesDto ToDto(Guid origin, Guid destination, bool configured, IReadOnlyList<string> modes, IReadOnlyCollection<LocationLookup> locations, IReadOnlyList<RouteModeDurationEstimateDto> durations) =>
        new(origin, destination, configured, modes,
            locations.FirstOrDefault(location => location.Id == origin)?.CountryId,
            locations.FirstOrDefault(location => location.Id == destination)?.CountryId,
            durations);

    private static IReadOnlyList<string> ReadModes(string serialized)
    {
        try
        {
            var modes = JsonSerializer.Deserialize<string[]>(serialized) ?? [];
            return modes.Select(mode => mode.ToUpperInvariant()).Distinct(StringComparer.Ordinal).Where(mode => ValidModes.Contains(mode, StringComparer.Ordinal)).ToArray();
        }
        catch (JsonException) { return []; }
    }

    private static bool AreDistinctLocations(Guid origin, Guid destination) => origin != Guid.Empty && destination != Guid.Empty && origin != destination;
    private static Guid? ReadActorId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
    private static string RouteKey(Guid originLocationId, Guid destinationLocationId) => $"{originLocationId:D}>{destinationLocationId:D}";

    private sealed record LocationLookup(Guid Id, Guid CountryId);

    private static Dictionary<string, IReadOnlyList<string>> ReadCapabilities(IConfiguration configuration)
    {
        var capabilities = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var route in configuration.GetSection("RouteTransportModes").GetChildren())
        {
            var parts = route.Key.Split('>');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var originId) || !Guid.TryParse(parts[1], out var destinationId) || originId == destinationId)
                throw new InvalidOperationException($"Invalid RouteTransportModes key '{route.Key}'. Use '<origin-location-guid>><destination-location-guid>'.");
            var modes = route.Get<string[]>() ?? [];
            var normalized = modes.Select(mode => mode.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToArray();
            if (normalized.Any(mode => !ValidModes.Contains(mode, StringComparer.Ordinal)))
                throw new InvalidOperationException($"RouteTransportModes['{route.Key}'] may contain only ROAD, AIR, and MIXED.");
            capabilities[RouteKey(originId, destinationId)] = normalized;
        }
        return capabilities;
    }
}
