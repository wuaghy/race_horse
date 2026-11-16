using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts.Grpc.Compliance;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.Services;

namespace Planning.Api.Grpc;

public sealed class PlanningComplianceGrpcClient(
    IPlanningDbContext db,
    ComplianceQuery.ComplianceQueryClient client,
    IConfiguration configuration) : ITripComplianceEvaluator
{
    public async Task<TripComplianceEvaluation> EvaluateAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await db.TransportTrips.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tripId, cancellationToken);
        if (trip is null) return new TripComplianceEvaluation(false, ["Trip was not found in Planning."]);

        var plan = await db.RoutePlans.AsNoTracking().SingleOrDefaultAsync(item => item.TripId == tripId, cancellationToken);
        var version = plan?.ActiveVersionId is Guid activeVersionId
            ? await db.RoutePlanVersions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == activeVersionId &&
                item.Status == Planning.Domain.PlanningConstants.RouteVersionStatus.Active, cancellationToken)
            : null;
        if (version is null)
            return new TripComplianceEvaluation(false, ["Trip has no ACTIVE route version."]);

        var legs = await db.RouteLegs.AsNoTracking().Where(item => item.RoutePlanVersionId == version.Id)
            .OrderBy(item => item.SequenceNo).ToListAsync(cancellationToken);
        if (legs.Count == 0)
            return new TripComplianceEvaluation(false, ["ACTIVE route version has no route legs."]);
        if (legs.Any(item => !item.PlannedDepartureAt.HasValue))
            return new TripComplianceEvaluation(false, ["Every active route leg needs a planned departure before compliance can be evaluated."]);

        var locationIds = legs.SelectMany(item => new[] { item.OriginLocationId, item.DestinationLocationId }).Distinct().ToArray();
        var locations = await db.Locations.AsNoTracking().Where(item => locationIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (legs.Any(item => !locations.ContainsKey(item.OriginLocationId) || !locations.ContainsKey(item.DestinationLocationId)))
            return new TripComplianceEvaluation(false, ["An active route leg has a missing location."]);

        var orders = await db.TripOrders.AsNoTracking().Where(order => order.TripId == tripId).ToListAsync(cancellationToken);
        var requestIds = orders.Where(order => order.RequestId.HasValue).Select(order => order.RequestId!.Value).Distinct().ToArray();
        var horseIds = orders.SelectMany(order =>
        {
            try { return JsonSerializer.Deserialize<Guid[]>(order.HorseIdsJson) ?? []; }
            catch (JsonException) { return []; }
        }).Distinct().ToArray();
        if (requestIds.Length == 0)
            return new TripComplianceEvaluation(false, ["Trip has no request ownership projection for compliance checks."]);
        if (horseIds.Length == 0)
            return new TripComplianceEvaluation(false, ["Trip has no requested horses to evaluate for compliance."]);

        var request = new EvaluateTripReadinessRequest { TripId = tripId.ToString() };
        request.RequestIds.AddRange(requestIds.Select(id => id.ToString()));
        request.HorseIds.AddRange(horseIds.Select(id => id.ToString()));
        foreach (var leg in legs)
        {
            var origin = locations[leg.OriginLocationId];
            var destination = locations[leg.DestinationLocationId];
            request.RouteLegs.Add(new Contracts.Grpc.Compliance.ComplianceRouteLeg
            {
                RouteLegId = leg.Id.ToString(),
                OriginCountryId = origin.CountryId.ToString(),
                DestinationCountryId = destination.CountryId.ToString(),
                PlannedDepartureAt = leg.PlannedDepartureAt!.Value.ToUniversalTime().ToString("O"),
                PlannedArrivalAt = leg.PlannedArrivalAt?.ToUniversalTime().ToString("O") ?? string.Empty
            });
        }

        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException("Jwt:SigningKey is required for internal Compliance gRPC calls.");
        var metadata = new Metadata { { "x-internal-service-key", signingKey } };
        var result = await client.EvaluateTripReadinessAsync(request, headers: metadata, cancellationToken: cancellationToken);
        return new TripComplianceEvaluation(result.IsReady, result.Blockers.ToArray());
    }
}
