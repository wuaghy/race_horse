using Compliance.Grpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Readiness;

namespace Planning.Infrastructure.Clients;

public class ComplianceGrpcClient : IComplianceVerificationClient
{
    private readonly ComplianceRpc.ComplianceRpcClient _client;
    private readonly IPlanningDbContext _db;
    private readonly ILogger<ComplianceGrpcClient> _logger;

    public ComplianceGrpcClient(
        ComplianceRpc.ComplianceRpcClient client,
        IPlanningDbContext db,
        ILogger<ComplianceGrpcClient> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        Guid tripId,
        Guid originCountryId,
        Guid destinationCountryId,
        IReadOnlyList<Guid> horseIds,
        DateOnly? plannedDepartureDate = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Dispatching gRPC VerifyTripCompliance request for TripId {TripId} (Origin: {Origin}, Destination: {Dest}, Horses: {Count})",
            tripId, originCountryId, destinationCountryId, horseIds.Count);

        var request = new VerifyTripComplianceRequest
        {
            TripId = tripId.ToString(),
            OriginCountryId = originCountryId == Guid.Empty ? string.Empty : originCountryId.ToString(),
            DestinationCountryId = destinationCountryId == Guid.Empty ? string.Empty : destinationCountryId.ToString(),
            PlannedDepartureDate = plannedDepartureDate?.ToString("yyyy-MM-dd") ?? string.Empty
        };

        foreach (var horseId in horseIds)
        {
            request.HorseIds.Add(horseId.ToString());
        }

        var response = await _client.VerifyTripComplianceAsync(request, cancellationToken: cancellationToken);

        var checkItems = response.CheckItems.Select(item => new ComplianceCheckItemDto(
            item.DocumentTypeCode,
            item.DocumentTypeName,
            item.Scope,
            item.TargetId,
            item.Status,
            item.IsMet,
            item.Details
        )).ToList();

        var evaluatedAt = DateTime.TryParse(response.EvaluatedAt, out var dt) ? dt : DateTime.UtcNow;

        var dto = new TripComplianceVerificationDto(
            Guid.TryParse(response.TripId, out var parsedId) ? parsedId : tripId,
            response.IsCompliant,
            response.TotalRequired,
            response.TotalMet,
            response.TotalMissing,
            response.TotalExpired,
            response.BlockerMessages.ToList(),
            checkItems,
            evaluatedAt
        );

        _logger.LogInformation("gRPC VerifyTripCompliance response for TripId {TripId}: Compliant={IsCompliant}, Met={Met}/{Total}, Blockers={BlockerCount}",
            tripId, dto.IsCompliant, dto.TotalMet, dto.TotalRequired, dto.BlockerMessages.Count);

        return dto;
    }

    public async Task<TripComplianceVerificationDto> VerifyTripComplianceAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await _db.TransportTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
        var horseIds = await _db.HorseLegAssignments.AsNoTracking()
            .Where(h => h.TripId == tripId)
            .Select(h => h.HorseId)
            .Distinct()
            .ToListAsync(cancellationToken);

        Guid originCountryId = Guid.Empty;
        Guid destinationCountryId = Guid.Empty;

        var plan = await _db.RoutePlans.AsNoTracking().FirstOrDefaultAsync(p => p.TripId == tripId, cancellationToken);
        if (plan?.ActiveVersionId != null)
        {
            var legs = await _db.RouteLegs.AsNoTracking()
                .Where(l => l.RoutePlanVersionId == plan.ActiveVersionId.Value)
                .OrderBy(l => l.SequenceNo)
                .ToListAsync(cancellationToken);

            if (legs.Count > 0)
            {
                var originLocId = legs[0].OriginLocationId;
                var destLocId = legs[^1].DestinationLocationId;

                originCountryId = await _db.Locations.AsNoTracking()
                    .Where(l => l.Id == originLocId)
                    .Select(l => l.CountryId)
                    .FirstOrDefaultAsync(cancellationToken);

                destinationCountryId = await _db.Locations.AsNoTracking()
                    .Where(l => l.Id == destLocId)
                    .Select(l => l.CountryId)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        DateOnly? departureDate = trip?.PlannedDepartureAt.HasValue == true
            ? DateOnly.FromDateTime(trip.PlannedDepartureAt.Value)
            : null;

        return await VerifyTripComplianceAsync(tripId, originCountryId, destinationCountryId, horseIds, departureDate, cancellationToken);
    }
}
